namespace PhytoRegistre.CatalogBuilder;

/// <summary>Codes de sortie du programme.</summary>
public static class ExitCodes
{
    public const int Success = 0;
    public const int Usage = 1;
    public const int InvalidData = 2;
    public const int Network = 3;
    public const int IoError = 4;
}

/// <summary>Erreur destinée à l'utilisateur, avec son code de sortie.</summary>
internal sealed class ToolException(int exitCode, string message, Exception? inner = null) : Exception(message, inner)
{
    public int ExitCode { get; } = exitCode;

    public static ToolException Usage(string message) => new(ExitCodes.Usage, message);

    public static ToolException InvalidData(string message, Exception? inner = null) => new(ExitCodes.InvalidData, message, inner);
}

/// <summary>Ligne de commande : « commande --option valeur --drapeau ».</summary>
internal sealed class CommandLine
{
    private static readonly HashSet<string> Flags = new(StringComparer.Ordinal) { "--dry-run" };

    private readonly Dictionary<string, string> _options;
    private readonly HashSet<string> _flags;

    private CommandLine(string command, Dictionary<string, string> options, HashSet<string> flags)
    {
        Command = command;
        _options = options;
        _flags = flags;
    }

    public string Command { get; }

    public static CommandLine Parse(IReadOnlyList<string> args, IReadOnlySet<string> allowedOptions)
    {
        if (args.Count == 0)
        {
            throw ToolException.Usage("Aucune commande indiquée.");
        }

        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        var flags = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Count; i++)
        {
            var name = args[i];
            if (Flags.Contains(name) && allowedOptions.Contains(name))
            {
                flags.Add(name);
                continue;
            }

            if (!allowedOptions.Contains(name))
            {
                throw ToolException.Usage($"Option inconnue pour « {args[0]} » : {name}");
            }

            if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw ToolException.Usage($"Valeur manquante pour l'option {name}.");
            }

            options[name] = args[++i];
        }

        return new CommandLine(args[0], options, flags);
    }

    public string? Get(string name) => _options.GetValueOrDefault(name);

    public string Get(string name, string defaultValue) => _options.GetValueOrDefault(name) ?? defaultValue;

    public string Required(string name) =>
        _options.GetValueOrDefault(name) ?? throw ToolException.Usage($"Option obligatoire manquante : {name}");

    public bool Has(string flag) => _flags.Contains(flag);
}
