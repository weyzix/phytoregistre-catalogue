using System.Text.Json.Serialization;
using PhytoRegistre.Core.Abstractions;

namespace PhytoRegistre.Core.Catalog;

/// <summary>
/// Sérialisation générée à la compilation (compatible trimming/AOT Android) du catalogue public :
/// camelCase, énumérations en texte, valeurs nulles omises.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    AllowTrailingCommas = false)]
[JsonSerializable(typeof(ProductCatalog))]
[JsonSerializable(typeof(CatalogManifest))]
internal sealed partial class CatalogJsonContext : JsonSerializerContext;
