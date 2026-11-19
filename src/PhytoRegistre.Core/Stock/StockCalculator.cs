using PhytoRegistre.Core.Abstractions;
using PhytoRegistre.Core.Domain;

namespace PhytoRegistre.Core.Stock;

/// <summary>Soldes de stock et sorties automatiques par fiche de traitement.</summary>
public sealed class StockCalculator : IStockCalculator
{
    public IReadOnlyList<StockBalance> ComputeBalances(
        IReadOnlyList<Product> products,
        IReadOnlyList<StockMovement> movements)
    {
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(movements);

        var totals = movements
            .GroupBy(m => m.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.Quantity));

        return products
            .Select(p => new StockBalance(
                p.Id, p.CommercialName, p.Unit, totals.GetValueOrDefault(p.Id)))
            .ToList();
    }

    public StockMovement CreateConsumption(TreatmentRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.ProductId is not { } productId)
        {
            throw new InvalidOperationException(
                "Impossible de créer une sortie de stock : la fiche n'est liée à aucun produit du stock.");
        }

        return new StockMovement
        {
            ProductId = productId,
            Date = record.ApplicationDate,
            Quantity = -record.TotalQuantity,
            Kind = StockMovementKind.TreatmentConsumption,
            TreatmentId = record.Id,
            Comment = $"Traitement {record.ProductName}".TrimEnd(),
        };
    }
}
