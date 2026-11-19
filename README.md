# phytoregistre-catalogue

Publication automatique du catalogue de produits phytopharmaceutiques utilisé par l'application
Android **PhytoRegistre**.

Chaque jour, un poste situé en Belgique (le serveur SFTP Fytoweb n'accepte que les adresses IP belges) :

1. télécharge l'export officiel Fytoweb par SFTP (`tools/PhytoRegistre.CatalogBuilder`) — l'export
   complet mensuel est découpé en parties (`yyyy_MM_dd_FULL_n.json`), suivies de fichiers `yyyy_MM_dd_DELTA.json` ;
2. construit `catalog-<version>.json.gz` + `manifest.json` (empreinte SHA-256) ;
3. publie le tout sur la branche `gh-pages` (GitHub Pages).

Le workflow [.github/workflows/catalogue.yml](.github/workflows/catalogue.yml) fait la même chose mais
est désactivé : les runners GitHub (hors Belgique) sont refusés par le serveur Fytoweb.

Les téléphones ne téléchargent que ces fichiers publics — aucun identifiant n'est embarqué dans l'app.
Si une exécution échoue, la dernière version publiée reste en ligne.

## Configuration

- Secret du dépôt `FYTOWEB_SFTP_PASSWORD` : mot de passe reçu dans l'e-mail d'inscription Fytoweb
  (<https://fytoweb.be/en/data>).
- Variable optionnelle `FYTOWEB_FULL_PATTERN` : motif du nom du fichier d'export complet.
- Diagnostic : *Actions › Catalogue Fytoweb › Run workflow › mode = list*.

## Source et licence des données

Source : SPF Santé publique – Fytoweb (<https://fytoweb.be>), licence
[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/deed.fr). Données transformées ;
seule la base officielle Fytoweb fait foi. Projet indépendant, non affilié au SPF Santé publique.
