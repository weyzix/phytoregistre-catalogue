using PhytoRegistre.Core.Domain;

namespace PhytoRegistre.Core.Abstractions;

/// <summary>
/// Catalogue de produits distribué publiquement aux téléphones (dérivé de l'export Fytoweb,
/// SPF Santé publique, licence CC BY 4.0). Aucun identifiant FTP n'est jamais embarqué dans l'app :
/// seul l'éditeur télécharge l'export officiel, construit ce catalogue et le publie en HTTPS.
/// </summary>
public sealed record ProductCatalog
{
    public const int CurrentSchemaVersion = 1;
    public const string DefaultAttribution =
        "Source : SPF Santé publique – Fytoweb (fytoweb.be), licence CC BY 4.0. Données transformées.";

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    /// <summary>Version lisible et triable, ex. "2026-11-19".</summary>
    public required string Version { get; init; }

    public DateTimeOffset GeneratedAt { get; init; }

    public string Attribution { get; init; } = DefaultAttribution;

    public IReadOnlyList<Product> Products { get; init; } = [];
}

/// <summary>Petit fichier lu en premier par l'app pour savoir s'il faut télécharger le catalogue.</summary>
public sealed record CatalogManifest
{
    public int SchemaVersion { get; init; } = ProductCatalog.CurrentSchemaVersion;
    public required string Version { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
    public int ProductCount { get; init; }

    /// <summary>SHA-256 (hex minuscule) du fichier catalogue compressé.</summary>
    public required string Sha256 { get; init; }

    /// <summary>URL HTTPS absolue ou relative au manifeste du fichier catalogue (.json.gz).</summary>
    public required string CatalogUrl { get; init; }

    public long CatalogSizeBytes { get; init; }

    public string Attribution { get; init; } = ProductCatalog.DefaultAttribution;
}

/// <summary>Transforme l'export JSON brut de Fytoweb en <see cref="ProductCatalog"/>.</summary>
public interface IFytowebCatalogParser
{
    /// <param name="fullExport">Export mensuel complet (tableau JSON d'autorisations).</param>
    /// <param name="dailyUpdates">Fichiers quotidiens de modifications, dans l'ordre chronologique.</param>
    /// <param name="suspensions">Fichier suspensions.json, s'il existe.</param>
    ProductCatalog Parse(
        Stream fullExport,
        IReadOnlyList<Stream> dailyUpdates,
        Stream? suspensions,
        DateTimeOffset generatedAt);
}

/// <summary>Sérialisation compacte (JSON + gzip) du catalogue et du manifeste.</summary>
public interface ICatalogSerializer
{
    byte[] SerializeCatalog(ProductCatalog catalog);

    /// <exception cref="FormatException">Contenu invalide ou version de schéma non supportée.</exception>
    ProductCatalog DeserializeCatalog(Stream gzipJson);

    string SerializeManifest(CatalogManifest manifest);

    /// <exception cref="FormatException">Manifeste invalide.</exception>
    CatalogManifest DeserializeManifest(string json);
}

/// <summary>Contrôles propres au produit choisi : autorisation à la date, dose maximale par culture.</summary>
public interface IProductComplianceChecker
{
    /// <summary>
    /// PRODUCT_WITHDRAWN (Error si la date dépasse la tolérance d'usage), PRODUCT_SUSPENDED (Error),
    /// PRODUCT_PROVISIONAL (Info), PRODUCT_NOT_PROFESSIONAL (Warning), DOSE_ABOVE_MAX (Warning, culture reconnue),
    /// CROP_NOT_IN_AUTHORIZATION (Info, culture non trouvée dans les usages autorisés).
    /// </summary>
    IReadOnlyList<ValidationIssue> Check(TreatmentRecord record, Product product);
}

/// <summary>Identifiant stable d'un produit Fytoweb, pour que les fiches restent liées après mise à jour.</summary>
public static class ProductIds
{
    /// <summary>Guid déterministe (SHA-1) à partir du n° d'autorisation normalisé.</summary>
    public static Guid FromAuthorizationNumber(string authorizationNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizationNumber);
        var normalized = "fytoweb:" + authorizationNumber.Trim().ToUpperInvariant();
        var hash = System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(normalized));
        var bytes = hash.AsSpan(0, 16).ToArray();
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50); // version 5
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // variante RFC 4122
        return new Guid(bytes);
    }
}
