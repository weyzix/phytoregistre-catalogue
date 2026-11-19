using System.Globalization;
using System.Text.Json;

namespace PhytoRegistre.Core.Fytoweb;

/// <summary>Lecture tolérante des champs de l'export Fytoweb (champ absent ou mal typé → null).</summary>
internal static class FytowebJson
{
    public const string DateFormat = "dd-MM-yyyy";

    public static JsonElement? Child(this JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            && value.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)
            ? value
            : null;

    public static string? Text(this JsonElement element, string name)
    {
        var value = element.Child(name);
        if (value is not { ValueKind: JsonValueKind.String } v)
        {
            return null;
        }

        var text = v.GetString()?.Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    public static decimal? Decimal(this JsonElement element, string name)
    {
        var value = element.Child(name);
        if (value is { ValueKind: JsonValueKind.Number } n && n.TryGetDecimal(out var d))
        {
            return d;
        }

        return value is { ValueKind: JsonValueKind.String } s
            && decimal.TryParse(s.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;
    }

    public static int? Int(this JsonElement element, string name)
    {
        var value = element.Decimal(name);
        return value is { } d && d == decimal.Truncate(d) && d is >= int.MinValue and <= int.MaxValue ? (int)d : null;
    }

    public static bool Bool(this JsonElement element, string name) =>
        element.Child(name) is { ValueKind: JsonValueKind.True };

    /// <summary>Date « dd-MM-yyyy » ; null si absente ou invalide.</summary>
    public static DateOnly? Date(this JsonElement element, string name) => ParseDate(element.Text(name));

    public static DateOnly? ParseDate(string? text) =>
        DateOnly.TryParseExact(text, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    /// <summary>Éléments objets d'un tableau (les éléments d'un autre type sont ignorés).</summary>
    public static IEnumerable<JsonElement> Objects(this JsonElement element, string name)
    {
        if (element.Child(name) is not { ValueKind: JsonValueKind.Array } array)
        {
            return [];
        }

        return array.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object);
    }

    /// <summary>Navigation objet.objet ; élément vide si un maillon manque.</summary>
    public static JsonElement Path(this JsonElement element, params string[] names)
    {
        var current = element;
        foreach (var name in names)
        {
            if (current.Child(name) is not { } next)
            {
                return default;
            }

            current = next;
        }

        return current;
    }
}
