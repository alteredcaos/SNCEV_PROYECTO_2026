using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using NetTopologySuite.IO;

namespace SNCEV.Api.Services.Geografia;

public sealed class GeografiaService : IGeografiaService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeografiaService> _logger;

    private readonly STRtree<AgebPoligono> _indice = new();
    private readonly STRtree<MunicipioPoligono> _indiceMunicipal = new();

    // Acceso directo por CVEGEO para exponer un polígono individual
    // sin volver a leer los GeoJSON en cada petición.
    private readonly Dictionary<string, AgebPoligono> _agebsPorCvegeo =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly GeometryFactory _geometryFactory =
        new(new PrecisionModel(), 4326);

    private readonly string _connectionString;

    public GeografiaService(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<GeografiaService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        _connectionString =
            _configuration.GetConnectionString("SqlServerConnection")
            ?? throw new InvalidOperationException(
                "No está configurada ConnectionStrings:SNCEV.");

        var carpetaGeo = Path.Combine(
            environment.ContentRootPath,
            "Data",
            "Geo");

        var archivos = new[]
        {
            "AgebsCDMX.json",
            "AgebsEdomex.json"
        };

        foreach (var archivo in archivos)
        {
            var ruta = Path.Combine(carpetaGeo, archivo);

            if (!File.Exists(ruta))
            {
                _logger.LogWarning(
                    "No se encontró la cartografía: {Archivo}",
                    archivo);
                continue;
            }

            CargarAgeb(ruta);
        }

        if (_agebsPorCvegeo.Count == 0)
            throw new InvalidOperationException(
                "No se cargó ningún polígono AGEB. Comprueba Data/Geo.");

        // Capa municipal independiente: CVEGEO de cinco caracteres.
        var rutaMunicipios = Path.Combine(carpetaGeo, "MunicipiosEdomex.json");
        if (File.Exists(rutaMunicipios))
            CargarMunicipios(rutaMunicipios);
        else
            _logger.LogWarning("No se encontró la cartografía municipal: {Ruta}", rutaMunicipios);

        // Construir ambos índices una sola vez.
        _indice.Build();
        _indiceMunicipal.Build();

        _logger.LogInformation(
            "Total de AGEB cargadas en índice espacial: {Cantidad}",
            _agebsPorCvegeo.Count);
    }

    // ============================================================
    // CARGA DE CADA ARCHIVO GEOJSON
    // ============================================================

    private void CargarAgeb(string ruta)
    {
        if (!File.Exists(ruta))
        {
            throw new FileNotFoundException(
                "No se encontró el archivo GeoJSON de AGEB.",
                ruta);
        }

        _logger.LogInformation(
            "Cargando AGEB desde {Ruta}",
            ruta);

        var json = File.ReadAllText(ruta);

        using var document =
            JsonDocument.Parse(json);

        var features =
            document.RootElement.GetProperty("features");

        var reader = new GeoJsonReader();

        var cargadas = 0;

        foreach (var feature in features.EnumerateArray())
        {
            var properties =
                feature.GetProperty("properties");

            var geometryElement =
                feature.GetProperty("geometry");

            var cvegeo =
                ObtenerTexto(properties, "CVEGEO");

            var cveEnt =
                ObtenerTexto(properties, "CVE_ENT");

            var cveMun =
                ObtenerTexto(properties, "CVE_MUN");

            var cveLoc =
                ObtenerTexto(properties, "CVE_LOC");

            var cveAgeb =
                ObtenerTexto(properties, "CVE_AGEB");

            if (string.IsNullOrWhiteSpace(cvegeo))
                continue;

            var geometryJson =
                geometryElement.GetRawText();

            var geometry =
                reader.Read<Geometry>(geometryJson);

            if (geometry is null || geometry.IsEmpty)
                continue;

            var item = new AgebPoligono
            {
                Cvegeo = cvegeo,
                CveEnt = cveEnt,
                CveMun = cveMun,
                CveLoc = cveLoc,
                CveAgeb = cveAgeb,
                Geometry = geometry
            };

            _indice.Insert(
                geometry.EnvelopeInternal,
                item);

            _agebsPorCvegeo[item.Cvegeo] = item;

            cargadas++;
        }

        _logger.LogInformation(
            "AGEB cargadas desde archivo: {Cantidad}",
            cargadas);
    }

    // Cartografía municipal, separada de la cartografía AGEB.
    private void CargarMunicipios(string ruta)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ruta));
        var reader = new GeoJsonReader();
        var cantidad = 0;

        foreach (var feature in document.RootElement.GetProperty("features").EnumerateArray())
        {
            var props = feature.GetProperty("properties");
            var entidad = ObtenerTexto(props, "CVE_ENT");
            var municipio = ObtenerTexto(props, "CVE_MUN");
            var cvegeo = ObtenerTexto(props, "CVEGEO");
            if (entidad != "15" || municipio.Length != 3 ||
                cvegeo != entidad + municipio)
                continue;

            var geometria = reader.Read<Geometry>(feature.GetProperty("geometry").GetRawText());
            if (geometria is null || geometria.IsEmpty)
                continue;

            var item = new MunicipioPoligono
            {
                EstadoClave = entidad,
                MunicipioClave = municipio,
                Geometry = geometria
            };
            _indiceMunicipal.Insert(geometria.EnvelopeInternal, item);
            cantidad++;
        }

        _logger.LogInformation("Polígonos municipales de Edomex cargados: {Cantidad}", cantidad);
    }

    private static string ObtenerTexto(
        JsonElement element,
        string propiedad)
    {
        if (!element.TryGetProperty(
                propiedad,
                out var valor))
        {
            return "";
        }

        return valor.ValueKind switch
        {
            JsonValueKind.String =>
                valor.GetString()?.Trim() ?? "",

            JsonValueKind.Number =>
                valor.ToString(),

            _ => ""
        };
    }

    // ============================================================
    // GEOJSON DE UNA AGEB POR CVEGEO
    // ============================================================

    public string? ObtenerAgebGeoJson(string cvegeo)
    {
        if (string.IsNullOrWhiteSpace(cvegeo))
            return null;

        cvegeo = cvegeo.Trim();

        if (!_agebsPorCvegeo.TryGetValue(cvegeo, out var ageb))
            return null;

        /*
         * GeoJsonWriter serializa únicamente la geometría.
         * Después la envolvemos como un GeoJSON Feature para que
         * React-Leaflet pueda consumirlo directamente con <GeoJSON />.
         */
        var writer = new GeoJsonWriter();
        var geometryJson = writer.Write(ageb.Geometry);

        using var geometryDocument =
            JsonDocument.Parse(geometryJson);

        var feature = new
        {
            type = "Feature",
            properties = new
            {
                cvegeo = ageb.Cvegeo,
                cveEnt = ageb.CveEnt,
                cveMun = ageb.CveMun,
                cveLoc = ageb.CveLoc,
                cveAgeb = ageb.CveAgeb
            },
            geometry = geometryDocument.RootElement.Clone()
        };

        return JsonSerializer.Serialize(feature);
    }

    // ============================================================
    // RESOLUCIÓN
    // ============================================================

    public async Task<ResolucionGeograficaDto> ResolverAsync(
        double latitud,
        double longitud,
        int limiteClues = 5,
        CancellationToken cancellationToken = default)
    {
        ValidarCoordenadas(
            latitud,
            longitud);

        limiteClues =
            Math.Clamp(limiteClues, 1, 20);

        /*
         * GeoJSON / NTS:
         *
         * X = longitud
         * Y = latitud
         *
         * Esto es importante; no invertirlos.
         */
        var punto =
            _geometryFactory.CreatePoint(
                new Coordinate(
                    longitud,
                    latitud));

        var candidatos =
            _indice.Query(
                punto.EnvelopeInternal);

        AgebPoligono? encontrada = null;

        foreach (var candidata in candidatos)
        {
            /*
             * Covers() incluye puntos exactamente
             * sobre el borde del polígono.
             */
            if (candidata.Geometry.Covers(punto))
            {
                encontrada = candidata;
                break;
            }
        }

        await using var connection =
            new SqlConnection(
                _connectionString);

        await connection.OpenAsync(
            cancellationToken);

        if (encontrada is null)
        {
            // Si no hay AGEB, intentar resolver entidad y municipio
            // con el índice municipal, sin inventar una clave AGEB.
            var municipioPoligono = _indiceMunicipal
                .Query(punto.EnvelopeInternal)
                .FirstOrDefault(x => x.Geometry.Covers(punto));

            EntidadGeoDto? entidadMunicipal = null;
            MunicipioGeoDto? municipioMunicipal = null;
            if (municipioPoligono is not null)
            {
                const string sqlMunicipio = """
                    SELECT e.clave_inegi AS EstadoClave,
                           e.nombre AS EstadoNombre,
                           m.clave_inegi AS MunicipioClaveInegi,
                           m.nombre AS MunicipioNombre
                    FROM sncev_geo.municipio m
                    INNER JOIN sncev_geo.estado e
                        ON e.clave_inegi = @EstadoClave
                    WHERE m.clave_inegi = @ClaveCompleta
                    """;

                var datosMunicipio = await connection.QuerySingleOrDefaultAsync<MunicipioSql>(
                    new CommandDefinition(sqlMunicipio,
                        new
                        {
                            municipioPoligono.EstadoClave,
                            ClaveCompleta = municipioPoligono.EstadoClave + municipioPoligono.MunicipioClave
                        },
                        cancellationToken: cancellationToken));

                if (datosMunicipio is not null)
                {
                    entidadMunicipal = new EntidadGeoDto
                    {
                        Clave = datosMunicipio.EstadoClave,
                        Nombre = datosMunicipio.EstadoNombre
                    };
                    municipioMunicipal = new MunicipioGeoDto
                    {
                        Clave = municipioPoligono.MunicipioClave,
                        Nombre = datosMunicipio.MunicipioNombre
                    };
                }
            }

            var cluesSinAgeb = await ObtenerCluesCercanasAsync(
      connection,
      latitud,
      longitud,
      limiteClues,
      cancellationToken);

            return new ResolucionGeograficaDto
            {
                Coordenada = new CoordenadaDto
                {
                    Latitud = latitud,
                    Longitud = longitud
                },

                AgebResuelta = false,
                Entidad = entidadMunicipal,
                Municipio = municipioMunicipal,

                Motivo = municipioMunicipal is null
                    ? "La coordenada no tiene cobertura AGEB ni municipio validado en el catálogo."
                    : "Municipio identificado; no existe cobertura AGEB para esta coordenada.",

                CluesCercanas =
                    cluesSinAgeb
            };
        }

        // ========================================================
        // Confirmamos el AGEB contra SQL Server.
        // ========================================================

        const string sqlAgeb = """
            SELECT
                a.cvegeo AS Cvegeo,
                a.cve_ageb AS CveAgeb,
                a.cve_loc AS CveLoc,

                a.estado_clave AS EstadoClave,

                m.clave_inegi AS MunicipioClaveInegi,
                m.nombre AS MunicipioNombre,

                e.nombre AS EstadoNombre

            FROM sncev_geo.ageb a

            INNER JOIN sncev_geo.municipio m
                ON m.id = a.municipio_id

            INNER JOIN sncev_geo.estado e
                ON e.clave_inegi = a.estado_clave

            WHERE a.cvegeo = @Cvegeo
              AND a.tiene_geometria = 1;
            """;

        var datosAgeb =
            await connection.QuerySingleOrDefaultAsync<AgebSql>(
                new CommandDefinition(
                    sqlAgeb,
                    new
                    {
                        encontrada.Cvegeo
                    },
                    cancellationToken:
                        cancellationToken));

        if (datosAgeb is null)
        {
            return new ResolucionGeograficaDto
            {
                Coordenada = new CoordenadaDto
                {
                    Latitud = latitud,
                    Longitud = longitud
                },

                AgebResuelta = false,

                Motivo =
                    "El polígono fue localizado, pero el AGEB " +
                    "no existe en el catálogo SNCEV."
            };
        }

        /*
         * Las CLUES se sugieren exclusivamente por cercanía geográfica.
         * Pueden estar en otro municipio/alcaldía.
         *
         * Esto NO significa adscripción administrativa.
         */
        var clues = await ObtenerCluesCercanasAsync(
       connection,
       latitud,
       longitud,
       limiteClues,
       cancellationToken);

        return new ResolucionGeograficaDto
        {
            Coordenada = new CoordenadaDto
            {
                Latitud = latitud,
                Longitud = longitud
            },

            AgebResuelta = true,

            Entidad = new EntidadGeoDto
            {
                Clave =
                    datosAgeb.EstadoClave,

                Nombre =
                    datosAgeb.EstadoNombre
            },

            Municipio = new MunicipioGeoDto
            {
                /*
                 * m.clave_inegi = 09002
                 * pero al cliente devolvemos 002.
                 */
                Clave =
                    encontrada.CveMun,

                Nombre =
                    datosAgeb.MunicipioNombre
            },

            Ageb = new AgebGeoDto
            {
                Cvegeo =
                    datosAgeb.Cvegeo,

                CveAgeb =
                    datosAgeb.CveAgeb,

                CveLoc =
                    datosAgeb.CveLoc
            },

            CluesCercanas = clues
        };
    }

    // ============================================================
    // CLUES CERCANAS
    // ============================================================

    private static async Task<IReadOnlyList<CluesCercanaDto>>
     ObtenerCluesCercanasAsync(
         SqlConnection connection,
         double latitud,
         double longitud,
         int limite,
         CancellationToken cancellationToken)
    {
        const double radioGrados = 0.15;

        const string sql = """
SELECT
    es.clues AS Clues,
    es.nombre_unidad AS Nombre,
    es.nombre_institucion AS Institucion,
    es.estatus_operacion AS Estatus,
    CAST(es.latitud AS float) AS Latitud,
    CAST(es.longitud AS float) AS Longitud
FROM sncev_geo.establecimiento_salud es
INNER JOIN sncev_geo.establecimiento_configuracion cfg
    ON cfg.clues = es.clues
   AND cfg.candidato_vacunacion = 1
WHERE es.seleccionable = 1
  AND es.latitud IS NOT NULL
  AND es.longitud IS NOT NULL
  AND es.latitud BETWEEN @Latitud - @Radio
                      AND @Latitud + @Radio
  AND es.longitud BETWEEN @Longitud - @Radio
                       AND @Longitud + @Radio;
""";

        var candidatos = await connection.QueryAsync<CluesSql>(
            new CommandDefinition(
                sql,
                new
                {
                    Latitud = latitud,
                    Longitud = longitud,
                    Radio = radioGrados
                },
                cancellationToken: cancellationToken));

        return candidatos
            .Select(x =>
            {
                var distancia = CalcularDistanciaKm(
                    latitud,
                    longitud,
                    x.Latitud,
                    x.Longitud);

                return new CluesCercanaDto
                {
                    Clues = x.Clues,
                    Nombre = x.Nombre ?? "",
                    Institucion = x.Institucion,
                    Estatus = x.Estatus,
                    Latitud = x.Latitud,
                    Longitud = x.Longitud,
                    DistanciaKm = Math.Round(distancia, 3)
                };
            })
            .OrderBy(x => x.DistanciaKm)
            .Take(limite)
            .ToArray();
    }

    // ============================================================
    // HAVERSINE
    // ============================================================

    private static double CalcularDistanciaKm(
        double lat1,
        double lon1,
        double lat2,
        double lon2)
    {
        const double radioTierraKm =
            6371.0088;

        var dLat =
            GradosARadianes(
                lat2 - lat1);

        var dLon =
            GradosARadianes(
                lon2 - lon1);

        var rLat1 =
            GradosARadianes(lat1);

        var rLat2 =
            GradosARadianes(lat2);

        var a =
            Math.Sin(dLat / 2)
            * Math.Sin(dLat / 2)
            +
            Math.Cos(rLat1)
            * Math.Cos(rLat2)
            * Math.Sin(dLon / 2)
            * Math.Sin(dLon / 2);

        var c =
            2 * Math.Atan2(
                Math.Sqrt(a),
                Math.Sqrt(1 - a));

        return radioTierraKm * c;
    }

    private static double GradosARadianes(
        double grados)
    {
        return grados *
               Math.PI /
               180.0;
    }

    private static void ValidarCoordenadas(
        double latitud,
        double longitud)
    {
        if (!double.IsFinite(latitud) || latitud is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(
                nameof(latitud));
        }

        if (!double.IsFinite(longitud) || longitud is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(
                nameof(longitud));
        }
    }

    // ============================================================
    // MODELOS INTERNOS
    // ============================================================

    private sealed class MunicipioPoligono
    {
        public string EstadoClave { get; init; } = "";
        public string MunicipioClave { get; init; } = "";
        public Geometry Geometry { get; init; } = default!;
    }

    private sealed class MunicipioSql
    {
        public string EstadoClave { get; init; } = "";
        public string EstadoNombre { get; init; } = "";
        public string MunicipioClaveInegi { get; init; } = "";
        public string MunicipioNombre { get; init; } = "";
    }

    private sealed class AgebPoligono
    {
        public string Cvegeo { get; init; } = "";
        public string CveEnt { get; init; } = "";
        public string CveMun { get; init; } = "";
        public string CveLoc { get; init; } = "";
        public string CveAgeb { get; init; } = "";

        public Geometry Geometry { get; init; } = default!;
    }

    private sealed class AgebSql
    {
        public string Cvegeo { get; init; } = "";
        public string CveAgeb { get; init; } = "";
        public string CveLoc { get; init; } = "";

        public string EstadoClave { get; init; } = "";
        public string EstadoNombre { get; init; } = "";

        public string MunicipioClaveInegi { get; init; } = "";
        public string MunicipioNombre { get; init; } = "";
    }

    private sealed class CluesSql
    {
        public string Clues { get; init; } = "";
        public string? Nombre { get; init; }
        public string? Institucion { get; init; }
        public string? Estatus { get; init; }

        public double Latitud { get; init; }
        public double Longitud { get; init; }
    }
}