using System.Globalization;
using PhytoRegistre.CatalogBuilder.Sftp;

namespace PhytoRegistre.CatalogBuilder;

/// <summary>Fichiers retenus sur le serveur.</summary>
/// <param name="FullParts">Parties de l'export complet le plus récent (ex. 2026_11_01_FULL_1..5), dans l'ordre.</param>
internal sealed record RemoteSelection(IReadOnlyList<RemoteEntry> FullParts, IReadOnlyList<RemoteEntry> Daily, RemoteEntry? Suspensions);

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
        var candidates = files.Where(f => FilePatterns.Matches(request.FullPattern, f.Name)).ToList();
        var latest = candidates.MaxBy(Key)
            ?? throw ToolException.InvalidData(
                $"Aucun export complet trouvé sous « {request.RemotePath} » avec le motif « {request.FullPattern} ». "
                + "Lancez la commande « list » puis ajustez --full-pattern.");
        var fullDate = Key(latest);
        var parts = FilePatterns.DateFromName(latest.Name) is null
            ? [latest]
            : FilePatterns.InPartOrder(candidates.Where(f => Key(f) == fullDate), f => f.Name);
        var daily = files
            .Where(f => !parts.Contains(f) && FilePatterns.Matches(request.DailyPattern, f.Name) && Key(f) > fullDate)
            .OrderBy(Key)
            .ThenBy(f => f.Name, StringComparer.Ordinal)
            .ToList();
        var suspensions = files.Where(f => FilePatterns.Matches(request.SuspensionsPattern, f.Name)).MaxBy(Key);
        return new RemoteSelection(parts, daily, suspensions);
    }

    private static DateTime Key(RemoteEntry f) => FilePatterns.SortKey(f.Name, f.LastWriteUtc);

    private void Report(RemoteSelection s, bool dryRun)
    {
        var verb = dryRun ? "Serait téléchargé" : "Téléchargement";
        foreach (var part in s.FullParts)
        {
            output.WriteLine($"{verb} (export complet) : {Describe(part)}");
        }
        foreach (var d in s.Daily)
        {
            output.WriteLine($"{verb} (quotidien) : {Describe(d)}");
        }

        output.WriteLine(s.Suspensions is null
            ? "Aucun fichier de suspensions trouvé (motif --suspensions-pattern)."
            : $"{verb} (suspensions) : {Describe(s.Suspensions)}");
    }

    private void Fetch(RemoteSession session, RemoteSelection s, string outDir)
    {
        FetchFullParts(session, s.FullParts, Path.Combine(outDir, FullDir));
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

    /// <summary>
    /// Garde en cache les parties déjà téléchargées (même nom, même taille) : l'export complet (~500 Mo)
    /// ne change qu'une fois par mois. Les parties d'un ancien export sont supprimées.
    /// </summary>
    private void FetchFullParts(RemoteSession session, IReadOnlyList<RemoteEntry> parts, string fullDir)
    {
        Directory.CreateDirectory(fullDir);
        var keep = parts.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var stale in Directory.EnumerateFiles(fullDir).Where(f => !keep.Contains(Path.GetFileName(f))))
        {
            File.Delete(stale);
        }

        foreach (var part in parts)
        {
            var local = new FileInfo(Path.Combine(fullDir, part.Name));
            if (local.Exists && local.Length == part.Size)
            {
                output.WriteLine($"Déjà présent, non retéléchargé : {part.Name}");
                continue;
            }

            session.Download(part, local.FullName);
        }
    }

    public static string Describe(RemoteEntry e) =>
        $"{e.FullName}  {e.Size.ToString("N0", CultureInfo.InvariantCulture)} o  "
        + e.LastWriteUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
}
