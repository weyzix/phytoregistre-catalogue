using PhytoRegistre.CatalogBuilder.Sftp;

namespace PhytoRegistre.CatalogBuilder;

/// <summary>Point d'entrée testable : analyse la ligne de commande et exécute build / download / list / all.</summary>
internal sealed class CatalogBuilderApp(
    ISftpSourceFactory sftp,
    TimeProvider time,
    Func<string, string?> env,
    TextWriter output,
    TextWriter error)
{
    public const string DefaultWorkDir = "fytoweb-brut";

    private static readonly string[] SftpOptions =
    [
        "--host", "--port", "--user", "--key", "--host-fingerprint", "--path",
        "--full-pattern", "--daily-pattern", "--suspensions-pattern",
    ];

    private static readonly Dictionary<string, HashSet<string>> Options = new(StringComparer.Ordinal)
    {
        ["build"] = ["--full", "--daily", "--suspensions", "--out", "--base-url"],
        ["download"] = [.. SftpOptions, "--out", "--dry-run"],
        ["list"] = ["--host", "--port", "--user", "--key", "--host-fingerprint", "--path"],
        ["all"] = [.. SftpOptions, "--out", "--work", "--base-url"],
        ["help"] = [],
    };

    public int Run(IReadOnlyList<string> args)
    {
        try
        {
            if (args.Count == 0 || !Options.TryGetValue(args[0], out var allowed))
            {
                throw ToolException.Usage(args.Count == 0 ? "Aucune commande indiquée." : $"Commande inconnue : {args[0]}");
            }

            return Execute(CommandLine.Parse(args, allowed));
        }
        catch (ToolException ex)
        {
            error.WriteLine($"Erreur : {ex.Message}");
            if (ex.ExitCode == ExitCodes.Usage)
            {
                error.WriteLine(Usage.Text);
            }

            return ex.ExitCode;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"Erreur d'accès aux fichiers : {ex.Message}");
            return ExitCodes.IoError;
        }
    }

    private int Execute(CommandLine cl)
    {
        switch (cl.Command)
        {
            case "build":
                return new BuildCommand(time, output).Run(BuildCommand.FromCommandLine(cl));
            case "download":
                return new DownloadCommand(sftp, output).Run(
                    RemoteSession.Settings(cl, env), DownloadRequest.FromCommandLine(cl, cl.Required("--out")));
            case "list":
                return new ListCommand(sftp, output).Run(RemoteSession.Settings(cl, env), cl.Get("--path", "/"));
            case "all":
                return RunAll(cl);
            default:
                output.WriteLine(Usage.Text);
                return ExitCodes.Success;
        }
    }

    private int RunAll(CommandLine cl)
    {
        var outDir = cl.Required("--out");
        var work = cl.Get("--work", DefaultWorkDir);
        var build = new BuildRequest(
            Path.Combine(work, DownloadCommand.FullDir),
            Path.Combine(work, DownloadCommand.DailyDir),
            null,
            outDir,
            BuildCommand.ValidateBaseUrl(cl.Get("--base-url")));
        var code = new DownloadCommand(sftp, output).Run(RemoteSession.Settings(cl, env), DownloadRequest.FromCommandLine(cl, work));
        if (code != ExitCodes.Success)
        {
            return code;
        }

        var suspensions = Path.Combine(work, DownloadCommand.SuspensionsFile);
        var daily = Directory.Exists(build.Daily) ? build.Daily : null;
        return new BuildCommand(time, output).Run(build with
        {
            Daily = daily,
            Suspensions = File.Exists(suspensions) ? suspensions : null,
        });
    }
}
