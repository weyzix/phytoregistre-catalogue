namespace PhytoRegistre.CatalogBuilder.Sftp;

/// <summary>Entrée d'un répertoire distant.</summary>
public sealed record RemoteEntry(string FullName, string Name, long Size, DateTime LastWriteUtc, bool IsDirectory);

/// <summary>Session SFTP ouverte (lecture seule).</summary>
public interface ISftpSource : IDisposable
{
    IReadOnlyList<RemoteEntry> List(string path);

    void Download(string remotePath, Stream destination);
}

public interface ISftpSourceFactory
{
    /// <param name="log">Messages de connexion (empreinte du serveur) ; jamais de secret.</param>
    ISftpSource Connect(SftpSettings settings, TextWriter log);
}

/// <summary>
/// Paramètres de connexion. Le mot de passe n'est lu que depuis la variable d'environnement
/// <see cref="PasswordVariable"/> ; il n'est jamais écrit dans un journal (<see cref="ToString"/> le masque).
/// </summary>
public sealed record SftpSettings(
    string Host,
    int Port,
    string User,
    string? Password,
    string? KeyPath,
    string? KeyPassphrase,
    string? HostFingerprint)
{
    /// <summary>Serveur open data Fytoweb (e-mail d'inscription ; accès depuis une IP belge).</summary>
    public const string DefaultHost = "ftp.health.belgium.be";
    public const string DefaultUser = "ftppanama-opendata";
    public const int DefaultPort = 22;

    public const string PasswordVariable = "FYTOWEB_SFTP_PASSWORD";
    public const string KeyPassphraseVariable = "FYTOWEB_SFTP_KEY_PASSPHRASE";
    public const string HostFingerprintVariable = "FYTOWEB_SFTP_HOST_FINGERPRINT";

    public override string ToString() => $"{User}@{Host}:{Port}";

    /// <summary>Supprime toute occurrence d'un secret d'un message avant affichage.</summary>
    public string Redact(string message)
    {
        var result = message;
        foreach (var secret in new[] { Password, KeyPassphrase })
        {
            if (!string.IsNullOrEmpty(secret))
            {
                result = result.Replace(secret, "***", StringComparison.Ordinal);
            }
        }

        return result;
    }
}
