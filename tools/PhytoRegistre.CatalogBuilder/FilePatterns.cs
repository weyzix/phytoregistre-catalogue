using System.Globalization;
using System.Text.RegularExpressions;

namespace PhytoRegistre.CatalogBuilder;

/// <summary>
/// Sélection des fichiers de l'export Fytoweb par motif (« * » et « ? », plusieurs motifs séparés par « ; »,
/// insensible à la casse). Le nommage réel des fichiers n'est connu qu'après inscription :
/// les motifs par défaut ci-dessous sont des hypothèses, modifiables en ligne de commande.
/// </summary>
internal static partial class FilePatterns
{
    public const string DefaultFull = "*full*.json;*full*.zip;*export*.json;*export*.zip";
    public const string DefaultDaily = "*daily*.json;*daily*.zip;*update*.json;*update*.zip;*delta*.json;*delta*.zip";
    public const string DefaultSuspensions = "suspension*.json";
    public const string AnyData = "*.json;*.zip";

    public static bool Matches(string patterns, string fileName) =>
        patterns.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(p => GlobToRegex(p).IsMatch(fileName));

    /// <summary>Date « yyyyMMdd », « yyyy-MM-dd » ou « yyyy_MM_dd » présente dans le nom, sinon null.</summary>
    public static DateOnly? DateFromName(string fileName)
    {
        foreach (Match m in DatePattern().Matches(fileName))
        {
            var text = m.Groups[1].Value + m.Groups[2].Value + m.Groups[3].Value;
            if (DateOnly.TryParseExact(text, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date;
            }
        }

        return null;
    }

    /// <summary>Clé de tri chronologique : date du nom, à défaut date de modification.</summary>
    public static DateTime SortKey(string fileName, DateTime lastWriteUtc) =>
        DateFromName(fileName)?.ToDateTime(TimeOnly.MinValue) ?? lastWriteUtc;

    /// <summary>Ordre des parties d'un export découpé : FULL_1, FULL_2, …, FULL_10 (numéro final, puis nom).</summary>
    public static IReadOnlyList<T> InPartOrder<T>(IEnumerable<T> items, Func<T, string> name) =>
        [.. items.OrderBy(i => PartNumber(name(i))).ThenBy(i => name(i), StringComparer.Ordinal)];

    private static int PartNumber(string fileName)
    {
        var match = PartPattern().Match(Path.GetFileNameWithoutExtension(fileName));
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
    }

    [GeneratedRegex(@"_(\d{1,3})$", RegexOptions.CultureInvariant)]
    private static partial Regex PartPattern();

    private static Regex GlobToRegex(string glob) =>
        new("^" + Regex.Escape(glob).Replace(@"\*", ".*").Replace(@"\?", ".") + "$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    [GeneratedRegex(@"(20\d{2})[-_]?(\d{2})[-_]?(\d{2})(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex DatePattern();
}
