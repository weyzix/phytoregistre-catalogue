using System.Globalization;
using System.Security.Cryptography;
using PhytoRegistre.Core.Abstractions;
using PhytoRegistre.Core.Catalog;
using PhytoRegistre.Core.Fytoweb;

namespace PhytoRegistre.CatalogBuilder;

/// <summary>Paramètres de la commande build.</summary>
internal sealed record BuildRequest(string Full, string? Daily, string? Suspensions, string OutDir, string? BaseUrl);

/// <summary>Construit catalog-&lt;version&gt;.json.gz et manifest.json à partir de l'export Fytoweb local.</summary>
internal sealed class BuildCommand(TimeProvider time, TextWriter output)
{
    public const string ManifestFileName = "manifest.json";

    public static BuildRequest FromCommandLine(CommandLine cl) => new(
        cl.Required("--full"),
        cl.Get("--daily"),
        cl.Get("--suspensions"),
        cl.Required("--out"),
        ValidateBaseUrl(cl.Get("--base-url")));

    public int Run(BuildRequest request)
    {
        var fullParts = InputFiles.ResolveFullParts(request.Full);
        var dailyFiles = InputFiles.ResolveDaily(request.Daily, fullParts[0]);
        var suspensionsFile = InputFiles.ResolveOptionalFile(request.Suspensions);
        output.WriteLine($"Export complet : {string.Join(", ", fullParts.Select(Path.GetFileName))}");
        output.WriteLine($"Fichiers quotidiens : {dailyFiles.Count}");

        // Les parties 2..n s'appliquent comme des mises à jour : chaque autorisation est identifiée par son
        // numéro, donc ajouter une partie revient à fusionner, et les fichiers quotidiens passent ensuite.
        var result = Parse(fullParts[0], [.. fullParts.Skip(1), .. dailyFiles], suspensionsFile);
        if (result.Catalog.Products.Count == 0)
        {
            throw ToolException.InvalidData("Le catalogue ne contient aucun produit : publication refusée.");
        }

        Write(result.Catalog, request);
        output.WriteLine(
            $"Catalogue {result.Catalog.Version} : {result.Catalog.Products.Count} produits, "
            + $"{result.SkippedElements} élément(s) ignoré(s), {result.SuspensionsApplied} suspension(s).");
        output.WriteLine($"Attribution à afficher dans l'application : {result.Catalog.Attribution}");
        return ExitCodes.Success;
    }

    private FytowebParseResult Parse(string fullFile, IReadOnlyList<string> dailyFiles, string? suspensionsFile)
    {
        var owned = new List<IDisposable>();
        try
        {
            var full = InputFiles.Open(fullFile, owned);
            var daily = dailyFiles.Select(f => InputFiles.Open(f, owned)).ToList();
            var suspensions = suspensionsFile is null ? null : InputFiles.Open(suspensionsFile, owned);
            return new FytowebCatalogParser().ParseWithReport(full, daily, suspensions, time.GetUtcNow());
        }
        catch (FormatException ex)
        {
            throw ToolException.InvalidData($"Données Fytoweb invalides : {ex.Message}", ex);
        }
        finally
        {
            owned.ForEach(d => d.Dispose());
        }
    }

    private void Write(ProductCatalog catalog, BuildRequest request)
    {
        var serializer = new CatalogSerializer();
        var bytes = serializer.SerializeCatalog(catalog);
        var fileName = $"catalog-{catalog.Version}.json.gz";
        var manifest = new CatalogManifest
        {
            Version = catalog.Version,
            GeneratedAt = catalog.GeneratedAt,
            ProductCount = catalog.Products.Count,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            CatalogUrl = request.BaseUrl is null ? fileName : request.BaseUrl.TrimEnd('/') + "/" + fileName,
            CatalogSizeBytes = bytes.Length,
            Attribution = catalog.Attribution,
        };

        Directory.CreateDirectory(request.OutDir);
        var catalogPath = Path.Combine(request.OutDir, fileName);
        File.WriteAllBytes(catalogPath, bytes);
        File.WriteAllText(Path.Combine(request.OutDir, ManifestFileName), serializer.SerializeManifest(manifest));
        output.WriteLine($"Écrit : {catalogPath} ({(bytes.Length / 1024.0).ToString("0.#", CultureInfo.InvariantCulture)} Ko)");
        output.WriteLine($"Écrit : {Path.Combine(request.OutDir, ManifestFileName)}");
    }

    public static string? ValidateBaseUrl(string? baseUrl)
    {
        if (baseUrl is null)
        {
            return null;
        }

        return Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? baseUrl
            : throw ToolException.Usage($"--base-url doit être une URL HTTPS absolue (reçu : {baseUrl}).");
    }
}
