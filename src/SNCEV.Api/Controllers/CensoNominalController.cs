using Dapper;
using ClosedXML.Excel;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SNCEV.Api.Services;
using System.Security.Claims;

namespace SNCEV.Api.Controllers;

[ApiController]
[Authorize(Roles = "Administrador,Directivo,Auditor")]
[Route("v1/reportes/censo")]
public sealed class CensoNominalController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly EvaluadorCoberturaNominalService _evaluador;
    public CensoNominalController(IConfiguration config, EvaluadorCoberturaNominalService evaluador)
    { _config = config; _evaluador = evaluador; }

    private string? Scope() => User.FindFirstValue("clues")?.Trim();

    private static bool FechaInvalida(DateTime corte) => corte.Year < 1900 || corte > DateTime.Today;

    // Cohorte y filtros idénticos para COUNT y paginación.
    // CLUES identifica una unidad aplicadora, NO una adscripción.
    private const string CohorteSql = """
        FROM sncev.paciente p
        OUTER APPLY (
            SELECT TOP (1) dp.ageb_clave
            FROM sncev.domicilio_paciente dp
            WHERE dp.paciente_id = p.id AND dp.vigente = 1
            ORDER BY dp.fecha_inicio DESC, dp.fecha_registro DESC, dp.id DESC
        ) d
        OUTER APPLY (
            SELECT TOP (1) e.nombre_unidad, e.nombre_institucion, e.jurisdiccion,
                           e.municipio, e.localidad
            FROM sncev_geo.establecimiento_salud e
            WHERE e.clues = @Scope
            ORDER BY e.fecha_importacion DESC, e.version_catalogo DESC, e.nombre_unidad
        ) unidad
        OUTER APPLY (
            SELECT TOP (1) a.nombre, a.tipo
            FROM sncev_geo.ageb a
            WHERE a.clave = COALESCE(NULLIF(RTRIM(d.ageb_clave), ''), NULLIF(RTRIM(p.ageb_clave), ''))
            ORDER BY a.fecha_actualizacion_geo DESC, a.nombre
        ) geo
        WHERE p.eliminado = 0
          AND p.fecha_creacion < DATEADD(day, 1, @Corte)
          AND EXISTS (
              SELECT 1 FROM sncev.vacuna_aplicada s
              WHERE s.paciente_id = p.id AND s.anulada = 0
                AND s.unidad_medica_clues = @Scope
                AND s.fecha_aplicacion < DATEADD(day, 1, @Corte)
          )
          AND (@Sexo IS NULL OR p.sexo = @Sexo)
          AND (@Cartilla IS NULL OR p.version_cartilla = @Cartilla)
          AND (@Ageb IS NULL OR COALESCE(NULLIF(RTRIM(d.ageb_clave), ''), NULLIF(RTRIM(p.ageb_clave), '')) = @Ageb)
          AND (@EdadMin IS NULL OR
               DATEDIFF(year, p.fecha_nacimiento, @Corte) -
               CASE WHEN DATEADD(year, DATEDIFF(year, p.fecha_nacimiento, @Corte), p.fecha_nacimiento) > @Corte THEN 1 ELSE 0 END >= @EdadMin)
          AND (@EdadMax IS NULL OR
               DATEDIFF(year, p.fecha_nacimiento, @Corte) -
               CASE WHEN DATEADD(year, DATEDIFF(year, p.fecha_nacimiento, @Corte), p.fecha_nacimiento) > @Corte THEN 1 ELSE 0 END <= @EdadMax)
        """;

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] DateTime? fechaCorte, [FromQuery] string? clues,
        [FromQuery] string? buscar, [FromQuery] string? sexo, [FromQuery] string? cartilla,
        [FromQuery] int? edadMin, [FromQuery] int? edadMax, [FromQuery] string? ageb,
        [FromQuery] int pagina = 1, [FromQuery] int tamano = 20,
        CancellationToken ct = default)
    {
        var corte = (fechaCorte ?? DateTime.Today).Date;
        if (FechaInvalida(corte) || pagina < 1 || tamano is < 1 or > 100 || (long)(pagina - 1) * tamano > int.MaxValue)
            return BadRequest(new { message = "Fecha de corte o paginación inválida." });
        if (clues?.Length > 20 || buscar?.Length > 100)
            return BadRequest(new { message = "Filtro demasiado largo." });
        var scope = Scope();
        if (string.IsNullOrWhiteSpace(scope)) return Forbid();
        if (!string.IsNullOrWhiteSpace(clues) && !string.Equals(clues.Trim(), scope, StringComparison.OrdinalIgnoreCase))
            return Forbid();

        sexo = string.IsNullOrWhiteSpace(sexo) ? null : sexo.Trim().ToUpperInvariant();
        if (sexo is not null && sexo != "M" && sexo != "F")
            return BadRequest(new { message = "Sexo inválido (M o F)." });
        if (edadMin is < 0 or > 125 || edadMax is < 0 or > 125 ||
            (edadMin.HasValue && edadMax.HasValue && edadMin > edadMax))
            return BadRequest(new { message = "Rango de edad inválido." });
        if (ageb?.Trim().Length > 13) return BadRequest(new { message = "AGEB demasiado larga." });
        ageb = string.IsNullOrWhiteSpace(ageb) ? null : ageb.Trim();
        // Lista blanca: nunca se aceptan nombres arbitrarios de cartilla.
        string[] cartillas = {
            "Niñas y Niños (0 a 9 años)", "Adolescentes (10 a 19 años)",
            "Mujeres (20 a 59 años)", "Hombres (20 a 59 años)",
            "Personas Mayores (60 años y más)"
        };
        cartilla = string.IsNullOrWhiteSpace(cartilla) ? null : cartilla.Trim();
        if (cartilla is not null && !cartillas.Contains(cartilla, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { message = "Cartilla no reconocida." });
        var q = string.IsNullOrWhiteSpace(buscar) ? null : buscar.Trim();
        var filtroBusqueda = """
            AND (@Buscar IS NULL OR p.nombre LIKE '%' + @Buscar + '%'
                 OR p.primer_apellido LIKE '%' + @Buscar + '%'
                 OR p.segundo_apellido LIKE '%' + @Buscar + '%'
                 OR CONVERT(varchar(36), p.id) = @Buscar)
            """;
        var args = new { Corte = corte, Scope = scope, Buscar = q, Sexo = sexo, Cartilla = cartilla, EdadMin = edadMin, EdadMax = edadMax, Ageb = ageb, Offset = (pagina - 1) * tamano, Tamano = tamano };
        var cs = _config.GetConnectionString("SqlServerConnection")
            ?? throw new InvalidOperationException("Falta SqlServerConnection.");
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync(ct);
        var total = await conn.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT_BIG(*)\n" + CohorteSql + "\n" + filtroBusqueda, args, cancellationToken: ct));
        var filas = await conn.QueryAsync(new CommandDefinition("""
            SELECT p.id AS PacienteId, p.nombre AS Nombre,
                   p.primer_apellido AS PrimerApellido, p.segundo_apellido AS SegundoApellido,
                   p.fecha_nacimiento AS FechaNacimiento, p.sexo AS Sexo,
                   p.version_cartilla AS Cartilla, @Scope AS Clues,
                   unidad.nombre_unidad AS NombreUnidad, unidad.nombre_institucion AS Institucion,
                   unidad.jurisdiccion AS Jurisdiccion, unidad.municipio AS MunicipioUnidad,
                   geo.nombre AS NombreAgeb, geo.tipo AS TipoAgeb,
                   COALESCE(NULLIF(RTRIM(d.ageb_clave), ''), NULLIF(RTRIM(p.ageb_clave), '')) AS Ageb,
                   (SELECT COUNT_BIG(*) FROM sncev.vacuna_aplicada va
                    WHERE va.paciente_id = p.id AND va.anulada = 0
                      AND va.fecha_aplicacion < DATEADD(day, 1, @Corte)) AS AplicacionesDocumentadas
            """ + "\n" + CohorteSql + "\n" + filtroBusqueda + "\n" + """
            ORDER BY p.primer_apellido, p.nombre, p.id
            OFFSET @Offset ROWS FETCH NEXT @Tamano ROWS ONLY;
            """, args, cancellationToken: ct));
        return Ok(new { fechaCorte = corte, clues = scope, pagina, tamano, total, filas, filtros = new { sexo, cartilla, edadMin, edadMax, ageb },
            metodologia = "Cohorte de pacientes con al menos una aplicación no anulada en la CLUES autorizada hasta la fecha de corte. No es un censo de adscripción: excluye pacientes sin aplicaciones en esa CLUES. AplicacionesDocumentadas cuenta antecedentes no anulados del paciente en cualquier CLUES. No certifica cobertura." });
    }

    // Exportación nominal: únicamente Administrador/Directivo/Auditor, siempre limitada a la CLUES del JWT.
    // Incluye TODOS los resultados filtrados (máximo 5000), nunca sólo la página visible.
    [HttpGet("exportar")]
    public async Task<IActionResult> Exportar(
        [FromQuery] string formato, [FromQuery] DateTime? fechaCorte,
        [FromQuery] string? clues, [FromQuery] string? buscar, [FromQuery] string? sexo,
        [FromQuery] string? cartilla, [FromQuery] int? edadMin, [FromQuery] int? edadMax,
        [FromQuery] string? ageb, CancellationToken ct)
    {
        formato = (formato ?? "").Trim().ToLowerInvariant();
        if (formato != "xlsx" && formato != "pdf")
            return BadRequest(new { message = "Formato permitido: xlsx o pdf." });
        var corte = (fechaCorte ?? DateTime.Today).Date;
        if (FechaInvalida(corte)) return BadRequest(new { message = "Fecha de corte inválida." });
        var scope = Scope();
        if (string.IsNullOrWhiteSpace(scope)) return Forbid();
        if (!string.IsNullOrWhiteSpace(clues) &&
            !string.Equals(clues.Trim(), scope, StringComparison.OrdinalIgnoreCase)) return Forbid();
        if (clues?.Length > 20 || buscar?.Length > 100 || ageb?.Trim().Length > 13)
            return BadRequest(new { message = "Filtro demasiado largo." });
        sexo = string.IsNullOrWhiteSpace(sexo) ? null : sexo.Trim().ToUpperInvariant();
        if (sexo is not null && sexo != "M" && sexo != "F")
            return BadRequest(new { message = "Sexo inválido." });
        if (edadMin is < 0 or > 125 || edadMax is < 0 or > 125 ||
            (edadMin.HasValue && edadMax.HasValue && edadMin > edadMax))
            return BadRequest(new { message = "Rango de edad inválido." });
        ageb = string.IsNullOrWhiteSpace(ageb) ? null : ageb.Trim();
        string[] cartillas = {
            "Niñas y Niños (0 a 9 años)", "Adolescentes (10 a 19 años)",
            "Mujeres (20 a 59 años)", "Hombres (20 a 59 años)",
            "Personas Mayores (60 años y más)"
        };
        cartilla = string.IsNullOrWhiteSpace(cartilla) ? null : cartilla.Trim();
        if (cartilla is not null && !cartillas.Contains(cartilla, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { message = "Cartilla no reconocida." });
        var q = string.IsNullOrWhiteSpace(buscar) ? null : buscar.Trim();
        const string filtroBusqueda = """
            AND (@Buscar IS NULL OR p.nombre LIKE '%' + @Buscar + '%'
                OR p.primer_apellido LIKE '%' + @Buscar + '%'
                OR p.segundo_apellido LIKE '%' + @Buscar + '%'
                OR CONVERT(varchar(36), p.id) = @Buscar)
            """;
        var args = new { Corte = corte, Scope = scope, Buscar = q, Sexo = sexo,
            Cartilla = cartilla, EdadMin = edadMin, EdadMax = edadMax, Ageb = ageb };
        var cs = _config.GetConnectionString("SqlServerConnection")
            ?? throw new InvalidOperationException("Falta SqlServerConnection.");
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync(ct);
        var sqlWhere = "\n" + CohorteSql + "\n" + filtroBusqueda + "\n";
        var total = await conn.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT COUNT_BIG(*)\n" + sqlWhere, args, cancellationToken: ct));
        const int maximo = 5000;
        if (total > maximo)
            return StatusCode(413, new { message = $"La consulta contiene {total} pacientes. Refine los filtros para exportar un máximo de {maximo}; no se generó un archivo incompleto." });
        var sql = """
            SELECT p.id AS PacienteId, p.nombre AS Nombre,
                   p.primer_apellido AS PrimerApellido, p.segundo_apellido AS SegundoApellido,
                   p.fecha_nacimiento AS FechaNacimiento, p.sexo AS Sexo,
                   p.version_cartilla AS Cartilla, @Scope AS Clues,
                   unidad.nombre_unidad AS NombreUnidad, unidad.nombre_institucion AS Institucion,
                   unidad.jurisdiccion AS Jurisdiccion, unidad.municipio AS MunicipioUnidad,
                   geo.nombre AS NombreAgeb, geo.tipo AS TipoAgeb,
                   COALESCE(NULLIF(RTRIM(d.ageb_clave), ''), NULLIF(RTRIM(p.ageb_clave), '')) AS Ageb,
                   (SELECT COUNT_BIG(*) FROM sncev.vacuna_aplicada va
                    WHERE va.paciente_id = p.id AND va.anulada = 0
                      AND va.fecha_aplicacion < DATEADD(day, 1, @Corte)) AS AplicacionesDocumentadas
            """ + "\n" + sqlWhere + "\n" + """
            ORDER BY p.primer_apellido, p.nombre, p.id;
            """;
        var filas = (await conn.QueryAsync<FilaExportacion>(new CommandDefinition(
            sql, args, cancellationToken: ct))).ToList();
        var descripcion = $"Corte: {corte:yyyy-MM-dd} | CLUES aplicadora: {scope} | Sexo: {sexo ?? "Todos"} | Cartilla: {cartilla ?? "Todas"} | Edad: {edadMin?.ToString() ?? "*"}-{edadMax?.ToString() ?? "*"} | AGEB: {ageb ?? "Todas"} | Búsqueda: {q ?? "Sin filtro"}";
        var nombre = $"SNCEV_Censo_{scope}_{corte:yyyyMMdd}.{formato}";
        if (formato == "xlsx")
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Censo nominal");
            ws.Cell(1, 1).Value = "SNCEV | Censo nominal de pacientes con aplicaciones";
            ws.Range(1, 1, 1, 16).Merge();
            ws.Range(1, 1, 1, 16).Style.Font.Bold = true;
            ws.Range(1, 1, 1, 16).Style.Font.FontSize = 15;
            ws.Cell(2, 1).Value = descripcion;
            ws.Range(2, 1, 2, 16).Merge();
            ws.Cell(3, 1).Value = "No es censo de adscripción ni certificación de cobertura. Aplicaciones: antecedentes no anulados en cualquier CLUES hasta el corte.";
            ws.Range(3, 1, 3, 16).Merge();
            string[] encabezados = { "ID paciente", "Nombre", "Primer apellido", "Segundo apellido", "Nacimiento", "Sexo", "Cartilla", "CLUES aplicadora", "AGEB", "Aplicaciones documentadas", "Nombre unidad", "Institución", "Jurisdicción", "Municipio unidad", "Nombre AGEB", "Tipo AGEB" };
            for (var j = 0; j < encabezados.Length; j++) ws.Cell(5, j + 1).Value = encabezados[j];
            for (var i = 0; i < filas.Count; i++)
            {
                var r = filas[i]; var n = i + 6;
                ws.Cell(n, 1).Value = r.PacienteId.ToString();
                ws.Cell(n, 2).Value = r.Nombre ?? "";
                ws.Cell(n, 3).Value = r.PrimerApellido ?? "";
                ws.Cell(n, 4).Value = r.SegundoApellido ?? "";
                ws.Cell(n, 5).Value = r.FechaNacimiento;
                ws.Cell(n, 5).Style.DateFormat.Format = "yyyy-mm-dd";
                ws.Cell(n, 6).Value = r.Sexo ?? "";
                ws.Cell(n, 7).Value = r.Cartilla ?? "";
                ws.Cell(n, 8).Value = r.Clues ?? "";
                ws.Cell(n, 9).Value = r.Ageb ?? "";
                ws.Cell(n, 10).Value = r.AplicacionesDocumentadas;
                ws.Cell(n, 11).Value = r.NombreUnidad ?? "";
                ws.Cell(n, 12).Value = r.Institucion ?? "";
                ws.Cell(n, 13).Value = r.Jurisdiccion ?? "";
                ws.Cell(n, 14).Value = r.MunicipioUnidad ?? "";
                ws.Cell(n, 15).Value = r.NombreAgeb ?? "";
                ws.Cell(n, 16).Value = r.TipoAgeb == "U" ? "Urbana" : r.TipoAgeb == "R" ? "Rural" : r.TipoAgeb ?? "";
            }
            ws.Range(5, 1, 5, 16).Style.Fill.BackgroundColor = XLColor.FromHtml("#173E45");
            ws.Range(5, 1, 5, 16).Style.Font.FontColor = XLColor.White;
            ws.Range(5, 1, 5, 16).Style.Font.Bold = true;
            ws.SheetView.FreezeRows(5);
            ws.Columns(1, 16).AdjustToContents(12, 48);
            if (filas.Count > 0) ws.Range(5, 1, filas.Count + 5, 16).SetAutoFilter();
            using var ms = new MemoryStream(); wb.SaveAs(ms);
            return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
        }
        // Mismo motor PDF que CartillaPdfService: MigraDoc + PDFsharp.
        // No incorpora QR ni folio de cartilla: es un reporte nominal, no una cartilla emitida.
        var pdf = GenerarPdfCenso(filas, descripcion, corte, scope);
        return File(pdf, "application/pdf", nombre);
    }

    private static byte[] GenerarPdfCenso(
        IReadOnlyList<FilaExportacion> filas, string descripcion, DateTime corte, string scope)
    {
        var documento = new Document();
        documento.Info.Title = "SNCEV - Censo nominal de pacientes con aplicaciones";
        documento.Info.Subject = "Reporte nominal por CLUES aplicadora";
        documento.Info.Author = "SNCEV";
        var normal = documento.Styles[StyleNames.Normal];
        normal.Font.Name = "Arial";
        normal.Font.Size = 8;

        var seccion = documento.AddSection();
        seccion.PageSetup.PageFormat = PageFormat.A4;
        seccion.PageSetup.Orientation = Orientation.Landscape;
        seccion.PageSetup.TopMargin = Unit.FromCentimeter(1.4);
        seccion.PageSetup.BottomMargin = Unit.FromCentimeter(1.4);
        seccion.PageSetup.LeftMargin = Unit.FromCentimeter(1.2);
        seccion.PageSetup.RightMargin = Unit.FromCentimeter(1.2);

        var titulo = seccion.AddParagraph("SNCEV | Censo nominal de pacientes con aplicaciones");
        titulo.Format.Font.Size = 14;
        titulo.Format.Font.Bold = true;
        titulo.Format.SpaceAfter = Unit.FromPoint(6);

        var filtros = seccion.AddParagraph(descripcion);
        filtros.Format.Font.Size = 8;
        filtros.Format.SpaceAfter = Unit.FromPoint(5);
        var aviso = seccion.AddParagraph(
            "No es un censo de adscripción ni certificación de cobertura. " +
            "Incluye pacientes con aplicación no anulada en la CLUES autorizada. " +
            "Las aplicaciones documentadas incluyen antecedentes de cualquier CLUES hasta el corte.");
        aviso.Format.Font.Size = 7;
        aviso.Format.SpaceAfter = Unit.FromPoint(8);

        var nombreUnidad = filas.FirstOrDefault()?.NombreUnidad;
        var jurisdiccion = filas.FirstOrDefault()?.Jurisdiccion;
        var referenciaUnidad = string.IsNullOrWhiteSpace(nombreUnidad) ? scope : $"{nombreUnidad} ({scope})";
        var resumen = seccion.AddParagraph($"Registros: {filas.Count:N0} | Fecha de corte: {corte:dd/MM/yyyy} | Unidad aplicadora: {referenciaUnidad} | Jurisdicción: {jurisdiccion ?? "No catalogada"}");
        resumen.Format.Font.Bold = true;
        resumen.Format.SpaceAfter = Unit.FromPoint(7);

        var tabla = seccion.AddTable();
        tabla.Borders.Width = Unit.FromPoint(0.35);
        tabla.Borders.Color = Colors.LightGray;
        tabla.Rows.LeftIndent = 0;
        foreach (var ancho in new[] { 5.4, 2.1, 1.0, 5.0, 2.4, 2.9, 1.7 })
            tabla.AddColumn(Unit.FromCentimeter(ancho));

        var encabezado = tabla.AddRow();
        encabezado.HeadingFormat = true;
        encabezado.Format.Font.Bold = true;
        encabezado.Shading.Color = Colors.LightGray;
        var etiquetas = new[] { "Paciente", "Nacimiento", "Sexo", "Cartilla", "CLUES", "AGEB", "Aplicaciones" };
        for (int j = 0; j < etiquetas.Length; j++)
        {
            encabezado.Cells[j].AddParagraph(etiquetas[j]);
            encabezado.Cells[j].VerticalAlignment = VerticalAlignment.Center;
        }

        foreach (var r in filas)
        {
            var fila = tabla.AddRow();
            fila.Format.Font.Size = 7;
            var valores = new[]
            {
                string.Join(" ", new[] { r.Nombre, r.PrimerApellido, r.SegundoApellido }
                    .Where(v => !string.IsNullOrWhiteSpace(v))),
                r.FechaNacimiento.ToString("dd/MM/yyyy"), r.Sexo ?? "",
                r.Cartilla ?? "", r.Clues ?? "", string.IsNullOrWhiteSpace(r.NombreAgeb) ? (r.Ageb ?? "") : $"{r.NombreAgeb} ({(r.TipoAgeb == "U" ? "Urbana" : r.TipoAgeb == "R" ? "Rural" : "Tipo no indicado")}) - {r.Ageb}",
                r.AplicacionesDocumentadas.ToString()
            };
            for (int j = 0; j < valores.Length; j++)
            {
                fila.Cells[j].AddParagraph(valores[j]);
                fila.Cells[j].VerticalAlignment = VerticalAlignment.Center;
            }
        }

        var pie = seccion.Footers.Primary.AddParagraph();
        pie.Format.Font.Size = 7;
        pie.Format.Alignment = ParagraphAlignment.Right;
        pie.AddText("SNCEV | Documento nominal de uso restringido | Página ");
        pie.AddPageField();
        pie.AddText(" de ");
        pie.AddNumPagesField();

        var renderer = new PdfDocumentRenderer { Document = documento };
        renderer.RenderDocument();
        using var stream = new MemoryStream();
        renderer.PdfDocument.Save(stream, false);
        return stream.ToArray();
    }

    private sealed class FilaExportacion
    {
        public Guid PacienteId { get; set; }
        public string? Nombre { get; set; }
        public string? PrimerApellido { get; set; }
        public string? SegundoApellido { get; set; }
        public DateTime FechaNacimiento { get; set; }
        public string? Sexo { get; set; }
        public string? Cartilla { get; set; }
        public string? Clues { get; set; }
        public string? Ageb { get; set; }
        public string? NombreUnidad { get; set; }
        public string? Institucion { get; set; }
        public string? Jurisdiccion { get; set; }
        public string? MunicipioUnidad { get; set; }
        public string? NombreAgeb { get; set; }
        public string? TipoAgeb { get; set; }
        public long AplicacionesDocumentadas { get; set; }
    }

    [HttpGet("{pacienteId:guid}/evaluacion")]
    public async Task<IActionResult> Evaluar(Guid pacienteId, [FromQuery] DateTime? fechaCorte, CancellationToken ct)
    {
        var corte = (fechaCorte ?? DateTime.Today).Date;
        if (FechaInvalida(corte)) return BadRequest(new { message = "Fecha de corte inválida." });
        var scope = Scope();
        if (string.IsNullOrWhiteSpace(scope)) return Forbid();
        var cs = _config.GetConnectionString("SqlServerConnection")
            ?? throw new InvalidOperationException("Falta SqlServerConnection.");
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync(ct);
        var permitido = await conn.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT COUNT(1) FROM sncev.paciente p
            WHERE p.id = @PacienteId AND p.eliminado = 0
              AND p.fecha_creacion < DATEADD(day, 1, @Corte)
              AND EXISTS (
                  SELECT 1 FROM sncev.vacuna_aplicada s
                  WHERE s.paciente_id = p.id AND s.anulada = 0
                    AND s.unidad_medica_clues = @Scope
                    AND s.fecha_aplicacion < DATEADD(day, 1, @Corte)
              )
            """, new { PacienteId = pacienteId, Scope = scope, Corte = corte }, cancellationToken: ct));
        if (permitido == 0) return NotFound();
        var resultado = await _evaluador.EvaluarAsync(pacienteId, corte, ct);
        return resultado is null ? NotFound() : Ok(resultado);
    }
}
