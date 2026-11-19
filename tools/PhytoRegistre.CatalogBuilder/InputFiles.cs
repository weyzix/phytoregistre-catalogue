using System.IO.Compression;

namespace PhytoRegistre.CatalogBuilder;

/// <summary>Résolution des fichiers d'entrée locaux (fichier, dossier, archive .zip).</summary>
internal static class InputFiles
{
    /// <summary>Fichier donné, ou le plus récent (date du nom puis date de modification) d'un dossier.</summary>
    public static string ResolveFull(string path)
    {
        if (File.Exists(path))
        {
            return Path.GetFullPath(path);
        }

        if (!Directory.Exists(path))
        {
            throw ToolException.InvalidData($"Export complet introuvable : {path}");
        }

        return DataFiles(path).LastOrDefault()
            ?? throw ToolException.InvalidData($"Aucun fichier .json ou .zip dans le dossier de l'export complet : {path}");
    }

    /// <summary>Fichiers quotidiens triés, postérieurs à l'export complet lorsque les dates sont connues.</summary>
    public static IReadOnlyList<string> ResolveDaily(string? path, string fullFile)
    {
        if (path is null)
        {
            return [];
        }

        if (File.Exists(path))
        {
            return [Path.GetFullPath(path)];
        }

        if (!Directory.Exists(path))
        {
            throw ToolException.InvalidData($"Dossier des fichiers quotidiens introuvable : {path}");
        }

        var fullDate = FilePatterns.DateFromName(Path.GetFileName(fullFile));
        return
        [
            .. DataFiles(path).Where(f =>
                fullDate is null
                || FilePatterns.DateFromName(Path.GetFileName(f)) is not { } d
                || d > fullDate),
        ];
    }

    public static string? ResolveOptionalFile(string? path)
    {
        if (path is null)
        {
            return null;
        }

        return File.Exists(path) ? Path.GetFullPath(path) : throw ToolException.InvalidData($"Fichier introuvable : {path}");
    }

    /// <summary>Ouvre un .json, ou la plus grande entrée .json d'un .zip. Les ressources sont ajoutées à <paramref name="owned"/>.</summary>
    public static Stream Open(string file, List<IDisposable> owned)
    {
        if (!file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var stream = File.OpenRead(file);
            owned.Add(stream);
            return stream;
        }

        var zip = ZipFile.OpenRead(file);
        owned.Add(zip);
        var entry = zip.Entries
            .Where(e => e.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .MaxBy(e => e.Length)
            ?? throw ToolException.InvalidData($"Aucun fichier .json dans l'archive {Path.GetFileName(file)}.");
        var entryStream = entry.Open();
        owned.Add(entryStream);
        return entryStream;
    }

    private static IEnumerable<string> DataFiles(string directory) =>
        Directory.EnumerateFiles(directory)
            .Where(f => FilePatterns.Matches(FilePatterns.AnyData, Path.GetFileName(f)))
            .OrderBy(f => FilePatterns.SortKey(Path.GetFileName(f), File.GetLastWriteTimeUtc(f)))
            .ThenBy(f => f, StringComparer.Ordinal);
}
