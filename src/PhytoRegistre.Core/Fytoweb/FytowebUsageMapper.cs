using System.Text.Json;
using PhytoRegistre.Core.Domain;

namespace PhytoRegistre.Core.Fytoweb;

/// <summary>pestUse.pestCrops[] → <see cref="ProductUsage"/> (une entrée par culture/usage).</summary>
internal static class FytowebUsageMapper
{
    /// <summary>Code de type de délai « délai avant récolte » (USAGECROP_intervalTypeCode).</summary>
    public const string PreHarvestIntervalType = "WO";

    private const string DaysUnit = "DAYS";

    public static IReadOnlyList<ProductUsage> Map(JsonElement authorisation) =>
        [.. Crops(authorisation).Select(MapCrop).OfType<ProductUsage>()];

    /// <summary>Toutes les unités de dose des ennemis visés (pour déduire l'unité du produit).</summary>
    public static IEnumerable<string?> DoseUnits(JsonElement authorisation) =>
        Crops(authorisation)
            .SelectMany(c => c.Objects("pestCropEnemies"))
            .Select(e => e.Text("USAGECROPPEST_applicationRateUnitFr"));

    /// <summary>Usage exigeant un stade : texte « BBCH » ou « pas de délai, voir stade » avec un stade renseigné.</summary>
    public static bool RequiresBbch(JsonElement authorisation) =>
        Crops(authorisation).Any(c =>
        {
            var stage = c.Text("USAGECROP_growthStageFr");
            return FytowebHeuristics.MentionsBbch(stage)
                || (c.Bool("USAGECROP_intervalSeeGrowthStage") && stage is not null);
        });

    private static IEnumerable<JsonElement> Crops(JsonElement authorisation) =>
        authorisation.Path("pestUse").Objects("pestCrops");

    private static ProductUsage? MapCrop(JsonElement crop)
    {
        var name = crop.Text("CROP_cropNameFr");
        if (name is null)
        {
            return null;
        }

        var enemies = crop.Objects("pestCropEnemies").ToList();
        var unit = MainUnit(enemies);
        var doses = enemies.Where(e => e.Text("USAGECROPPEST_applicationRateUnitFr") == unit).ToList();
        return new ProductUsage
        {
            CropNameFr = name,
            CropScientificName = crop.Text("CROP_scientificName"),
            TargetPests = [.. enemies.Select(e => e.Text("PEST_NameFr")).OfType<string>().Distinct()],
            DoseMin = Min(doses.Select(e => e.Decimal("USAGECROPPEST_applicationRateMin"))),
            DoseMax = Max(doses.Select(e => e.Decimal("USAGECROPPEST_applicationRateMax") ?? e.Decimal("USAGECROPPEST_applicationRateMin"))),
            DoseUnit = unit,
            GrowthStage = crop.Text("USAGECROP_growthStageFr"),
            MaxApplications = crop.Int("USAGECROP_maxApplications"),
            PreHarvestIntervalDays = PreHarvestInterval(crop),
            Remarks = crop.Text("USAGECROP_remarksFr"),
        };
    }

    /// <summary>Unité la plus fréquente parmi les ennemis (premier rencontré en cas d'égalité).</summary>
    private static string? MainUnit(IEnumerable<JsonElement> enemies) =>
        enemies
            .Select(e => e.Text("USAGECROPPEST_applicationRateUnitFr"))
            .OfType<string>()
            .GroupBy(u => u)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

    private static int? PreHarvestInterval(JsonElement crop)
    {
        var type = crop.Text("USAGECROP_intervalTypeCode");
        var unit = crop.Text("USAGECROP_intervalUnitCode");
        var isDays = unit is null || string.Equals(unit, DaysUnit, StringComparison.OrdinalIgnoreCase);
        return string.Equals(type, PreHarvestIntervalType, StringComparison.OrdinalIgnoreCase) && isDays
            ? crop.Int("USAGECROP_interval")
            : null;
    }

    private static decimal? Min(IEnumerable<decimal?> values)
    {
        var list = values.OfType<decimal>().ToList();
        return list.Count == 0 ? null : list.Min();
    }

    private static decimal? Max(IEnumerable<decimal?> values)
    {
        var list = values.OfType<decimal>().ToList();
        return list.Count == 0 ? null : list.Max();
    }
}
