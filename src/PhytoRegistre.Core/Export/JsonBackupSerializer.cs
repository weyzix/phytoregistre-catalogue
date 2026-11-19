using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using PhytoRegistre.Core.Abstractions;

namespace PhytoRegistre.Core.Export;

/// <summary>
/// Sauvegarde complète en JSON (System.Text.Json) : camelCase, énumérations en texte,
/// DateOnly/TimeOnly ISO 8601, indenté. Les propriétés calculées (ex. totalQuantity)
/// sont écrites pour lisibilité mais ignorées à la relecture.
/// </summary>
public sealed class JsonBackupSerializer : IBackupSerializer
{
    /// <summary>Version de schéma la plus récente prise en charge.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>Taille maximale acceptée à la relecture : 50 Mo (en caractères).</summary>
    public const int MaxInputLength = 50 * 1024 * 1024;

    private static readonly JsonSerializerOptions Options = CreateOptions();

    public string Serialize(BackupSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return JsonSerializer.Serialize(snapshot, Options);
    }

    public BackupSnapshot Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new FormatException("Fichier de sauvegarde vide.");
        }

        if (json.Length > MaxInputLength)
        {
            throw new FormatException("Fichier de sauvegarde trop volumineux (plus de 50 Mo).");
        }

        BackupSnapshot? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<BackupSnapshot>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new FormatException("Fichier de sauvegarde invalide (JSON mal formé).", ex);
        }
        catch (NotSupportedException ex)
        {
            throw new FormatException("Fichier de sauvegarde invalide (contenu non supporté).", ex);
        }

        return Validate(snapshot);
    }

    private static BackupSnapshot Validate(BackupSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            throw new FormatException("Fichier de sauvegarde vide (null).");
        }

        if (snapshot.SchemaVersion is < 1 or > CurrentSchemaVersion)
        {
            throw new FormatException(
                $"Version de sauvegarde {snapshot.SchemaVersion} non supportée (max {CurrentSchemaVersion}).");
        }

        if (snapshot.Treatments is null || snapshot.Parcels is null || snapshot.Products is null
            || snapshot.Crops is null || snapshot.StockMovements is null)
        {
            throw new FormatException("Fichier de sauvegarde invalide (liste manquante).");
        }

        if (snapshot.Treatments.Any(t => t is null || t.Id == Guid.Empty)
            || snapshot.Parcels.Any(p => p is null || p.Id == Guid.Empty)
            || snapshot.Products.Any(p => p is null || p.Id == Guid.Empty)
            || snapshot.StockMovements.Any(m => m is null || m.Id == Guid.Empty)
            || snapshot.Crops.Any(c => c is null))
        {
            throw new FormatException("Fichier de sauvegarde invalide (élément vide ou identifiant manquant).");
        }

        return snapshot;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        options.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
        options.MakeReadOnly();
        return options;
    }
}
