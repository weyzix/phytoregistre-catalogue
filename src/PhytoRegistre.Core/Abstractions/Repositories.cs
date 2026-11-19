using PhytoRegistre.Core.Domain;

namespace PhytoRegistre.Core.Abstractions;

public interface ITreatmentRepository
{
    Task<IReadOnlyList<TreatmentRecord>> GetAllAsync(CancellationToken ct = default);
    Task<TreatmentRecord?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TreatmentRecord>> GetByPeriodAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    Task SaveAsync(TreatmentRecord record, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

/// <summary>Écriture atomique d'une fiche et de sa sortie de stock.</summary>
public interface ITreatmentStore
{
    /// <summary>
    /// Saves the record and atomically replaces its TreatmentConsumption movements by <paramref name="consumption"/> (null = none).
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="consumption"/> n'est pas une sortie TreatmentConsumption liée à <paramref name="record"/>.
    /// </exception>
    Task SaveAsync(TreatmentRecord record, StockMovement? consumption, CancellationToken ct = default);

    /// <summary>
    /// Deletes the record and its TreatmentConsumption movements atomically
    /// (other movement kinds keep but get TreatmentId cleared).
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IParcelRepository
{
    Task<IReadOnlyList<Parcel>> GetAllAsync(CancellationToken ct = default);
    Task<Parcel?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task SaveAsync(Parcel parcel, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface IProductRepository
{
    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken ct = default);
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task SaveAsync(Product product, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

public interface ICropRepository
{
    Task<IReadOnlyList<Crop>> GetAllAsync(CancellationToken ct = default);
    Task SaveAsync(Crop crop, CancellationToken ct = default);
}

public interface IStockRepository
{
    Task<IReadOnlyList<StockMovement>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<StockMovement>> GetByProductAsync(Guid productId, CancellationToken ct = default);
    Task<IReadOnlyList<StockMovement>> GetByTreatmentAsync(Guid treatmentId, CancellationToken ct = default);
    Task SaveAsync(StockMovement movement, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
