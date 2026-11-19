using System.Text;
using PhytoRegistre.Core.Abstractions;
using PhytoRegistre.Core.Domain;

namespace PhytoRegistre.Core.Export;

/// <summary>
/// Export du registre en CSV (Excel BE/FR) et construction du rapport de contrôle.
/// Couvre les 8 rubriques du Règlement d'exécution (UE) 2023/564 + données complémentaires.
/// </summary>
public sealed class RegisterExporter : IRegisterExporter
{
    private const string Separator = ";";
    private const string NewLine = "\r\n";

    private static readonly string[] CsvHeaders =
    [
        "Date", "Heure début", "Type d'utilisation", "Produit", "N° autorisation",
        "N° autorisation d'urgence", "Dose/ha", "Unité", "Surface traitée (ha)", "Quantité totale",
        "Parcelle", "N° PAC", "Cadastre", "Adresse", "X Lambert72", "Y Lambert72", "Culture",
        "Code OEPP", "Stade BBCH", "Client (ETA)", "ETA", "Lot semences", "Opérateur", "Variété",
        "Ennemi visé", "Météo", "Remarques",
    ];

    private static readonly string[] ReportHeaders =
    [
        "Date", "Heure début", "Type d'utilisation", "Produit", "N° autorisation",
        "N° autorisation d'urgence", "Dose/ha", "Surface traitée (ha)", "Quantité totale",
        "Localisation", "Culture", "Code OEPP", "Stade BBCH", "Client (ETA)", "Opérateur", "Remarques",
    ];

    private readonly TimeProvider _timeProvider;

    public RegisterExporter()
        : this(TimeProvider.System)
    {
    }

    /// <param name="timeProvider">Horloge (date de génération du rapport), injectable pour les tests.</param>
    public RegisterExporter(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    public byte[] ExportCsv(IReadOnlyList<TreatmentRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var builder = new StringBuilder();
        AppendLine(builder, CsvHeaders.Select(CsvCell.Generated));
        foreach (var record in Sort(records))
        {
            AppendLine(builder, ToCsvCells(record));
        }

        var utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        return [.. utf8WithBom.GetPreamble(), .. utf8WithBom.GetBytes(builder.ToString())];
    }

    public ControlReport BuildControlReport(
        IReadOnlyList<TreatmentRecord> records,
        string farmName,
        DateOnly from,
        DateOnly to)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(farmName);
        if (from > to)
        {
            throw new ArgumentException("La date de début doit précéder la date de fin.", nameof(from));
        }

        var rows = Sort(records.Where(r => r.ApplicationDate >= from && r.ApplicationDate <= to))
            .Select(ToReportRow)
            .ToList();
        var generatedAt = _timeProvider.GetLocalNow();

        return new ControlReport(
            Title: "Registre d'utilisation des produits phytopharmaceutiques",
            Subtitle: $"{farmName} — période du {FrenchFormat.Date(from)} au {FrenchFormat.Date(to)}",
            Headers: ReportHeaders,
            Rows: rows,
            Footer: "Registre tenu conformément au Règlement d'exécution (UE) 2023/564 — généré le "
                + $"{FrenchFormat.Date(DateOnly.FromDateTime(generatedAt.DateTime))} à "
                + FrenchFormat.Time(TimeOnly.FromDateTime(generatedAt.DateTime)));
    }

    private static IEnumerable<TreatmentRecord> Sort(IEnumerable<TreatmentRecord> records) =>
        records
            .OrderBy(r => r.ApplicationDate)
            .ThenBy(r => r.StartTime ?? TimeOnly.MinValue)
            .ThenBy(r => r.CreatedAt);

    private static void AppendLine(StringBuilder builder, IEnumerable<CsvCell> cells) =>
        builder.Append(string.Join(Separator, cells.Select(c => c.Render()))).Append(NewLine);

    private static IEnumerable<CsvCell> ToCsvCells(TreatmentRecord r)
    {
        var loc = r.Location;
        return
        [
            CsvCell.Generated(FrenchFormat.Date(r.ApplicationDate)),
            CsvCell.Generated(FrenchFormat.Time(r.StartTime)),
            CsvCell.Generated(FrenchFormat.Usage(r.UsageType)),
            CsvCell.Text(r.ProductName),
            CsvCell.Text(r.AuthorizationNumber),
            CsvCell.Text(r.EmergencyAuthorizationNumber),
            CsvCell.Generated(FrenchFormat.Number(r.DosePerHa)),
            CsvCell.Generated(FrenchFormat.Unit(r.DoseUnit)),
            CsvCell.Generated(FrenchFormat.Number(r.TreatedAreaHa)),
            CsvCell.Generated(FrenchFormat.Number(r.TotalQuantity)),
            CsvCell.Text(loc.Label),
            CsvCell.Text(loc.PacReference),
            CsvCell.Text(loc.CadastreNumber),
            CsvCell.Text(loc.Address),
            CsvCell.Generated(FrenchFormat.Number(loc.Coordinates?.X)),
            CsvCell.Generated(FrenchFormat.Number(loc.Coordinates?.Y)),
            CsvCell.Text(r.CropName),
            CsvCell.Text(r.EppoCode),
            CsvCell.Generated(FrenchFormat.Number(r.BbchStage)),
            CsvCell.Text(r.Contractor?.ClientName),
            CsvCell.Text(r.Contractor?.ContractorName),
            CsvCell.Text(FrenchFormat.Seed(r.Seed)),
            CsvCell.Text(r.Operator),
            CsvCell.Text(r.Variety),
            CsvCell.Text(r.TargetPest),
            // Généré à partir de nombres (peut commencer par « - » pour T° < 0) ; la source est en fin.
            CsvCell.Generated(FrenchFormat.Weather(r.Weather)),
            CsvCell.Text(r.Notes),
        ];
    }

    private static IReadOnlyList<string> ToReportRow(TreatmentRecord r) =>
    [
        FrenchFormat.Date(r.ApplicationDate),
        FrenchFormat.Time(r.StartTime),
        FrenchFormat.Usage(r.UsageType),
        r.ProductName,
        r.AuthorizationNumber,
        r.EmergencyAuthorizationNumber ?? "",
        $"{FrenchFormat.Number(r.DosePerHa)} {FrenchFormat.Unit(r.DoseUnit)}",
        FrenchFormat.Number(r.TreatedAreaHa),
        $"{FrenchFormat.Number(r.TotalQuantity)} {FrenchFormat.Unit(r.DoseUnit)}",
        FrenchFormat.MainLocation(r.Location),
        r.CropName ?? "",
        r.EppoCode ?? "",
        FrenchFormat.Number(r.BbchStage),
        FrenchFormat.Contractor(r.Contractor),
        r.Operator ?? "",
        r.Notes ?? "",
    ];
}
