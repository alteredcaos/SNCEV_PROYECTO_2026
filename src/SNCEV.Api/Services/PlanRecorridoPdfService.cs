using Dapper;
using Microsoft.Data.SqlClient;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace SNCEV.Api.Services;

public sealed class PlanRecorridoPdfService
{
    private readonly IConfiguration _config;

    public PlanRecorridoPdfService(IConfiguration config)
    {
        _config = config;
    }

    private string GetConnectionString() =>
        _config.GetConnectionString("SqlServerConnection")
        ?? throw new InvalidOperationException(
            "No se encontró la cadena de conexión 'SqlServerConnection'.");

    public async Task<PlanRecorridoPdfResultado?> GenerarAsync(
        DateTime fecha,
        string clues,
        CancellationToken cancellationToken = default)
    {
        fecha = fecha.Date;
        var hasta = fecha.AddDays(1);

        await using var conn =
            new SqlConnection(GetConnectionString());

        await conn.OpenAsync(cancellationToken);

        // =====================================================
        // Citas del día
        // =====================================================

        var citas = (
            await conn.QueryAsync<PlanRecorridoItem>(
                new CommandDefinition(
                    """
                    SELECT
                        c.id AS CitaId,
                        c.fecha_hora AS FechaHora,
                        c.estado AS EstadoCita,
                        c.clues AS Clues,

                        p.id AS PacienteId,

                        LTRIM(RTRIM(CONCAT(
                            p.nombre, ' ',
                            p.primer_apellido, ' ',
                            COALESCE(p.segundo_apellido, '')
                        ))) AS Paciente,

                        p.fecha_nacimiento AS FechaNacimiento,
                        p.sexo AS Sexo,

                        vc.nombre_generico AS Vacuna,
                        c.numero_dosis AS NumeroDosis,

                        d.ageb_clave AS AgebClave,
                        d.cvegeo AS Cvegeo,
                        d.localidad AS Localidad,
                        d.colonia AS Colonia,
                        d.codigo_postal AS CodigoPostal,
                        d.calle AS Calle,
                        d.numero_exterior AS NumeroExterior,
                        d.numero_interior AS NumeroInterior,
                        d.direccion_texto AS DireccionTexto,

                        m.nombre AS Municipio

                    FROM sncev.cita_vacunacion c

                    INNER JOIN sncev.paciente p
                        ON p.id = c.paciente_id

                    INNER JOIN sncev.vacuna_catalogo vc
                        ON vc.id = c.vacuna_id

                    LEFT JOIN sncev.domicilio_paciente d
                        ON d.paciente_id = p.id
                       AND d.vigente = 1

                    LEFT JOIN sncev_geo.municipio m
                        ON m.id = d.municipio_id

                    WHERE c.fecha_hora >= @Fecha
                      AND c.fecha_hora < @Hasta
                      AND c.clues = @Clues
                      AND c.estado IN ('PROGRAMADA', 'CONFIRMADA')
                      AND p.eliminado = 0

                    ORDER BY
                        CASE
                            WHEN d.ageb_clave IS NULL THEN 1
                            ELSE 0
                        END,
                        d.ageb_clave,
                        c.fecha_hora,
                        p.primer_apellido,
                        p.nombre;
                    """,
                    new
                    {
                        Fecha = fecha,
                        Hasta = hasta,
                        Clues = clues
                    },
                    cancellationToken: cancellationToken)))
            .ToList();

        if (citas.Count == 0)
            return null;

        // =====================================================
        // PDF
        // =====================================================

        var document = new Document();

        document.Info.Title =
            $"Plan de recorrido SNCEV {fecha:yyyy-MM-dd}";

        document.Info.Subject =
            $"Agenda operativa de vacunación - {clues}";

        var normal =
            document.Styles[StyleNames.Normal];

        normal.Font.Name = "Arial";
        normal.Font.Size = 8.5;

        var section = document.AddSection();

        section.PageSetup.PageFormat =
            PageFormat.A4;

        section.PageSetup.Orientation =
            Orientation.Landscape;

        section.PageSetup.TopMargin =
            Unit.FromCentimeter(1.2);

        section.PageSetup.BottomMargin =
            Unit.FromCentimeter(1.2);

        section.PageSetup.LeftMargin =
            Unit.FromCentimeter(1.2);

        section.PageSetup.RightMargin =
            Unit.FromCentimeter(1.2);


        // =====================================================
        // Encabezado
        // =====================================================

        var titulo = section.AddParagraph();

        titulo.AddFormattedText(
            "SNCEV — PLAN DE RECORRIDO",
            TextFormat.Bold);

        titulo.Format.Font.Size = 16;
        titulo.Format.SpaceAfter =
            Unit.FromCentimeter(0.15);


        var datos = section.AddParagraph();

        datos.AddFormattedText(
            $"Fecha: {fecha:dd/MM/yyyy}",
            TextFormat.Bold);

        datos.AddText(
            $"     CLUES: {clues}");

        datos.AddText(
            $"     Generado: {DateTime.Now:dd/MM/yyyy HH:mm}");

        datos.Format.SpaceAfter =
            Unit.FromCentimeter(0.3);


        // =====================================================
        // Resumen
        // =====================================================

        var agebs = citas
            .Where(x =>
                !string.IsNullOrWhiteSpace(x.AgebClave))
            .Select(x => x.AgebClave!)
            .Distinct()
            .Count();

        var pacientes = citas
            .Select(x => x.PacienteId)
            .Distinct()
            .Count();

        var resumen = section.AddParagraph();

        resumen.AddFormattedText(
            "Resumen: ",
            TextFormat.Bold);

        resumen.AddText(
            $"{pacientes} paciente(s) · " +
            $"{citas.Count} dosis programada(s) · " +
            $"{agebs} AGEB");

        resumen.Format.SpaceAfter =
            Unit.FromCentimeter(0.5);


        // =====================================================
        // Agrupar por AGEB
        // =====================================================

        var grupos = citas
            .GroupBy(x =>
                string.IsNullOrWhiteSpace(x.AgebClave)
                    ? "SIN AGEB"
                    : x.AgebClave!)
            .OrderBy(x =>
                x.Key == "SIN AGEB" ? "ZZZZZZ" : x.Key);


        foreach (var grupo in grupos)
        {
            var primero = grupo.First();

            var encabezado =
                section.AddParagraph();

            encabezado.AddFormattedText(
                $"AGEB {grupo.Key}",
                TextFormat.Bold);

            if (!string.IsNullOrWhiteSpace(
                    primero.Municipio))
            {
                encabezado.AddText(
                    $" — {primero.Municipio}");
            }

            if (!string.IsNullOrWhiteSpace(
                    primero.Colonia))
            {
                encabezado.AddText(
                    $" — {primero.Colonia}");
            }

            encabezado.Format.Font.Size = 11;

            encabezado.Format.SpaceBefore =
                Unit.FromCentimeter(0.35);

            encabezado.Format.SpaceAfter =
                Unit.FromCentimeter(0.15);


            var tabla = section.AddTable();
            tabla.Borders.Width = 0.4;
            tabla.Rows.LeftIndent = 0;

            tabla.AddColumn(Unit.FromCentimeter(1.25)); // Hora
            tabla.AddColumn(Unit.FromCentimeter(4.25)); // Paciente
            tabla.AddColumn(Unit.FromCentimeter(4.00)); // Vacuna / dosis
            tabla.AddColumn(Unit.FromCentimeter(7.00)); // Domicilio
            tabla.AddColumn(Unit.FromCentimeter(2.00)); // Estado
            tabla.AddColumn(Unit.FromCentimeter(3.20)); // Resultado
            tabla.AddColumn(Unit.FromCentimeter(4.00)); // Observaciones

            var cabecera = tabla.AddRow();
            cabecera.Shading.Color = Colors.LightGray;
            cabecera.Format.Font.Bold = true;
            cabecera.Format.Font.Size = 7.5;
            cabecera.HeadingFormat = true;

            cabecera.Cells[0].AddParagraph("Hora");
            cabecera.Cells[1].AddParagraph("Paciente");
            cabecera.Cells[2].AddParagraph("Vacuna / dosis");
            cabecera.Cells[3].AddParagraph("Domicilio");
            cabecera.Cells[4].AddParagraph("Cita");
            cabecera.Cells[5].AddParagraph("Resultado");
            cabecera.Cells[6].AddParagraph("Observaciones");

            foreach (var item in grupo)
            {
                var row = tabla.AddRow();
                row.Format.Font.Size = 7.5;
                row.Height = Unit.FromCentimeter(1.15);
                row.HeightRule = RowHeightRule.AtLeast;

                row.Cells[0].AddParagraph(item.FechaHora.ToString("HH:mm"));
                row.Cells[1].AddParagraph(item.Paciente);
                row.Cells[2].AddParagraph($"{item.Vacuna}\nDosis {item.NumeroDosis}");
                row.Cells[3].AddParagraph(ConstruirDomicilio(item));
                row.Cells[4].AddParagraph(item.EstadoCita);
                row.Cells[5].AddParagraph("[ ] VAC\n[ ] NL\n[ ] REP\n[ ] OTRO");
                row.Cells[6].AddParagraph("____________________\n____________________");
            }

            var leyenda = section.AddParagraph();
            leyenda.Format.Font.Size = 7;
            leyenda.Format.SpaceAfter = Unit.FromCentimeter(0.25);
            leyenda.AddFormattedText("Resultado: ", TextFormat.Bold);
            leyenda.AddText(
                "VAC = Vacunado · NL = No localizado · REP = Reprogramar · OTRO = Otro resultado");
        }


        // =====================================================
        // Cierre operativo del recorrido
        // =====================================================

        var cierreTitulo = section.AddParagraph();
        cierreTitulo.Format.SpaceBefore = Unit.FromCentimeter(0.6);
        cierreTitulo.Format.SpaceAfter = Unit.FromCentimeter(0.25);
        cierreTitulo.AddFormattedText("CIERRE DEL RECORRIDO", TextFormat.Bold);
        cierreTitulo.Format.Font.Size = 11;

        var cierre = section.AddTable();
        cierre.Borders.Width = 0.5;
        cierre.AddColumn(Unit.FromCentimeter(6.5));
        cierre.AddColumn(Unit.FromCentimeter(6.5));
        cierre.AddColumn(Unit.FromCentimeter(6.5));
        cierre.AddColumn(Unit.FromCentimeter(6.5));

        var r1 = cierre.AddRow();
        r1.Cells[0].AddParagraph($"Programados: {citas.Count}");
        r1.Cells[1].AddParagraph("Visitados: __________");
        r1.Cells[2].AddParagraph("Vacunados: __________");
        r1.Cells[3].AddParagraph("No localizados: __________");

        var r2 = cierre.AddRow();
        r2.Cells[0].AddParagraph("Reprogramados: __________");
        r2.Cells[1].AddParagraph("Otros: __________");
        r2.Cells[2].AddParagraph("Hora inicio: __________");
        r2.Cells[3].AddParagraph("Hora término: __________");

        var responsable = section.AddParagraph();
        responsable.Format.SpaceBefore = Unit.FromCentimeter(0.5);
        responsable.AddText(
            "Responsable del recorrido: __________________________________________");
        responsable.AddText(
            "        Firma: ____________________________");

        var observaciones = section.AddParagraph();
        observaciones.Format.SpaceBefore = Unit.FromCentimeter(0.45);
        observaciones.AddFormattedText("Observaciones generales", TextFormat.Bold);
        section.AddParagraph("____________________________________________________________________________________________________________");
        section.AddParagraph("____________________________________________________________________________________________________________");
        section.AddParagraph("____________________________________________________________________________________________________________");


        // =====================================================
        // Aviso
        // =====================================================

        var aviso = section.AddParagraph();

        aviso.Format.SpaceBefore =
            Unit.FromCentimeter(0.4);

        aviso.Format.Font.Size = 7;

        aviso.AddText(
            "Documento operativo de uso interno. " +
            "Contiene información nominal. " +
            "Debe manejarse conforme a los controles " +
            "de acceso y protección de datos del SNCEV.");


        // =====================================================
        // Render
        // =====================================================

        var renderer =
            new PdfDocumentRenderer
            {
                Document = document
            };

        renderer.RenderDocument();

        using var stream =
            new MemoryStream();

        renderer.PdfDocument.Save(stream);

        var bytes = stream.ToArray();

        var nombreArchivo =
            $"SNCEV_Recorrido_{clues}_{fecha:yyyyMMdd}.pdf";

        return new PlanRecorridoPdfResultado(
            bytes,
            nombreArchivo,
            citas.Count,
            pacientes,
            agebs);
    }


    // =========================================================
    // Domicilio
    // =========================================================

    private static string ConstruirDomicilio(
        PlanRecorridoItem item)
    {
        if (!string.IsNullOrWhiteSpace(
                item.DireccionTexto))
        {
            return item.DireccionTexto;
        }

        var partes = new List<string>();

        if (!string.IsNullOrWhiteSpace(item.Calle))
            partes.Add(item.Calle);

        if (!string.IsNullOrWhiteSpace(
                item.NumeroExterior))
        {
            partes.Add($"No. {item.NumeroExterior}");
        }

        if (!string.IsNullOrWhiteSpace(
                item.NumeroInterior))
        {
            partes.Add($"Int. {item.NumeroInterior}");
        }

        if (!string.IsNullOrWhiteSpace(item.Colonia))
            partes.Add(item.Colonia);

        if (!string.IsNullOrWhiteSpace(
                item.CodigoPostal))
        {
            partes.Add($"C.P. {item.CodigoPostal}");
        }

        if (!string.IsNullOrWhiteSpace(
                item.Localidad))
        {
            partes.Add(item.Localidad);
        }

        return partes.Count > 0
            ? string.Join(", ", partes)
            : "Domicilio no registrado";
    }


    // =========================================================
    // Modelos internos
    // =========================================================

    private sealed class PlanRecorridoItem
    {
        public Guid CitaId { get; set; }

        public DateTime FechaHora { get; set; }

        public string EstadoCita { get; set; } =
            string.Empty;

        public string Clues { get; set; } =
            string.Empty;

        public Guid PacienteId { get; set; }

        public string Paciente { get; set; } =
            string.Empty;

        public DateTime FechaNacimiento { get; set; }

        public string Sexo { get; set; } =
            string.Empty;

        public string Vacuna { get; set; } =
            string.Empty;

        public int NumeroDosis { get; set; }

        public string? AgebClave { get; set; }

        public string? Cvegeo { get; set; }

        public string? Localidad { get; set; }

        public string? Colonia { get; set; }

        public string? CodigoPostal { get; set; }

        public string? Calle { get; set; }

        public string? NumeroExterior { get; set; }

        public string? NumeroInterior { get; set; }

        public string? DireccionTexto { get; set; }

        public string? Municipio { get; set; }
    }
}


public sealed record PlanRecorridoPdfResultado(
    byte[] Contenido,
    string NombreArchivo,
    int TotalCitas,
    int TotalPacientes,
    int TotalAgebs);