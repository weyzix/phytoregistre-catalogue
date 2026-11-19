using PhytoRegistre.Core.Abstractions;
using PhytoRegistre.Core.Domain;

namespace PhytoRegistre.Core.Geo;

/// <summary>
/// Conversion WGS84 (GPS) ↔ Lambert belge 72 (EPSG:31370).
/// <para>
/// Chaîne : WGS84 géographique → cartésien géocentrique (GRS80/WGS84) → Helmert 7 paramètres
/// (inverse de EPSG:15929 « BD72 to WGS 84 (3) ») → cartésien BD72 → géographique BD72
/// (ellipsoïde International 1924 / Hayford) → Lambert conique conforme 2SP (EPSG:9802).
/// Précision attendue de la transformation de datum : ~1 m (cf. EPSG:15929), suffisante pour
/// localiser une parcelle. Les hauteurs sont ignorées (h = 0), comme PROJ (push/pop v_3).
/// </para>
/// </summary>
public sealed class Lambert72Converter : ICoordinateConverter
{
    private const double ArcSecToRad = Math.PI / (180.0 * 3600.0);
    private const double DegToRad = Math.PI / 180.0;

    // --- Ellipsoïdes (EPSG:7030 WGS 84, EPSG:7022 International 1924) ---
    private static readonly Ellipsoid Wgs84 = new(6378137.0, 1.0 / 298.257223563);
    private static readonly Ellipsoid Hayford1924 = new(6378388.0, 1.0 / 297.0);

    // --- Helmert BD72 → WGS84, EPSG:15929 « BD72 to WGS 84 (3) », méthode Coordinate Frame (EPSG:1032).
    // Source : base EPSG / PROJ 9.5 (proj.db) ; équivalent au +towgs84=-106.8686,52.2978,-103.7239,
    // 0.3366,-0.457,1.8422,-1.2747 (convention Position Vector) de la définition PROJ de EPSG:31370.
    // Vérifié numériquement contre PROJ 9.5.1 (écarts < 1 cm, cf. tests).
    private const double Tx = -106.8686, Ty = 52.2978, Tz = -103.7239; // m
    private const double Rx = -0.3366 * ArcSecToRad;                    // Coordinate Frame
    private const double Ry = 0.457 * ArcSecToRad;
    private const double Rz = -1.8422 * ArcSecToRad;
    private const double Scale = -1.2747e-6;                            // ppm

    // --- Projection Belge Lambert 72 (EPSG:19961), Lambert Conic Conformal 2SP ---
    private static readonly double Lat1 = Dms(49, 50, 0.00204);   // 1er parallèle standard
    private static readonly double Lat2 = Dms(51, 10, 0.00204);   // 2e parallèle standard
    private static readonly double Lon0 = Dms(4, 22, 2.952);      // méridien central
    private const double FalseEasting = 150000.013;
    private const double FalseNorthing = 5400088.438;             // latitude d'origine = 90° → rF = 0

    private static readonly LccConstants Lcc = LccConstants.Create(Hayford1924, Lat1, Lat2);

    public LambertPoint ToLambert72(GeoPoint wgs84)
    {
        EnsureValid(wgs84);
        var wgsCart = ToCartesian(Wgs84, wgs84.Latitude * DegToRad, wgs84.Longitude * DegToRad);
        var bd72Cart = Wgs84ToBd72(wgsCart);
        var (lat, lon) = ToGeodetic(Hayford1924, bd72Cart);
        return Project(lat, lon);
    }

    public GeoPoint ToWgs84(LambertPoint lambert72)
    {
        if (!double.IsFinite(lambert72.X) || !double.IsFinite(lambert72.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(lambert72), "Coordonnées Lambert 72 invalides.");
        }

        var (lat, lon) = Unproject(lambert72);
        var bd72Cart = ToCartesian(Hayford1924, lat, lon);
        var wgsCart = Bd72ToWgs84(bd72Cart);
        var (wLat, wLon) = ToGeodetic(Wgs84, wgsCart);
        return new GeoPoint(wLat / DegToRad, wLon / DegToRad);
    }

    private static void EnsureValid(GeoPoint p)
    {
        if (!double.IsFinite(p.Latitude) || Math.Abs(p.Latitude) > 90.0
            || !double.IsFinite(p.Longitude) || Math.Abs(p.Longitude) > 180.0)
        {
            throw new ArgumentOutOfRangeException(nameof(p), "Coordonnées GPS invalides.");
        }
    }

    private static double Dms(int deg, int min, double sec) => (deg + min / 60.0 + sec / 3600.0) * DegToRad;

    // ---------- Helmert (Coordinate Frame) ----------

    /// <summary>X_wgs = T + (1 + s)·R·X_bd72, R = [[1, rz, −ry], [−rz, 1, rx], [ry, −rx, 1]].</summary>
    private static Vector3 Bd72ToWgs84(Vector3 p)
    {
        var k = 1.0 + Scale;
        return new Vector3(
            Tx + k * (p.X + Rz * p.Y - Ry * p.Z),
            Ty + k * (-Rz * p.X + p.Y + Rx * p.Z),
            Tz + k * (Ry * p.X - Rx * p.Y + p.Z));
    }

    /// <summary>Inverse : X_bd72 = Rᵀ·(X_wgs − T)/(1 + s) (Rᵀ = R⁻¹ au 2e ordre près, &lt; 1 mm).</summary>
    private static Vector3 Wgs84ToBd72(Vector3 p)
    {
        var k = 1.0 / (1.0 + Scale);
        var x = p.X - Tx;
        var y = p.Y - Ty;
        var z = p.Z - Tz;
        return new Vector3(
            k * (x - Rz * y + Ry * z),
            k * (Rz * x + y - Rx * z),
            k * (-Ry * x + Rx * y + z));
    }

    // ---------- Géographique ↔ cartésien ----------

    private static Vector3 ToCartesian(Ellipsoid e, double lat, double lon)
    {
        var sinLat = Math.Sin(lat);
        var n = e.A / Math.Sqrt(1.0 - e.E2 * sinLat * sinLat);
        var cosLat = Math.Cos(lat);
        return new Vector3(n * cosLat * Math.Cos(lon), n * cosLat * Math.Sin(lon), n * (1.0 - e.E2) * sinLat);
    }

    private static (double Lat, double Lon) ToGeodetic(Ellipsoid e, Vector3 p)
    {
        var lon = Math.Atan2(p.Y, p.X);
        var horizontal = Math.Sqrt(p.X * p.X + p.Y * p.Y);
        var lat = Math.Atan2(p.Z, horizontal * (1.0 - e.E2));
        for (var i = 0; i < 10; i++)
        {
            var sinLat = Math.Sin(lat);
            var n = e.A / Math.Sqrt(1.0 - e.E2 * sinLat * sinLat);
            var next = Math.Atan2(p.Z + e.E2 * n * sinLat, horizontal);
            if (Math.Abs(next - lat) < 1e-14)
            {
                return (next, lon);
            }

            lat = next;
        }

        return (lat, lon);
    }

    // ---------- Lambert Conic Conformal 2SP (EPSG Guidance Note 7-2, § 1.3.1.1) ----------

    private static LambertPoint Project(double lat, double lon)
    {
        var r = Hayford1924.A * Lcc.F * Math.Pow(Lcc.T(lat), Lcc.N);
        var theta = Lcc.N * (lon - Lon0);
        return new LambertPoint(FalseEasting + r * Math.Sin(theta), FalseNorthing - r * Math.Cos(theta));
    }

    private static (double Lat, double Lon) Unproject(LambertPoint p)
    {
        var dx = p.X - FalseEasting;
        var dy = FalseNorthing - p.Y; // rF − (N − FN) avec rF = 0
        var r = Math.Sqrt(dx * dx + dy * dy);
        var t = Math.Pow(r / (Hayford1924.A * Lcc.F), 1.0 / Lcc.N);
        var theta = Math.Atan2(dx, dy);
        var e = Hayford1924.E;
        var lat = Math.PI / 2 - 2 * Math.Atan(t);
        for (var i = 0; i < 15; i++)
        {
            var esin = e * Math.Sin(lat);
            lat = Math.PI / 2 - 2 * Math.Atan(t * Math.Pow((1 - esin) / (1 + esin), e / 2));
        }

        return (lat, theta / Lcc.N + Lon0);
    }

    private readonly record struct Vector3(double X, double Y, double Z);

    private sealed record Ellipsoid(double A, double Flattening)
    {
        public double E2 { get; } = Flattening * (2.0 - Flattening);
        public double E => Math.Sqrt(E2);
    }

    private sealed record LccConstants(Ellipsoid Ellipsoid, double N, double F)
    {
        public static LccConstants Create(Ellipsoid e, double lat1, double lat2)
        {
            var m1 = M(e, lat1);
            var m2 = M(e, lat2);
            var t1 = TOf(e, lat1);
            var t2 = TOf(e, lat2);
            var n = (Math.Log(m1) - Math.Log(m2)) / (Math.Log(t1) - Math.Log(t2));
            return new LccConstants(e, n, m1 / (n * Math.Pow(t1, n)));
        }

        public double T(double lat) => TOf(Ellipsoid, lat);

        private static double M(Ellipsoid e, double lat)
        {
            var sin = Math.Sin(lat);
            return Math.Cos(lat) / Math.Sqrt(1.0 - e.E2 * sin * sin);
        }

        private static double TOf(Ellipsoid e, double lat)
        {
            var esin = e.E * Math.Sin(lat);
            return Math.Tan(Math.PI / 4 - lat / 2) / Math.Pow((1 - esin) / (1 + esin), e.E / 2);
        }
    }
}
