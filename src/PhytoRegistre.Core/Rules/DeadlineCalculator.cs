using PhytoRegistre.Core.Abstractions;
using PhytoRegistre.Core.Domain;

namespace PhytoRegistre.Core.Rules;

/// <summary>Délais légaux d'une fiche.</summary>
public sealed class DeadlineCalculator : IDeadlineCalculator
{
    public const int RegistrationDays = 7;
    public const int ClientSheetDays = 30;
    public const int ElectronicConversionDays = 30;

    /// <summary>Dernière année sans obligation de conversion électronique.</summary>
    public const int LastYearWithoutConversion = 2026;

    /// <summary>Dernière année de la période transitoire (conversion au 31 janvier suivant).</summary>
    public const int LastTransitionYear = 2029;

    public DeadlineInfo Compute(TreatmentRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var date = record.ApplicationDate;

        return new DeadlineInfo(
            RegistrationDeadline: date.AddDays(RegistrationDays),
            ClientSheetDeadline: record.Contractor is null ? null : date.AddDays(ClientSheetDays),
            ElectronicConversionDeadline: ConversionDeadline(date));
    }

    private static DateOnly? ConversionDeadline(DateOnly date) => date.Year switch
    {
        <= LastYearWithoutConversion => null,
        <= LastTransitionYear => new DateOnly(date.Year + 1, 1, 31),
        _ => date.AddDays(ElectronicConversionDays),
    };
}
