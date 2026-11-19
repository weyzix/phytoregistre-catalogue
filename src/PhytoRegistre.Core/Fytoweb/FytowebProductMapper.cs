using System.Globalization;
using System.Text.Json;
using PhytoRegistre.Core.Abstractions;
using PhytoRegistre.Core.Domain;

namespace PhytoRegistre.Core.Fytoweb;

/// <summary>Une autorisation de l'export Fytoweb → <see cref="Product"/> (null si inexploitable).</summary>
internal static class FytowebProductMapper
{
    private const string ActiveSubstanceRole = "Substance active";
    private const string ProfessionalGroup = "PRO";
    private const string OrganicMarker = "agriculture biologique";

    public static Product? TryMap(JsonElement auth)
    {
        if (auth.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var number = auth.Text("AUTH_NUMBER");
        var product = auth.Path("pestProduct");
        var name = product.Text("PRODUCT_name");
        if (number is null || name is null)
        {
            return null;
        }

        var formulationCode = product.Text("PRODUCT_formulationTypeCode");
        var spe8Texts = Spe8Texts(auth).ToList();
        return new Product
        {
            Id = ProductIds.FromAuthorizationNumber(number),
            CommercialName = name,
            AuthorizationNumber = number,
            Source = FytowebCatalogParser.SourceName,
            Status = FytowebHeuristics.StatusFromDecision(auth.Text("AUTH_DECISION_decisionCode")),
            AuthorizedUntil = auth.Date("AUTH_dateTo"),
            UseToleratedUntil = auth.Date("useToleratedTo"),
            Unit = FytowebHeuristics.ProductUnit(FytowebUsageMapper.DoseUnits(auth), formulationCode),
            IsSpe8 = spe8Texts.Count > 0,
            HasTimeRestriction = spe8Texts.Any(FytowebHeuristics.MentionsTimeWindow),
            RequiresBbchStage = FytowebUsageMapper.RequiresBbch(auth),
            ActiveSubstances = ActiveSubstances(auth),
            IsProfessional = IsProfessional(product.Text("PRODUCT_userGroupCode")),
            IsOrganicAllowed = IsOrganic(auth),
            FormulationType = Formulation(product.Text("PRODUCT_formulationTypeFr"), formulationCode),
            Usages = FytowebUsageMapper.Map(auth),
        };
    }

    /// <summary>Clé de comparaison des n° d'autorisation (casse et espaces ignorés).</summary>
    public static string Key(string authorizationNumber) => authorizationNumber.Trim().ToUpperInvariant();

    /// <summary>Textes (spécifiques au produit si présents) des phrases SPe8.</summary>
    private static IEnumerable<string> Spe8Texts(JsonElement auth) =>
        auth.Path("pestLabelling").Objects("pestLabels")
            .Where(l => string.Equals(l.Text("code"), FytowebHeuristics.Spe8Code, StringComparison.OrdinalIgnoreCase))
            .Select(l => l.Text("customTextFr") ?? l.Text("textFr") ?? "");

    /// <summary>« boscalid 200 g/L; dimoxystrobine 200 g/L » (substances actives uniquement).</summary>
    private static string? ActiveSubstances(JsonElement auth)
    {
        var parts = auth.Path("composition").Objects("PRODUCT_Components")
            .Where(c => c.Text("Component_roleInFormulationFr") is null or ActiveSubstanceRole)
            .Select(Describe)
            .OfType<string>()
            .ToList();
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    private static string? Describe(JsonElement component)
    {
        var name = component.Path("Component_substance").Text("Substance_nameFr");
        if (name is null)
        {
            return null;
        }

        var amount = component.Decimal("Component_pure");
        var unit = component.Text("Component_formulationTypeUnitFr");
        return amount is null
            ? name
            : $"{name} {amount.Value.ToString("0.####", CultureInfo.InvariantCulture)} {unit}".TrimEnd();
    }

    /// <summary>PRO ou AMA+PRO → professionnel ; AMA seul → non professionnel ; inconnu → professionnel.</summary>
    private static bool IsProfessional(string? userGroup) =>
        userGroup is null || userGroup.Contains(ProfessionalGroup, StringComparison.OrdinalIgnoreCase);

    private static bool IsOrganic(JsonElement auth) =>
        auth.Objects("ProductAttributes")
            .Any(a => a.Text("textFr")?.Contains(OrganicMarker, StringComparison.OrdinalIgnoreCase) == true);

    private static string? Formulation(string? nameFr, string? code) => (nameFr, code) switch
    {
        (not null, not null) => $"{nameFr} ({code})",
        _ => nameFr ?? code,
    };
}
