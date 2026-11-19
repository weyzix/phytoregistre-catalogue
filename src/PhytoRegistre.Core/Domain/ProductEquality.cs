namespace PhytoRegistre.Core.Domain;

// Égalité par valeur incluant les listes (Usages, TargetPests) : l'égalité générée par les
// records compare les IReadOnlyList par référence, ce qui casse les allers-retours JSON/SQLite.

public sealed partial record Product
{
    public bool Equals(Product? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null
            && Id == other.Id
            && CommercialName == other.CommercialName
            && AuthorizationNumber == other.AuthorizationNumber
            && Unit == other.Unit
            && HasTimeRestriction == other.HasTimeRestriction
            && IsSpe8 == other.IsSpe8
            && RequiresBbchStage == other.RequiresBbchStage
            && ActiveSubstances == other.ActiveSubstances
            && IsUserDefined == other.IsUserDefined
            && IsSample == other.IsSample
            && HasSameCatalogData(other);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Id);
        hash.Add(CommercialName);
        hash.Add(AuthorizationNumber);
        hash.Add(Status);
        hash.Add(AuthorizedUntil);
        hash.Add(SequenceEquality.Hash(Usages));
        return hash.ToHashCode();
    }

    private bool HasSameCatalogData(Product other) =>
        Source == other.Source
        && Status == other.Status
        && AuthorizedUntil == other.AuthorizedUntil
        && UseToleratedUntil == other.UseToleratedUntil
        && SuspendedFrom == other.SuspendedFrom
        && SuspendedUntil == other.SuspendedUntil
        && IsProfessional == other.IsProfessional
        && IsOrganicAllowed == other.IsOrganicAllowed
        && FormulationType == other.FormulationType
        && SequenceEquality.Equal(Usages, other.Usages);
}

public sealed partial record ProductUsage
{
    public bool Equals(ProductUsage? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return other is not null
            && CropNameFr == other.CropNameFr
            && CropScientificName == other.CropScientificName
            && SequenceEquality.Equal(TargetPests, other.TargetPests)
            && DoseMin == other.DoseMin
            && DoseMax == other.DoseMax
            && DoseUnit == other.DoseUnit
            && GrowthStage == other.GrowthStage
            && MaxApplications == other.MaxApplications
            && PreHarvestIntervalDays == other.PreHarvestIntervalDays
            && Remarks == other.Remarks;
    }

    public override int GetHashCode() =>
        HashCode.Combine(CropNameFr, CropScientificName, SequenceEquality.Hash(TargetPests), DoseMin, DoseMax, DoseUnit);
}

internal static class SequenceEquality
{
    public static bool Equal<T>(IReadOnlyList<T>? a, IReadOnlyList<T>? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        var left = a ?? [];
        var right = b ?? [];
        return left.SequenceEqual(right);
    }

    public static int Hash<T>(IReadOnlyList<T>? items)
    {
        var hash = new HashCode();
        foreach (var item in items ?? [])
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }
}
