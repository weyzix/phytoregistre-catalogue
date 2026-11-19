using System.Globalization;
using System.Text.Json;
using PhytoRegistre.Core.Abstractions;
using PhytoRegistre.Core.Domain;

namespace PhytoRegistre.Core.Weather;

/// <summary>
/// Météo horaire au moment du traitement via Open-Meteo (https://open-meteo.com, sans clé d'API).
/// Dates récentes (≤ 90 jours) et prévisions (≤ 15 jours) : API « forecast » avec past_days ;
/// dates plus anciennes : API « archive » (ERA5). Ne lève jamais d'exception réseau : renvoie null.
/// </summary>
public sealed class OpenMeteoWeatherService : IWeatherService
{
    public const string SourceName = "Open-Meteo";

    private const string ForecastEndpoint = "https://api.open-meteo.com/v1/forecast";
    private const string ArchiveEndpoint = "https://archive-api.open-meteo.com/v1/archive";
    private const string HourlyVariables = "temperature_2m,relative_humidity_2m,wind_speed_10m,precipitation";
    private const string TimeZone = "Europe%2FBrussels";

    /// <summary>Au-delà, l'API forecast ne fournit plus l'historique (max 92 j) → archive.</summary>
    private const int MaxForecastPastDays = 90;

    /// <summary>L'API forecast fournit au plus 16 jours de prévision (aujourd'hui inclus).</summary>
    private const int MaxForecastDays = 16;

    private readonly HttpClient _httpClient;
    private readonly TimeProvider _timeProvider;

    public OpenMeteoWeatherService(HttpClient httpClient)
        : this(httpClient, TimeProvider.System)
    {
    }

    /// <param name="timeProvider">Horloge (date du jour), injectable pour les tests.</param>
    public OpenMeteoWeatherService(HttpClient httpClient, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _httpClient = httpClient;
        _timeProvider = timeProvider;
    }

    public async Task<WeatherConditions?> GetConditionsAsync(
        GeoPoint location, DateTime localDateTime, CancellationToken ct = default)
    {
        var uri = BuildUri(location, localDateTime);
        if (uri is null)
        {
            return null;
        }

        try
        {
            using var response = await _httpClient.GetAsync(uri, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            return OpenMeteoResponseParser.Parse(document.RootElement, localDateTime, SourceName);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return null; // timeout HttpClient : pas une annulation demandée par l'appelant
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException
            or IOException)
        {
            return null;
        }
    }

    /// <summary>Construit l'URL, ou null si la position est invalide ou la date hors couverture.</summary>
    internal Uri? BuildUri(GeoPoint location, DateTime localDateTime)
    {
        if (!IsValid(location))
        {
            return null;
        }

        var date = DateOnly.FromDateTime(localDateTime);
        var today = DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
        var daysAgo = today.DayNumber - date.DayNumber;
        var common = string.Create(
            CultureInfo.InvariantCulture,
            $"latitude={location.Latitude}&longitude={location.Longitude}&hourly={HourlyVariables}&timezone={TimeZone}");

        if (daysAgo > MaxForecastPastDays)
        {
            var day = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return new Uri($"{ArchiveEndpoint}?{common}&start_date={day}&end_date={day}");
        }

        // +1 jour de marge (décalage éventuel entre fuseau de l'appareil et Europe/Brussels)
        var pastDays = daysAgo >= 0 ? daysAgo + 1 : 0;
        var forecastDays = daysAgo >= 0 ? 1 : 1 - daysAgo;
        return forecastDays > MaxForecastDays
            ? null
            : new Uri(string.Create(
                CultureInfo.InvariantCulture,
                $"{ForecastEndpoint}?{common}&past_days={pastDays}&forecast_days={forecastDays}"));
    }

    private static bool IsValid(GeoPoint p) =>
        double.IsFinite(p.Latitude) && Math.Abs(p.Latitude) <= 90
        && double.IsFinite(p.Longitude) && Math.Abs(p.Longitude) <= 180;
}
