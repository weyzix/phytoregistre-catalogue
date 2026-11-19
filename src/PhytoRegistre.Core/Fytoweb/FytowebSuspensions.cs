using System.Text.Json;

namespace PhytoRegistre.Core.Fytoweb;

/// <summary>Une suspension (suspensions.json) : n° d'autorisation concernés et période.</summary>
public sealed record FytowebSuspension(IReadOnlyList<string> AuthorizationNumbers, DateOnly From, DateOnly? Until);

/// <summary>Lecture de suspensions.json (listAuthorizationNumber « 10485P/B, 8829P/B », dateFrom, dateTo?).</summary>
internal static class FytowebSuspensions
{
    private static readonly char[] Separators = [',', ';'];

    /// <returns>Suspensions valides et nombre d'éléments ignorés (mal formés).</returns>
    public static (IReadOnlyList<FytowebSuspension> Suspensions, int Skipped) Read(Stream stream)
    {
        var result = new List<FytowebSuspension>();
        var skipped = 0;
        foreach (var element in FytowebArrayReader.ReadElements(stream, "suspensions.json"))
        {
            var suspension = TryMap(element);
            if (suspension is null)
            {
                skipped++;
            }
            else
            {
                result.Add(suspension);
            }
        }

        return (result, skipped);
    }

    private static FytowebSuspension? TryMap(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || element.Date("dateFrom") is not { } from)
        {
            return null;
        }

        var numbers = (element.Text("listAuthorizationNumber") ?? "")
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return numbers.Length == 0 ? null : new FytowebSuspension(numbers, from, element.Date("dateTo"));
    }
}
