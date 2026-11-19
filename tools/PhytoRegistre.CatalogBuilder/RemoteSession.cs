using System.Globalization;
using PhytoRegistre.CatalogBuilder.Sftp;

namespace PhytoRegistre.CatalogBuilder;

/// <summary>Connexion SFTP avec traduction des erreurs réseau en messages français sans secret.</summary>
internal sealed class RemoteSession : IDisposable
{
    private const int MaxDepth = 5;

    private readonly ISftpSource _source;
    private readonly SftpSettings _settings;

    private RemoteSession(ISftpSource source, SftpSettings settings)
    {
        _source = source;
        _settings = settings;
    }

    public static SftpSettings Settings(CommandLine cl, Func<string, string?> env)
    {
        var key = cl.Get("--key");
        if (key is not null && !File.Exists(key))
        {
            throw ToolException.Usage($"Clé privée introuvable : {key}");
        }

        var password = env(SftpSettings.PasswordVariable);
        if (key is null && string.IsNullOrEmpty(password))
        {
            throw ToolException.Usage(
                $"Mot de passe SFTP absent : définissez la variable d'environnement {SftpSettings.PasswordVariable} "
                + "(mot de passe fourni dans l'e-mail d'inscription Fytoweb) ou utilisez --key <fichier>.");
        }

        if (!int.TryParse(cl.Get("--port", "22"), NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535)
        {
            throw ToolException.Usage("--port doit être un numéro de port valide.");
        }

        return new SftpSettings(
            cl.Get("--host", SftpSettings.DefaultHost),
            port,
            cl.Get("--user", SftpSettings.DefaultUser),
            string.IsNullOrEmpty(password) ? null : password,
            key,
            env(SftpSettings.KeyPassphraseVariable),
            cl.Get("--host-fingerprint") ?? env(SftpSettings.HostFingerprintVariable));
    }

    public static RemoteSession Open(ISftpSourceFactory factory, SftpSettings settings, TextWriter output)
    {
        output.WriteLine($"Connexion SFTP à {settings}…");
        return new RemoteSession(Guard(settings, () => factory.Connect(settings, output)), settings);
    }

    /// <summary>Fichiers et dossiers sous <paramref name="root"/>, récursivement (profondeur limitée).</summary>
    public IReadOnlyList<RemoteEntry> Walk(string root)
    {
        var result = new List<RemoteEntry>();
        Walk(root, 0, result);
        return result;
    }

    public void Download(RemoteEntry file, string localPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(localPath))!);
        var temp = localPath + ".part";
        using (var stream = File.Create(temp))
        {
            Guard(_settings, () =>
            {
                _source.Download(file.FullName, stream);
                return true;
            });
        }

        File.Move(temp, localPath, overwrite: true);
    }

    public void Dispose() => _source.Dispose();

    private void Walk(string path, int depth, List<RemoteEntry> result)
    {
        var entries = Guard(_settings, () => _source.List(path));
        foreach (var entry in entries.OrderBy(e => e.Name, StringComparer.Ordinal))
        {
            result.Add(entry);
            if (entry.IsDirectory && depth < MaxDepth)
            {
                Walk(entry.FullName, depth + 1, result);
            }
        }
    }

    private static T Guard<T>(SftpSettings settings, Func<T> action)
    {
        try
        {
            return action();
        }
        catch (Exception ex) when (ex is not ToolException and not OutOfMemoryException)
        {
            throw new ToolException(
                ExitCodes.Network,
                $"Échec SFTP ({ex.GetType().Name}) : {settings.Redact(ex.Message)}. "
                + "Vérifiez la connexion depuis une IP belge, l'utilisateur et le mot de passe.",
                ex);
        }
    }
}
