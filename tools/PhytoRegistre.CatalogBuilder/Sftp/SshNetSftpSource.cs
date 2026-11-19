using Renci.SshNet;
using Renci.SshNet.Common;

namespace PhytoRegistre.CatalogBuilder.Sftp;

/// <summary>Implémentation SSH.NET (SFTP, algorithmes SHA-2 / Ed25519 pris en charge).</summary>
public sealed class SshNetSftpSourceFactory : ISftpSourceFactory
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public ISftpSource Connect(SftpSettings settings, TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var info = new ConnectionInfo(settings.Host, settings.Port, settings.User, Authentication(settings))
        {
            Timeout = Timeout,
        };
        var client = new SftpClient(info);
        client.HostKeyReceived += (_, e) => VerifyHostKey(settings, e, log);
        try
        {
            client.Connect();
            return new SshNetSftpSource(client);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private static AuthenticationMethod Authentication(SftpSettings settings)
    {
        if (settings.KeyPath is not null)
        {
            var key = settings.KeyPassphrase is null
                ? new PrivateKeyFile(settings.KeyPath)
                : new PrivateKeyFile(settings.KeyPath, settings.KeyPassphrase);
            return new PrivateKeyAuthenticationMethod(settings.User, key);
        }

        return new PasswordAuthenticationMethod(settings.User, settings.Password ?? "");
    }

    /// <summary>Empreinte attendue fournie : refus si différente. Sinon : affichée pour vérification (TOFU).</summary>
    private static void VerifyHostKey(SftpSettings settings, HostKeyEventArgs e, TextWriter log)
    {
        var actual = "SHA256:" + e.FingerPrintSHA256;
        if (settings.HostFingerprint is { } expected)
        {
            e.CanTrust = string.Equals(Normalize(expected), Normalize(actual), StringComparison.Ordinal);
            if (!e.CanTrust)
            {
                log.WriteLine($"Empreinte du serveur inattendue ({actual}) : connexion refusée.");
            }

            return;
        }

        log.WriteLine($"Empreinte du serveur {settings.Host} : {e.HostKeyName} {actual}");
        log.WriteLine($"  (pour l'imposer : --host-fingerprint \"{actual}\" ou {SftpSettings.HostFingerprintVariable})");
        e.CanTrust = true;
    }

    private static string Normalize(string fingerprint) =>
        fingerprint.Trim().Replace("SHA256:", "", StringComparison.OrdinalIgnoreCase).TrimEnd('=');
}

internal sealed class SshNetSftpSource(SftpClient client) : ISftpSource
{
    public IReadOnlyList<RemoteEntry> List(string path) =>
        [
            .. client.ListDirectory(path)
                .Where(f => f.Name is not ("." or ".."))
                .Where(f => f.IsDirectory || f.IsRegularFile)
                .Select(f => new RemoteEntry(f.FullName, f.Name, f.Length, f.LastWriteTimeUtc, f.IsDirectory)),
        ];

    public void Download(string remotePath, Stream destination) => client.DownloadFile(remotePath, destination);

    public void Dispose()
    {
        if (client.IsConnected)
        {
            client.Disconnect();
        }

        client.Dispose();
    }
}
