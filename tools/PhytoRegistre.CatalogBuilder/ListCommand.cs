using PhytoRegistre.CatalogBuilder.Sftp;

namespace PhytoRegistre.CatalogBuilder;

/// <summary>
/// Affiche l'arborescence distante (noms, tailles, dates) : permet de découvrir le nommage réel
/// des fichiers lors de la première connexion, puis de régler --full-pattern / --daily-pattern.
/// </summary>
internal sealed class ListCommand(ISftpSourceFactory factory, TextWriter output)
{
    public int Run(SftpSettings settings, string path)
    {
        using var session = RemoteSession.Open(factory, settings, output);
        var entries = session.Walk(path);
        foreach (var entry in entries)
        {
            output.WriteLine(entry.IsDirectory ? $"{entry.FullName}/" : DownloadCommand.Describe(entry));
        }

        output.WriteLine($"{entries.Count(e => !e.IsDirectory)} fichier(s), {entries.Count(e => e.IsDirectory)} dossier(s).");
        return ExitCodes.Success;
    }
}
