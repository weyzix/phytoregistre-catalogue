using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using PhytoRegistre.Core.Abstractions;
using PhytoRegistre.Core.Domain;
using PhytoRegistre.Core.Validation;

namespace PhytoRegistre.Core.Reference;

/// <summary>
/// Référentiels embarqués (Reference/Data/*.json) : PPP, cultures OEPP, stades BBCH.
/// Chargés une seule fois puis partagés (données immuables).
/// </summary>
public sealed class ReferenceCatalog : IReferenceCatalog
{
    public const string FytowebSearchUrl = "https://fytoweb.be/fr/guides/produits-phytopharmaceutiques";

    private const string ResourcePrefix = "PhytoRegistre.Core.Reference.Data.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly Lazy<CatalogData> Data = new(Load);

    public IReadOnlyList<Product> Products => Data.Value.Products;

    public IReadOnlyList<Crop> Crops => Data.Value.Crops;

    public IReadOnlyList<BbchStage> BbchStages => Data.Value.BbchStages;

    public Uri GetFytowebUri(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);
        var query = Uri.EscapeDataString(AuthorizationNumber.Normalize(product.AuthorizationNumber));
        return new Uri($"{FytowebSearchUrl}?search={query}");
    }

    private static CatalogData Load() => new(
        ReadItems<Product>("products.json"),
        ReadItems<Crop>("crops.json"),
        [.. ReadItems<BbchStage>("bbch.json").OrderBy(s => s.Code)]);

    private static IReadOnlyList<T> ReadItems<T>(string fileName)
    {
        var assembly = typeof(ReferenceCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourcePrefix + fileName)
            ?? throw new InvalidOperationException($"Référentiel embarqué introuvable : {fileName}.");

        var file = JsonSerializer.Deserialize<CatalogFile<T>>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"Référentiel embarqué vide : {fileName}.");
        return file.Items.AsReadOnly();
    }

    private sealed record CatalogData(
        IReadOnlyList<Product> Products,
        IReadOnlyList<Crop> Crops,
        IReadOnlyList<BbchStage> BbchStages);

    private sealed record CatalogFile<T>
    {
        [JsonPropertyName("_comment")]
        public string? Comment { get; init; }

        public List<T> Items { get; init; } = [];
    }
}
