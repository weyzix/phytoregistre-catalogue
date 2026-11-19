namespace PhytoRegistre.Core.Domain;

/// <summary>1. Type d'utilisation (facultatif si déductible des autres données).</summary>
public enum UsageType
{
    Unspecified = 0,
    /// <summary>Plein air : champ agricole, surface non cultivée, serre non permanente.</summary>
    OpenField = 1,
    /// <summary>Sous protection : hangar PDT, silo, serre permanente.</summary>
    UnderProtection = 2,
    /// <summary>Traitement de semences / plants.</summary>
    SeedTreatment = 3,
}

/// <summary>Formulation : détermine l'unité de dose (kg ou L).</summary>
public enum QuantityUnit
{
    Liter = 0,
    Kilogram = 1,
}

/// <summary>Catégorie de culture, utilisée pour l'exemption de stade BBCH.</summary>
public enum CropCategory
{
    Regular = 0,
    /// <summary>Jachère.</summary>
    Fallow = 1,
    /// <summary>Culture intermédiaire / engrais vert / couvert.</summary>
    CoverCrop = 2,
    /// <summary>Terrain meuble non cultivé en permanence.</summary>
    Uncultivated = 3,
}

public enum Severity
{
    Info = 0,
    Warning = 1,
    Error = 2,
}

public enum StockMovementKind
{
    Purchase = 0,
    TreatmentConsumption = 1,
    Adjustment = 2,
    Return = 3,
}
