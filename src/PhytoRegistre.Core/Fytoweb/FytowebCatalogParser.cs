using PhytoRegistre.Core.Abstractions;
using PhytoRegistre.Core.Domain;

namespace PhytoRegistre.Core.Fytoweb;

/// <summary>Catalogue produit et statistiques de transformation.</summary>
/// <param name="SkippedElements">Éléments ignorés (autorisations ou suspensions inexploitables).</param>
/// <param name="DailyUpdatesApplied">Autorisations lues dans les fichiers quotidiens (ajouts + remplacements).</param>
/// <param name="SuspensionsApplied">Produits du catalogue marqués suspendus.</param>
public sealed record FytowebParseResult(
    ProductCatalog Catalog,
    int SkippedElements,
    int DailyUpdatesApplied,
    int SuspensionsApplied);

/// <summary>
/// Transforme l'export JSON Fytoweb (export complet + fichiers quotidiens + suspensions) en catalogue.
/// Lecture en flux, élément par élément. Les fichiers quotidiens sont appliqués dans l'ordre fourni :
/// une autorisation de même n° remplace la précédente (aucun marqueur de suppression n'est documenté :
/// un retrait arrive sous forme de décision INT). Les éléments inexploitables sont comptés et ignorés.
/// </summary>
public sealed class FytowebCatalogParser : IFytowebCatalogParser
{
    public const string SourceName = "Fytoweb";

    public ProductCatalog Parse(
        Stream fullExport,
        IReadOnlyList<Stream> dailyUpdates,
        Stream? suspensions,
        DateTimeOffset generatedAt) =>
        ParseWithReport(fullExport, dailyUpdates, suspensions, generatedAt).Catalog;

    /// <exception cref="FormatException">Racine d'un fichier autre qu'un tableau JSON, ou JSON mal formé.</exception>
    public FytowebParseResult ParseWithReport(
        Stream fullExport,
        IReadOnlyList<Stream> dailyUpdates,
        Stream? suspensions,
        DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(fullExport);
        ArgumentNullException.ThrowIfNull(dailyUpdates);

        var products = new Dictionary<string, Product>(StringComparer.Ordinal);
        var skipped = Load(products, fullExport, "Export complet");
        var dailyApplied = 0;
        for (var i = 0; i < dailyUpdates.Count; i++)
        {
            skipped += Load(products, dailyUpdates[i], $"Fichier quotidien n° {i + 1}", count => dailyApplied += count);
        }

        var suspended = 0;
        if (suspensions is not null)
        {
            var (list, badSuspensions) = FytowebSuspensions.Read(suspensions);
            skipped += badSuspensions;
            suspended = ApplySuspensions(products, list);
        }

        var catalog = new ProductCatalog
        {
            Version = generatedAt.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            GeneratedAt = generatedAt,
            Products = [.. products.Values.OrderBy(p => p.CommercialName, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.AuthorizationNumber, StringComparer.Ordinal)],
        };
        return new FytowebParseResult(catalog, skipped, dailyApplied, suspended);
    }

    /// <returns>Nombre d'éléments ignorés.</returns>
    private static int Load(Dictionary<string, Product> products, Stream stream, string label, Action<int>? onApplied = null)
    {
        var skipped = 0;
        var applied = 0;
        foreach (var element in FytowebArrayReader.ReadElements(stream, label))
        {
            var product = FytowebProductMapper.TryMap(element);
            if (product is null)
            {
                skipped++;
                continue;
            }

            products[FytowebProductMapper.Key(product.AuthorizationNumber)] = product;
            applied++;
        }

        onApplied?.Invoke(applied);
        return skipped;
    }

    /// <summary>La suspension la plus récente (date de début) l'emporte pour un même produit.</summary>
    private static int ApplySuspensions(Dictionary<string, Product> products, IReadOnlyList<FytowebSuspension> suspensions)
    {
        var latest = suspensions
            .SelectMany(s => s.AuthorizationNumbers.Select(n => (Key: FytowebProductMapper.Key(n), Suspension: s)))
            .GroupBy(x => x.Key)
            .Select(g => g.MaxBy(x => x.Suspension.From))
            .Where(x => products.ContainsKey(x.Key))
            .ToList();
        foreach (var (key, suspension) in latest)
        {
            products[key] = products[key] with { SuspendedFrom = suspension.From, SuspendedUntil = suspension.Until };
        }

        return latest.Count;
    }
}
