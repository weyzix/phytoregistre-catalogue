namespace PhytoRegistre.Core.Export;

/// <summary>Cellule CSV : texte saisi par l'utilisateur (à neutraliser) ou valeur générée par l'application.</summary>
internal readonly record struct CsvCell(string Value, bool IsGenerated)
{
    private static readonly char[] FormulaTriggers = ['=', '+', '-', '@', '\t', '\r'];
    private static readonly char[] QuoteTriggers = [';', '"', '\n', '\r'];

    public static CsvCell Text(string? value) => new(value ?? "", false);

    public static CsvCell Generated(string? value) => new(value ?? "", true);

    /// <summary>
    /// Rend la cellule : neutralisation de l'injection de formules (OWASP « CSV Injection ») pour le
    /// texte libre — préfixe « ' » — puis guillemets RFC 4180 si « ; », « " » ou saut de ligne.
    /// </summary>
    public string Render()
    {
        var value = !IsGenerated && Value.Length > 0 && FormulaTriggers.Contains(Value[0])
            ? "'" + Value
            : Value;

        return value.IndexOfAny(QuoteTriggers) >= 0
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;
    }
}
