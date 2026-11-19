using System.Globalization;
using PhytoRegistre.CatalogBuilder.Sftp;

namespace PhytoRegistre.CatalogBuilder;

/// <summary>Fichiers retenus sur le serveur.</summary>
internal sealed record RemoteSelection(RemoteEntry Full, IReadOnlyList<RemoteEntry> Daily, RemoteEntry? Suspensions);

/// <summary>Patrons de sélection et dossier distant.</summary>
internal sealed record DownloadRequest(string RemotePath, string OutDir, string FullPattern, string DailyPattern, string SuspensionsPattern, bool DryRun)
{
    public static DownloadRequest FromCommandLine(CommandLine cl, string outDir) => new(
        cl.Get("--path", "/"),
        outDir,
        cl.Get("--full-pattern", FilePatterns.DefaultFull),
        cl.Get("--daily-pattern", FilePatterns.DefaultDaily),
        cl.Get("--suspensions-pattern", FilePatterns.DefaultSuspensions),
        cl.Has("--dry-run"));
}

/// <summary>
/// Télécharge le dernier export complet, les fichiers quotidiens postérieurs et suspensions.json :
/// &lt;out&gt;/full/, &lt;out&gt;/daily/, &lt;out&gt;/suspensions.json.
/// </summary>
internal sealed class DownloadCommand(ISftpSourceFactory factory, TextWriter output)
{
    public const string FullDir = "full";
    public const string DailyDir = "daily";
    public const string SuspensionsFile = "suspensions.json";

    public int Run(SftpSettings settings, DownloadRequest request)
    {
        using var session = RemoteSession.Open(factory, settings, output);
        var files = session.Walk(request.RemotePath).Where(e => !e.IsDirectory).ToList();
        var selection = Select(files, request);
        Report(selection, request.DryRun);
        if (request.DryRun)
        {
            output.WriteLine("Mode simulation (--dry-run) : aucun fichier téléchargé.");
            return ExitCodes.Success;
        }

        Fetch(session, selection, request.OutDir);
        output.WriteLine($"Téléchargement terminé dans {Path.GetFullPath(request.OutDir)}");
        return ExitCodes.Success;
    }

    public static RemoteSelection Select(IReadOnlyList<RemoteEntry> files, DownloadRequest request)
    {
        var full = files
            .Where(f => FilePatterns.Matches(request.FullPattern, f.Name))
            .MaxBy(Key)
            ?? throw ToolException.InvalidData(
                $"Aucun export complet trouvé sous « {request.RemotePath} » avec le motif « {request.FullPattern} ». "
                + "Lancez la commande « list » puis ajustez --full-pattern.");
        var fullDate = Key(full);
        var daily = files
            .Where(f => f != full && FilePatterns.Matches(request.DailyPattern, f.Name) && Key(f) > fullDate)
            .OrderBy(Key)
            .ThenBy(f => f.Name, StringComparer.Ordinal)
            .ToList();
        var suspensions = files.Where(f => FilePatterns.Matches(request.SuspensionsPattern, f.Name)).MaxBy(Key);
        return new RemoteSelection(full, daily, suspensions);
    }

    private static DateTime Key(RemoteEntry f) => FilePatterns.SortKey(f.Name, f.LastWriteUtc);

    private void Report(RemoteSelection s, bool dryRun)
    {
        var verb = dryRun ? "Serait téléchargé" : "Téléchargement";
        output.WriteLine($"{verb} (export complet) : {Describe(s.Full)}");
        foreach (var d in s.Daily)
        {
            output.WriteLine($"{verb} (quotidien) : {Describe(d)}");
        }

        output.WriteLine(s.Suspensions is null
            ? "Aucun fichier de suspensions trouvé (motif --suspensions-pattern)."
            : $"{verb} (suspensions) : {Describe(s.Suspensions)}");
    }

    private static void Fetch(RemoteSession session, RemoteSelection s, string outDir)
    {
        session.Download(s.Full, Path.Combine(outDir, FullDir, s.Full.Name));
        var dailyDir = Path.Combine(outDir, DailyDir);
        if (Directory.Exists(dailyDir))
        {
            Directory.Delete(dailyDir, recursive: true); // évite d'appliquer d'anciens fichiers quotidiens
        }

        foreach (var d in s.Daily)
        {
            session.Download(d, Path.Combine(dailyDir, d.Name));
        }

        var suspensionsPath = Path.Combine(outDir, SuspensionsFile);
        if (s.Suspensions is not null)
        {
            session.Download(s.Suspensions, suspensionsPath);
        }
        else if (File.Exists(suspensionsPath))
        {
            File.Delete(suspensionsPath);
        }
    }

    public static string Describe(RemoteEntry e) =>
        $"{e.FullName}  {e.Size.ToString("N0", CultureInfo.InvariantCulture)} o  "
        + e.LastWriteUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
}
