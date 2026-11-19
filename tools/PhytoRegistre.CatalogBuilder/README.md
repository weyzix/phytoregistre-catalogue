# PhytoRegistre.CatalogBuilder (`phyto-catalog`)

Outil **réservé à l'éditeur de l'application**. Il télécharge l'export open data officiel de
Fytoweb (SPF Santé publique), en tire un **catalogue compact** (`catalog-<version>.json.gz` +
`manifest.json`) et vous laisse le publier sur un hébergement HTTPS statique. Les téléphones ne
téléchargent **que** ce catalogue public : aucun identifiant SFTP n'est jamais embarqué dans l'application.

## 1. Inscription et accès

- Demande d'accès (gratuite) : <https://fytoweb.be/en/data>. L'e-mail de confirmation contient le mot de passe.
- Protocole **SFTP** — serveur `ftp.health.belgium.be`, utilisateur `ftppanama-opendata`
  (valeurs par défaut de l'outil ; modifiables avec `--host` / `--user`).
- Le serveur n'accepte que des connexions depuis une **adresse IP belge** et des algorithmes
  récents (**SHA-2**, clés **Ed25519**) : SSH.NET les prend en charge.
- **Mot de passe** : uniquement par la variable d'environnement `FYTOWEB_SFTP_PASSWORD`
  (mot de passe fourni dans l'e-mail d'inscription Fytoweb). Il n'est jamais accepté en argument,
  jamais affiché ni écrit dans les journaux. Ne le committez nulle part.
- Clé privée (alternative) : `--key <fichier>` ; phrase de passe éventuelle dans `FYTOWEB_SFTP_KEY_PASSPHRASE`.
- Empreinte du serveur : affichée à la première connexion. Pour l'imposer ensuite :
  `--host-fingerprint "SHA256:..."` ou variable `FYTOWEB_SFTP_HOST_FINGERPRINT`.

## 2. Commandes

```text
phyto-catalog list     [--path /]                              # arborescence distante (noms, tailles, dates)
phyto-catalog download --out <dossier brut> [--dry-run]        # télécharge les fichiers retenus
phyto-catalog build    --full <fichier|dossier> [--daily <dossier>] [--suspensions <fichier>]
                       --out <dossier publication> [--base-url https://...]
phyto-catalog all      --out <dossier publication> [--work <dossier brut>] [--base-url https://...]
```

Options SFTP communes : `--host`, `--port` (22), `--user`, `--key`, `--host-fingerprint`, `--path`,
`--full-pattern`, `--daily-pattern`, `--suspensions-pattern`.

Codes de sortie : `0` succès, `1` erreur d'usage, `2` données invalides (rien n'est publié),
`3` erreur SFTP / réseau, `4` erreur de fichier local.

### Nommage des fichiers distants (à confirmer à la première connexion)

Le nommage réel n'est connu qu'après inscription. Lancez d'abord `list`, puis `download --dry-run`,
et ajustez les motifs si nécessaire (`*` et `?`, plusieurs motifs séparés par `;`, sans tenir compte de la casse) :

| Fichier | Motif par défaut (hypothèse) | Règle de sélection |
|---|---|---|
| Export complet mensuel | `*full*.json;*full*.zip;*export*.json;*export*.zip` | le plus récent |
| Fichiers quotidiens | `*daily*…;*update*…;*delta*…` (.json/.zip) | postérieurs à l'export complet, dans l'ordre chronologique |
| Suspensions | `suspension*.json` | le plus récent |

« Plus récent » = date `yyyyMMdd` / `yyyy-MM-dd` présente dans le nom, à défaut date de modification.
Les archives `.zip` sont acceptées (la plus grande entrée `.json` est lue).
Exemple : l'échantillon officiel s'appelle `test-data-20251119.json` ; il faudrait `--full-pattern "*data*.json"`.

`download` range les fichiers dans `<out>/full/`, `<out>/daily/` (vidé à chaque exécution) et `<out>/suspensions.json`.

## 3. Règles de transformation (résumé)

- Identifiant produit stable : GUID dérivé du n° d'autorisation (les fiches restent liées après mise à jour).
- Statut : `AKK` → autorisé, `PROVISIONAL_AGREEMENT` → provisoire, `INT` → retiré ; autre code → inconnu.
- Fichiers quotidiens : une autorisation de même n° remplace la précédente (aucun marqueur de suppression
  n'est documenté : un retrait arrive comme décision `INT`).
- Suspensions : chaque n° listé (séparateurs `,` ou `;`) reçoit la période `dateFrom` → `dateTo`.
- SPe8 / restriction horaire : heuristique sur le texte de la phrase SPe8 (« tôt le matin », « tard le soir »,
  « heure », « 20h00 »…). **La liste officielle des produits à restriction horaire (PDF Fytoweb) fait foi.**
- Les éléments inexploitables sont ignorés et comptés ; un fichier dont la racine n'est pas un tableau JSON
  (ou tronqué) fait échouer la construction. Un catalogue vide n'est jamais publié.

## 4. Publication

Exemple avec une release GitHub nommée `catalogue` :

```powershell
phyto-catalog all --out .\publication --base-url https://github.com/<compte>/<depot>/releases/download/catalogue
gh release upload catalogue (Get-ChildItem .\publication\catalog-*.json.gz).FullName --clobber
gh release upload catalogue .\publication\manifest.json --clobber
```

Avec GitHub Pages : copiez `catalog-*.json.gz` et `manifest.json` dans la branche publiée
(sans `--base-url`, `catalogUrl` est relatif au manifeste). Publiez **toujours** le catalogue avant le manifeste.

## 5. Planification (Planificateur de tâches Windows)

```powershell
# Le mot de passe est stocké une fois dans l'environnement utilisateur (jamais dans la commande planifiée)
[Environment]::SetEnvironmentVariable("FYTOWEB_SFTP_PASSWORD", (Read-Host "Mot de passe Fytoweb"), "User")
```

Créez `C:\phyto\publier.cmd` :

```bat
@echo off
cd /d C:\phyto
phyto-catalog.exe all --out publication --work brut >> catalogue.log 2>&1 || exit /b 1
rem cmd.exe ne développe pas « * » : boucle explicite (catalogue d'abord, manifeste ensuite)
for %%f in (publication\catalog-*.json.gz) do gh release upload catalogue "%%f" --clobber >> catalogue.log 2>&1
gh release upload catalogue publication\manifest.json --clobber >> catalogue.log 2>&1
```

puis planifiez-le chaque jour (la machine doit avoir une IP belge) :

```bat
schtasks /Create /TN "PhytoRegistre catalogue" /SC DAILY /ST 06:30 /TR "C:\phyto\publier.cmd"
```

## 6. Licence et attribution

Les données Fytoweb sont publiées sous **CC BY 4.0**. Le catalogue contient le texte d'attribution
(`attribution`), que **l'application doit afficher** (écran « À propos » / source des données) :
« Source : SPF Santé publique – Fytoweb (fytoweb.be), licence CC BY 4.0. Données transformées. »
