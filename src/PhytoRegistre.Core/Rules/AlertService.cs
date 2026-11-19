using PhytoRegistre.Core.Abstractions;
using PhytoRegistre.Core.Domain;
using PhytoRegistre.Core.Export;
using PhytoRegistre.Core.Validation;

namespace PhytoRegistre.Core.Rules;

/// <summary>
/// Alertes transverses du registre : rubriques bloquantes manquantes, fiche ETA non remise,
/// enregistrement tardif, x,y à remplacer par le n° PAC, stock négatif.
/// </summary>
public sealed class AlertService(IRecordValidator validator, IDeadlineCalculator deadlines) : IAlertService
{
    public const int ClientSheetReminderDays = 5;

    private const string RegisteredLateCode = "REGISTERED_LATE";

    public AlertService() : this(new RecordValidator(), new DeadlineCalculator())
    {
    }

    public IReadOnlyList<RegisterAlert> GetAlerts(
        IReadOnlyList<TreatmentRecord> records,
        IReadOnlyList<Parcel> parcels,
        IReadOnlyList<StockBalance> stock,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(parcels);
        ArgumentNullException.ThrowIfNull(stock);

        var parcelsById = parcels.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());

        return
        [
            .. records.SelectMany(r => RecordAlerts(r, parcelsById, today)),
            .. stock.Where(s => s.Quantity < 0).Select(NegativeStockAlert),
        ];
    }

    private IEnumerable<RegisterAlert> RecordAlerts(
        TreatmentRecord record, IReadOnlyDictionary<Guid, Parcel> parcels, DateOnly today)
    {
        // Pas d'alerte CONVERSION_DUE_SOON : ce registre est électronique dès la saisie, la
        // conversion papier → électronique (SPEC B, DeadlineInfo.ElectronicConversionDeadline)
        // est donc toujours déjà réalisée. L'échéance reste calculée pour information.
        var info = deadlines.Compute(record);
        IEnumerable<ValidationIssue> issues =
        [
            .. validator.Validate(record, today).Where(IsRegisterLevelIssue),
            .. ClientSheetIssues(record, info, today),
            .. PacReferenceIssues(record, parcels),
        ];
        return issues.Select(i => new RegisterAlert(record.Id, i));
    }

    /// <summary>Erreurs bloquantes et dépassement du délai légal d'enregistrement (7 jours).</summary>
    private static bool IsRegisterLevelIssue(ValidationIssue issue) =>
        issue.Severity == Severity.Error || issue.Code == RegisteredLateCode;

    private static IEnumerable<ValidationIssue> ClientSheetIssues(
        TreatmentRecord record, DeadlineInfo info, DateOnly today)
    {
        if (record.Contractor is null || record.Contractor.ClientSheetSentOn is not null
            || info.ClientSheetDeadline is not { } deadline)
        {
            yield break;
        }

        const string field = nameof(TreatmentRecord.Contractor);
        var deadlineText = FrenchFormat.Date(deadline);
        if (today > deadline)
        {
            yield return new(Severity.Warning, field, "CLIENT_SHEET_OVERDUE",
                $"Fiche non remise au client {record.Contractor.ClientName} : délai de 30 jours dépassé ({deadlineText}).");
        }
        else if (deadline.DayNumber - today.DayNumber <= ClientSheetReminderDays)
        {
            yield return new(Severity.Info, field, "CLIENT_SHEET_DUE_SOON",
                $"Fiche à remettre au client {record.Contractor.ClientName} au plus tard le {deadlineText}.");
        }
    }


    private static IEnumerable<ValidationIssue> PacReferenceIssues(
        TreatmentRecord record, IReadOnlyDictionary<Guid, Parcel> parcels)
    {
        var location = record.Location;
        if (location.Coordinates is null || !string.IsNullOrWhiteSpace(location.PacReference)
            || location.ParcelId is not { } parcelId
            || !parcels.TryGetValue(parcelId, out var parcel)
            || string.IsNullOrWhiteSpace(parcel.PacReference))
        {
            yield break;
        }

        yield return new(Severity.Warning, nameof(TreatmentRecord.Location), "PAC_REFERENCE_AVAILABLE",
            $"Le n° PAC de la parcelle « {parcel.Name} » est maintenant connu : "
            + $"remplacer x,y par n° PAC {parcel.PacReference}.");
    }

    private static RegisterAlert NegativeStockAlert(StockBalance balance)
    {
        var issue = new ValidationIssue(Severity.Warning, nameof(StockBalance.Quantity), "STOCK_NEGATIVE",
            $"Stock négatif pour {balance.ProductName} ({FrenchFormat.Number(balance.Quantity)} {FrenchFormat.Unit(balance.Unit)}) : "
            + "vérifiez les entrées de stock.");
        return new RegisterAlert(null, issue);
    }
}
