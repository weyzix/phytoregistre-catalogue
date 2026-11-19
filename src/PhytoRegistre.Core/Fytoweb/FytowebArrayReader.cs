using System.Text.Json;

namespace PhytoRegistre.Core.Fytoweb;

/// <summary>
/// Lit un tableau JSON racine élément par élément, sans charger tout le fichier en mémoire
/// (les exports réels dépassent 100 Mo). Chaque élément est matérialisé seul.
/// </summary>
internal static class FytowebArrayReader
{
    /// <exception cref="FormatException">Racine absente, autre qu'un tableau, ou JSON tronqué / mal formé.</exception>
    public static IEnumerable<JsonElement> ReadElements(Stream stream, string fileLabel)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var enumerator = JsonSerializer
            .DeserializeAsyncEnumerable<JsonElement>(stream)
            .ToBlockingEnumerable()
            .GetEnumerator();
        while (MoveNext(enumerator, fileLabel))
        {
            yield return enumerator.Current;
        }
    }

    private static bool MoveNext(IEnumerator<JsonElement> enumerator, string fileLabel)
    {
        try
        {
            return enumerator.MoveNext();
        }
        catch (JsonException ex)
        {
            throw new FormatException(
                $"{fileLabel} : le contenu doit être un tableau JSON valide (ligne {ex.LineNumber + 1}).", ex);
        }
    }
}
