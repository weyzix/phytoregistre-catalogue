namespace PhytoRegistre.CatalogBuilder;

internal static class Usage
{
    public const string Text = """
        Usage : phyto-catalog <commande> [options]

          build    --full <fichier|dossier> [--daily <dossier>] [--suspensions <fichier>]
                   --out <dossier> [--base-url <https://...>]
                   Construit catalog-<version>.json.gz et manifest.json.

          download --out <dossier> [--dry-run] [options SFTP]
                   Télécharge le dernier export complet, les fichiers quotidiens postérieurs et suspensions.json.

          list     [--path /] [options SFTP]
                   Affiche l'arborescence distante (noms, tailles, dates).

          all      --out <dossier> [--work <dossier brut>] [--base-url <https://...>] [options SFTP]
                   download puis build.

          help     Affiche cette aide.

        Options SFTP : --host (défaut ftp.health.belgium.be) --user (défaut ftppanama-opendata) --port (22)
                       --key <clé privée> --host-fingerprint <SHA256:...> --path <dossier distant, défaut />
                       --full-pattern / --daily-pattern / --suspensions-pattern (motifs * ? séparés par ;)
        Mot de passe : variable d'environnement FYTOWEB_SFTP_PASSWORD (jamais en argument).
        Codes de sortie : 0 succès, 1 usage, 2 données invalides, 3 erreur SFTP/réseau, 4 erreur fichier.
        """;
}
