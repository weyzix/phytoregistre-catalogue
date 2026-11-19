using System.Globalization;
using System.Text.Json;
using PhytoRegistre.Core.Domain;

namespace PhytoRegistre.Core.Weather;

/// <summary>
/// Extrait l'heure correspondante d'une réponse Open-Meteo « hourly » :
/// { "hourly": { "time": ["yyyy-MM-ddTHH:mm", ...], "temperature_2m": [...], ... } }.
/// </summary>
internal static class OpenMeteoResponseParser
{
    private const string TimeFormat = "yyyy-MM-dd'T'HH:mm";

    /// <returns>null si la structure est inattendue, l'heure absente ou toutes les valeurs nulles.</returns>
    public static WeatherConditions? Parse(JsonElement root, DateTime localDateTime, string source)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("hourly", out var hourly)
            || hourly.ValueKind != JsonValueKind.Object
            || !hourly.TryGetProperty("time", out var times)
            || times.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var wanted = localDateTime.ToString("yyyy-MM-dd'T'HH':00'", CultureInfo.InvariantCulture);
        var index = IndexOf(times, wanted);
        if (index < 0)
        {
            return null;
        }

        var conditions = new WeatherConditions
        {
            TemperatureC = ValueAt(hourly, "temperature_2m", index),
            RelativeHumidityPct = ValueAt(hourly, "relative_humidity_2m", index),
            WindSpeedKmh = ValueAt(hourly, "wind_speed_10m", index),
            PrecipitationMm = ValueAt(hourly, "precipitation", index),
            Source = source,
        };

        return conditions is { TemperatureC: null, RelativeHumidityPct: null, WindSpeedKmh: null, PrecipitationMm: null }
            ? null
            : conditions;
    }

    private static int IndexOf(JsonElement times, string wanted)
    {
        var i = 0;
        foreach (var item in times.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String
                && DateTime.TryParseExact(item.GetString(), TimeFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsed)
                && parsed.ToString("yyyy-MM-dd'T'HH':00'", CultureInfo.InvariantCulture) == wanted)
            {
                return i;
            }

            i++;
        }

        return -1;
    }

    private static double? ValueAt(JsonElement hourly, string name, int index)
    {
        if (!hourly.TryGetProperty(name, out var values)
            || values.ValueKind != JsonValueKind.Array
            || index >= values.GetArrayLength())
        {
            return null;
        }

        var value = values[index];
        return value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var d) ? d : null;
    }
}
