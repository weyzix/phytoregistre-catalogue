using System.IO.Compression;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using PhytoRegistre.Core.Abstractions;
using PhytoRegistre.Core.Domain;

namespace PhytoRegistre.Core.Catalog;

/// <summary>
/// Catalogue public : JSON compact (camelCase, énumérations en texte, sans null) compressé gzip ;
/// manifeste : JSON indenté. Toute entrée invalide lève <see cref="FormatException"/>.
/// </summary>
public sealed partial class CatalogSerializer : ICatalogSerializer
{
    /// <summary>Taille décompressée maximale acceptée : 200 Mo.</summary>
    public const long DefaultMaxDecompressedBytes = 200L * 1024 * 1024;

    private const int MaxManifestLength = 64 * 1024;

    // Échappement minimal (accents lisibles, fichier plus petit) : contenu jamais injecté dans du HTML.
    private static readonly CatalogJsonContext Compact = new(new JsonSerializerOptions(CatalogJsonContext.Default.Options)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });

    private static readonly CatalogJsonContext Indented = new(new JsonSerializerOptions(CatalogJsonContext.Default.Options)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = true,
    });

    private readonly long _maxDecompressedBytes;

    public CatalogSerializer(long maxDecompressedBytes = DefaultMaxDecompressedBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDecompressedBytes);
        _maxDecompressedBytes = maxDecompressedBytes;
    }

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Hex();

    public byte[] SerializeCatalog(ProductCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            JsonSerializer.Serialize(gzip, catalog, Compact.ProductCatalog);
        }

        return buffer.ToArray();
    }

    public ProductCatalog DeserializeCatalog(Stream gzipJson)
    {
        ArgumentNullException.ThrowIfNull(gzipJson);
        ProductCatalog? catalog;
        try
        {
            using var gzip = new GZipStream(gzipJson, CompressionMode.Decompress, leaveOpen: true);
            using var bounded = new BoundedReadStream(gzip, _maxDecompressedBytes);
            catalog = JsonSerializer.Deserialize(bounded, Compact.ProductCatalog);
        }
        catch (SizeLimitExceededException ex)
        {
            throw new FormatException($"Catalogue trop volumineux (plus de {_maxDecompressedBytes / (1024 * 1024)} Mo décompressés).", ex);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or NotSupportedException)
        {
            throw new FormatException("Catalogue invalide (fichier gzip/JSON illisible).", ex);
        }

        return Validate(catalog);
    }

    public string SerializeManifest(CatalogManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return JsonSerializer.Serialize(manifest, Indented.CatalogManifest);
    }

    public CatalogManifest DeserializeManifest(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaxManifestLength)
        {
            throw new FormatException("Manifeste du catalogue vide ou trop volumineux.");
        }

        CatalogManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize(json, Compact.CatalogManifest);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new FormatException("Manifeste du catalogue invalide (JSON mal formé).", ex);
        }

        return Validate(manifest);
    }

    private static ProductCatalog Validate(ProductCatalog? catalog)
    {
        if (catalog is null)
        {
            throw new FormatException("Catalogue vide.");
        }

        EnsureSchema(catalog.SchemaVersion);
        if (string.IsNullOrWhiteSpace(catalog.Version) || catalog.Products is null)
        {
            throw new FormatException("Catalogue incomplet (version ou liste de produits manquante).");
        }

        if (catalog.Products.Any(p => !IsValid(p)))
        {
            throw new FormatException("Catalogue invalide : produit sans nom ou sans n° d'autorisation.");
        }

        return catalog;
    }

    private static bool IsValid(Product? p) =>
        p is not null
        && !string.IsNullOrWhiteSpace(p.CommercialName)
        && !string.IsNullOrWhiteSpace(p.AuthorizationNumber)
        && p.Usages is not null
        && p.Usages.All(u => u is not null && !string.IsNullOrWhiteSpace(u.CropNameFr));

    private static CatalogManifest Validate(CatalogManifest? manifest)
    {
        if (manifest is null)
        {
            throw new FormatException("Manifeste du catalogue vide.");
        }

        EnsureSchema(manifest.SchemaVersion);
        if (string.IsNullOrWhiteSpace(manifest.Version)
            || string.IsNullOrWhiteSpace(manifest.CatalogUrl)
            || manifest.Sha256 is null
            || !Sha256Hex().IsMatch(manifest.Sha256))
        {
            throw new FormatException("Manifeste du catalogue incomplet (version, URL ou empreinte SHA-256 invalide).");
        }

        return manifest;
    }

    private static void EnsureSchema(int schemaVersion)
    {
        if (schemaVersion != ProductCatalog.CurrentSchemaVersion)
        {
            throw new FormatException(
                $"Version de schéma du catalogue non prise en charge ({schemaVersion}) : mettez l'application à jour.");
        }
    }
}
