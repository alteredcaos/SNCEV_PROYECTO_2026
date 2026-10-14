using Dapper;
using Microsoft.Data.SqlClient;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using SNCEV.Api.Models;
using System.Security.Cryptography;
using QRCoder;
using PdfSharp.Drawing;
using PdfSharp.Pdf.IO;

namespace SNCEV.Api.Services;

public sealed class CartillaPdfService
{
    private readonly IConfiguration _config;
    private readonly MotorEsquemaVacunacionService _motor;

    public CartillaPdfService(
        IConfiguration config,
        MotorEsquemaVacunacionService motor)
    {
        _config = config;
        _motor = motor;
    }

    public sealed record EmisorCartilla(
    Guid? UsuarioId,
    string? Nombre,
    string? Clues);

    private static async Task RegistrarEmisionAsync(
    SqlConnection conn,
    Guid pacienteId,
    string folio,
    Guid codigoVerificacion,
    string hashSha256,
    DateTime fechaEmision,
    EmisorCartilla emisor,
    CancellationToken cancellationToken)
    {
        const string sql = """
        INSERT INTO sncev.documento_cartilla
        (
            paciente_id,
            folio,
            codigo_verificacion,
            hash_sha256,
            fecha_emision,
            usuario_emisor_id,
            usuario_emisor_nombre,
            clues_emision,
            estado
        )
        VALUES
        (
            @PacienteId,
            @Folio,
            @CodigoVerificacion,
            @HashSha256,
            @FechaEmision,
            @UsuarioEmisorId,
            @UsuarioEmisorNombre,
            @CluesEmision,
            'VIGENTE'
        );
        """;

        await conn.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    PacienteId = pacienteId,
                    Folio = folio,
                    CodigoVerificacion = codigoVerificacion,
                    HashSha256 = hashSha256,
                    FechaEmision = fechaEmision,

                    UsuarioEmisorId = emisor.UsuarioId,
                    UsuarioEmisorNombre = emisor.Nombre,
                    CluesEmision = emisor.Clues
                },
                cancellationToken: cancellationToken));
    }
    private string GetConnectionString() =>
        _config.GetConnectionString("SqlServerConnection")
        ?? throw new InvalidOperationException(
            "No se encontró la cadena de conexión 'SqlServerConnection'.");

    public async Task<CartillaPdfResultado?> GenerarAsync(
    Guid pacienteId,
    EmisorCartilla emisor,
    CancellationToken cancellationToken)
    {
        await using var conn = new SqlConnection(GetConnectionString());
        await conn.OpenAsync(cancellationToken);

        var paciente = await conn.QueryFirstOrDefaultAsync<PacientePdfDb>(
            new CommandDefinition(
                """
                SELECT
                    p.id AS Id,
                    p.curp AS Curp,
                    p.nombre AS Nombre,
                    p.primer_apellido AS PrimerApellido,
                    p.segundo_apellido AS SegundoApellido,
                    p.fecha_nacimiento AS FechaNacimiento,
                    p.sexo AS Sexo,
                    tc.nombre AS TipoCartilla
                FROM sncev.paciente p
                LEFT JOIN sncev.tipo_cartilla tc
                    ON tc.id = p.tipo_cartilla_id
                WHERE p.id = @PacienteId
                  AND p.eliminado = 0;
                """,
                new { PacienteId = pacienteId },
                cancellationToken: cancellationToken));

        if (paciente is null)
            return null;

        var domicilio = await conn.QueryFirstOrDefaultAsync<DomicilioPdfDb>(
            new CommandDefinition(
                """
                SELECT TOP (1)
                    d.estado_clave AS EstadoClave,
                    e.nombre AS EstadoNombre,
                    d.municipio_id AS MunicipioId,
                    m.nombre AS MunicipioNombre,
                    d.localidad AS Localidad,
                    d.colonia AS Colonia,
                    d.codigo_postal AS CodigoPostal,
                    d.calle AS Calle,
                    d.numero_exterior AS NumeroExterior,
                    d.numero_interior AS NumeroInterior,
                    d.direccion_texto AS DireccionTexto,
                    d.ageb_clave AS AgebClave,
                    d.cvegeo AS Cvegeo,
                    d.clues_sugerida AS CluesSugerida,
                    es.nombre_unidad AS CluesSugeridaNombre
                FROM sncev.domicilio_paciente d
                LEFT JOIN sncev_geo.estado e
                    ON e.clave_inegi = d.estado_clave
                LEFT JOIN sncev_geo.municipio m
                    ON m.id = d.municipio_id
                LEFT JOIN sncev_geo.establecimiento_salud es
                    ON es.clues = d.clues_sugerida
                WHERE d.paciente_id = @PacienteId
                  AND d.vigente = 1
                ORDER BY d.fecha_inicio DESC;
                """,
                new { PacienteId = pacienteId },
                cancellationToken: cancellationToken));

        // Reutiliza exactamente el mismo motor que usa la pantalla de Cartilla.
        // El motor sólo considera aplicaciones vigentes (no anuladas).
        EsquemaVacunacionDto? esquema =
            await _motor.EvaluarAsync(pacienteId, cancellationToken);

        if (esquema is null)
            return null;

        var emitido = DateTime.Now;

        // Identificador interno/público opaco.
        // No contiene CURP ni pacienteId.
        var codigoVerificacion = Guid.NewGuid();

        var folio =
            $"SNCEV-{emitido:yyyyMMdd}-{codigoVerificacion.ToString("N")[..12].ToUpperInvariant()}";

        var baseUrl =
            _config["Sncev:VerificationBaseUrl"]
            ?? "https://localhost:63730";

        baseUrl = baseUrl.TrimEnd('/');

        var urlVerificacion =
            $"{baseUrl}/v1/verificar/{codigoVerificacion:D}";

        var qrMatrix = GenerarQrMatrix(urlVerificacion);

        // MigraDoc genera el contenido y el pie textual. El QR se dibuja
        // después directamente sobre el PDF con PDFsharp, evitando por
        // completo el cargador de imágenes de MigraDoc.
        var document = CrearDocumento(
            paciente,
            domicilio,
            esquema,
            emitido,
            folio,
            codigoVerificacion,
            urlVerificacion);

        var renderer = new PdfDocumentRenderer
        {
            Document = document
        };

        renderer.RenderDocument();

        byte[] pdfBaseBytes;

        using (var stream = new MemoryStream())
        {
            renderer.PdfDocument.Save(stream, false);
            pdfBaseBytes = stream.ToArray();
        }

        // El resultado de este método ya contiene físicamente el QR.
        // Éstos serán los bytes exactos que recibirá el usuario y sobre
        // ellos se calculará posteriormente SHA-256.
        var pdfBytes = IncrustarQrVectorialConPdfSharp(pdfBaseBytes, qrMatrix);

        // SHA-256 del documento final, ya con el QR incrustado.
        var hashBytes = SHA256.HashData(pdfBytes);

        var hashSha256 =
            Convert.ToHexString(hashBytes)
                .ToLowerInvariant();

        // Registrar la emisión sólo después de haber generado
        // correctamente el PDF y su huella.
        await RegistrarEmisionAsync(
    conn,
    paciente.Id,
    folio,
    codigoVerificacion,
    hashSha256,
    emitido,
    emisor,
    cancellationToken);

        var nombreArchivo =
            $"Cartilla_SNCEV_{codigoVerificacion.ToString("N")[..12]}_{emitido:yyyyMMdd}.pdf";

        return new CartillaPdfResultado(
            pdfBytes,
            nombreArchivo);
    }

    private static Document CrearDocumento(
        PacientePdfDb paciente,
        DomicilioPdfDb? domicilio,
        EsquemaVacunacionDto esquema,
        DateTime emitido,
        string folio,
        Guid codigoVerificacion,
        string urlVerificacion)
    {
        var document = new Document();

        document.Info.Title = "Cartilla electrónica de vacunación";
        document.Info.Subject = "SNCEV";
        document.Info.Author = "SNCEV";

        ConfigurarEstilos(document);

        var section = document.AddSection();

        section.PageSetup.PageFormat = PageFormat.A4;
        section.PageSetup.Orientation = Orientation.Portrait;
        section.PageSetup.TopMargin = Unit.FromCentimeter(1.5);
        section.PageSetup.BottomMargin = Unit.FromCentimeter(1.5);
        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.5);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.5);

        CrearEncabezado(section);
        CrearDatosPaciente(section, paciente, esquema);
        CrearDomicilio(section, domicilio);
        CrearRecomendacion(section, esquema);
        CrearAplicaciones(section, esquema);
        CrearEstadoEsquema(section, esquema);
        CrearPiePagina(
            section,
            emitido,
            folio,
            codigoVerificacion,
            urlVerificacion);

        return document;
    }

    private static void ConfigurarEstilos(Document document)
    {
        // MigraDoc garantiza el estilo Normal.
        var normal = document.Styles[StyleNames.Normal];

        if (normal is null)
        {
            throw new InvalidOperationException(
                "MigraDoc no pudo inicializar el estilo Normal.");
        }

        normal.Font.Name = "Arial";
        normal.Font.Size = 9;

        // Creamos nuestros propios estilos.
        // No dependemos de que MigraDoc tenga Title/Heading1 predefinidos.

        var titulo = document.Styles["SNCEV-Titulo"];

        if (titulo is null)
        {
            titulo = document.Styles.AddStyle(
                "SNCEV-Titulo",
                StyleNames.Normal);
        }

        titulo.Font.Name = "Arial";
        titulo.Font.Size = 15;
        titulo.Font.Bold = true;
        titulo.ParagraphFormat.SpaceAfter = Unit.FromPoint(3);

        var encabezado = document.Styles["SNCEV-Encabezado"];

        if (encabezado is null)
        {
            encabezado = document.Styles.AddStyle(
                "SNCEV-Encabezado",
                StyleNames.Normal);
        }

        encabezado.Font.Name = "Arial";
        encabezado.Font.Size = 11;
        encabezado.Font.Bold = true;

        encabezado.ParagraphFormat.SpaceBefore =
            Unit.FromPoint(10);

        encabezado.ParagraphFormat.SpaceAfter =
            Unit.FromPoint(5);

        encabezado.ParagraphFormat.KeepWithNext = true;
    }

    private static void CrearEncabezado(Section section)
    {
        var p = section.AddParagraph();
        p.Style = "SNCEV-Titulo";
        p.AddText("SISTEMA NACIONAL DE CARTILLA ELECTRÓNICA DE VACUNACIÓN");

        p = section.AddParagraph();
        p.Format.Font.Size = 11;
        p.Format.Font.Bold = true;
        p.AddText("Cartilla electrónica de vacunación");

        p = section.AddParagraph();
        p.Format.Borders.Bottom.Width = Unit.FromPoint(1);
        p.Format.SpaceAfter = Unit.FromPoint(8);
    }

    private static void CrearDatosPaciente(
        Section section,
        PacientePdfDb paciente,
        EsquemaVacunacionDto esquema)
    {
        section.AddParagraph("Datos de identificación", "SNCEV-Encabezado");

        var table = section.AddTable();
        table.Borders.Width = Unit.FromPoint(0.4);
        table.Rows.LeftIndent = 0;

        table.AddColumn(Unit.FromCentimeter(4.3));
        table.AddColumn(Unit.FromCentimeter(5.0));
        table.AddColumn(Unit.FromCentimeter(4.0));
        table.AddColumn(Unit.FromCentimeter(4.2));

        AgregarFilaDatos(
            table,
            "Nombre completo",
            NombreCompleto(paciente),
            "CURP",
            Valor(paciente.Curp));

        AgregarFilaDatos(
            table,
            "Fecha de nacimiento",
            paciente.FechaNacimiento.ToString("dd/MM/yyyy"),
            "Edad",
            esquema.Edad?.Texto ?? "—");

        AgregarFilaDatos(
            table,
            "Sexo",
            paciente.Sexo == "M"
                ? "Masculino"
                : paciente.Sexo == "F"
                    ? "Femenino"
                    : "—",
            "Tipo de cartilla",
            Valor(paciente.TipoCartilla));
    }

    private static void CrearDomicilio(
        Section section,
        DomicilioPdfDb? domicilio)
    {
        section.AddParagraph("Domicilio vigente", "SNCEV-Encabezado");

        if (domicilio is null)
        {
            section.AddParagraph("Sin domicilio vigente registrado.");
            return;
        }

        section.AddParagraph(ConstruirDireccion(domicilio));

        var p = section.AddParagraph();
        p.Format.Font.Size = 8;

        p.AddFormattedText("AGEB: ", TextFormat.Bold);
        p.AddText(Valor(domicilio.Cvegeo ?? domicilio.AgebClave));
        p.AddText("    ");

        p.AddFormattedText("CLUES cercana: ", TextFormat.Bold);
        p.AddText(Valor(domicilio.CluesSugerida));

        if (!string.IsNullOrWhiteSpace(domicilio.CluesSugeridaNombre))
        {
            p.AddText(" - ");
            p.AddText(domicilio.CluesSugeridaNombre);
        }
    }

    private static void CrearRecomendacion(
        Section section,
        EsquemaVacunacionDto esquema)
    {
        if (esquema.Recomendacion is null)
            return;

        var table = section.AddTable();
        table.Format.SpaceBefore = Unit.FromPoint(10);
        table.Borders.Width = Unit.FromPoint(0.7);
        table.AddColumn(Unit.FromCentimeter(17.5));

        var row = table.AddRow();
        var cell = row.Cells[0];

        var p = cell.AddParagraph();
        p.Format.Font.Bold = true;
        p.AddText("Recomendación del motor");

        p = cell.AddParagraph();
        p.AddText(
            $"{esquema.Recomendacion.Vacuna} · dosis {esquema.Recomendacion.NumeroDosis}");

        p = cell.AddParagraph();
        p.Format.Font.Size = 8;
        p.AddText(Valor(esquema.Recomendacion.Motivo));
    }

    private static void CrearAplicaciones(
        Section section,
        EsquemaVacunacionDto esquema)
    {
        section.AddParagraph("Aplicaciones vigentes", "SNCEV-Encabezado");

        if (esquema.Aplicadas.Count == 0)
        {
            section.AddParagraph("No hay dosis vigentes registradas.");
            return;
        }

        var table = section.AddTable();
        table.Borders.Width = Unit.FromPoint(0.4);
        table.Rows.LeftIndent = 0;

        table.AddColumn(Unit.FromCentimeter(4.2));
        table.AddColumn(Unit.FromCentimeter(1.2));
        table.AddColumn(Unit.FromCentimeter(2.3));
        table.AddColumn(Unit.FromCentimeter(2.7));
        table.AddColumn(Unit.FromCentimeter(3.8));
        table.AddColumn(Unit.FromCentimeter(3.3));

        var header = table.AddRow();
        header.HeadingFormat = true;
        header.Format.Font.Bold = true;

        Encabezado(header.Cells[0], "Vacuna");
        Encabezado(header.Cells[1], "Dosis");
        Encabezado(header.Cells[2], "Fecha");
        Encabezado(header.Cells[3], "Lote");
        Encabezado(header.Cells[4], "Laboratorio");
        Encabezado(header.Cells[5], "CLUES");

        foreach (var dosis in esquema.Aplicadas
                     .OrderBy(x => x.FechaAplicacion))
        {
            var row = table.AddRow();

            Celda(row.Cells[0], Valor(dosis.Vacuna));
            Celda(row.Cells[1], dosis.NumeroDosis.ToString());
            Celda(row.Cells[2], dosis.FechaAplicacion.ToString("dd/MM/yyyy"));
            Celda(row.Cells[3], Valor(dosis.Lote));
            Celda(row.Cells[4], Valor(dosis.Laboratorio));
            Celda(row.Cells[5], Valor(dosis.UnidadMedicaClues));
        }

        var nota = section.AddParagraph();
        nota.Format.SpaceBefore = Unit.FromPoint(4);
        nota.Format.Font.Size = 8;
        nota.AddText(
            "Las aplicaciones anuladas se conservan en auditoría y no se consideran dosis vigentes.");
    }

    private static void CrearEstadoEsquema(
        Section section,
        EsquemaVacunacionDto esquema)
    {
        section.AddParagraph("Estado actual del esquema", "SNCEV-Encabezado");

        section.AddParagraph(
            $"Aplicadas: {esquema.Aplicadas.Count}   ·   " +
            $"Aplicables ahora: {esquema.Aplicables.Count}   ·   " +
            $"Programadas: {esquema.Programadas.Count}   ·   " +
            $"Requieren verificación: {esquema.RequierenVerificacion.Count}");

        if (esquema.Aplicables.Count > 0)
        {
            var titulo = section.AddParagraph();
            titulo.Format.SpaceBefore = Unit.FromPoint(5);
            titulo.Format.Font.Bold = true;
            titulo.AddText("Aplicables ahora");

            foreach (var item in esquema.Aplicables)
            {
                var p = section.AddParagraph();
                p.Format.LeftIndent = Unit.FromCentimeter(0.4);
                p.Format.Font.Size = 8;
                p.AddText(
                    $"• {item.Vacuna} · dosis {item.NumeroDosis}: {Valor(item.Motivo)}");
            }
        }

        if (esquema.Programadas.Count > 0)
        {
            var titulo = section.AddParagraph();
            titulo.Format.SpaceBefore = Unit.FromPoint(5);
            titulo.Format.Font.Bold = true;
            titulo.AddText("Programadas");

            foreach (var item in esquema.Programadas)
            {
                var p = section.AddParagraph();
                p.Format.LeftIndent = Unit.FromCentimeter(0.4);
                p.Format.Font.Size = 8;

                var fecha = item.FechaMinimaAplicacion.HasValue
                    ? $" · a partir del {item.FechaMinimaAplicacion.Value:dd/MM/yyyy}"
                    : string.Empty;

                p.AddText(
                    $"• {item.Vacuna} · dosis {item.NumeroDosis}{fecha}: {Valor(item.Motivo)}");
            }
        }

        if (esquema.RequierenVerificacion.Count > 0)
        {
            var titulo = section.AddParagraph();
            titulo.Format.SpaceBefore = Unit.FromPoint(5);
            titulo.Format.Font.Bold = true;
            titulo.AddText("Requieren verificación");

            foreach (var item in esquema.RequierenVerificacion)
            {
                var p = section.AddParagraph();
                p.Format.LeftIndent = Unit.FromCentimeter(0.4);
                p.Format.Font.Size = 8;
                p.AddText(
                    $"• {item.Vacuna} · dosis {item.NumeroDosis}: {Valor(item.Motivo)}");
            }
        }
    }

    private static void CrearPiePagina(
        Section section,
        DateTime emitido,
        string folio,
        Guid codigoVerificacion,
        string urlVerificacion)
    {
        var footer = section.Footers.Primary;

        var table = footer.AddTable();
        table.Borders.Top.Width = Unit.FromPoint(0.7);
        table.Rows.LeftIndent = 0;

        table.AddColumn(Unit.FromCentimeter(14.8));
        table.AddColumn(Unit.FromCentimeter(2.7));

        var row = table.AddRow();

        var info = row.Cells[0];
        info.VerticalAlignment = VerticalAlignment.Center;

        var p = info.AddParagraph();
        p.Format.Font.Size = 7;

        p.AddText("Documento electrónico generado por SNCEV");
        p.AddLineBreak();
        p.AddText($"Emisión: {emitido:dd/MM/yyyy HH:mm:ss}");
        p.AddLineBreak();
        p.AddText($"Folio: {folio}");
        p.AddLineBreak();
        p.AddText($"Código de verificación: {codigoVerificacion:D}");
        p.AddLineBreak();
        p.AddText("Integridad SHA-256 registrada por SNCEV.");
        p.AddLineBreak();
        p.AddFormattedText(
            "Escanee el QR para verificar esta emisión.",
            TextFormat.Bold);

        // Reservamos visualmente la columna derecha. El QR se dibuja
        // directamente sobre el PDF final mediante PDFsharp.
        var qrCell = row.Cells[1];
        qrCell.VerticalAlignment = VerticalAlignment.Center;
        var qrParagraph = qrCell.AddParagraph();
        qrParagraph.Format.Alignment = ParagraphAlignment.Center;
        qrParagraph.AddText(" ");

        var url = footer.AddParagraph();
        url.Format.Font.Size = 6;
        url.Format.SpaceBefore = Unit.FromPoint(2);
        url.AddText("Verificación: ");
        url.AddText(urlVerificacion);

        var paginas = footer.AddParagraph();
        paginas.Format.Alignment = ParagraphAlignment.Right;
        paginas.Format.Font.Size = 7;
        paginas.AddText("Página ");
        paginas.AddPageField();
        paginas.AddText(" de ");
        paginas.AddNumPagesField();
    }

    private static byte[] IncrustarQrVectorialConPdfSharp(
        byte[] pdfBaseBytes,
        bool[,] qrMatrix)
    {
        using var input = new MemoryStream(pdfBaseBytes);
        using var pdf = PdfReader.Open(input, PdfDocumentOpenMode.Modify);

        if (pdf.PageCount == 0)
            throw new InvalidOperationException(
                "No fue posible incrustar el QR porque el PDF no contiene páginas.");

        var rows = qrMatrix.GetLength(0);
        var cols = qrMatrix.GetLength(1);

        if (rows == 0 || cols == 0 || rows != cols)
            throw new InvalidOperationException(
                "La matriz QR generada no es válida.");

        // Tamaño total del QR en puntos PDF (~2.3 cm).
        const double qrSize = 65.0;
        const double rightMargin = 43.0;
        const double bottomMargin = 35.0;

        // Cada módulo se dibuja directamente como vector PDF.
        // No intervienen PNG, XImage, codecs ni rutas temporales.
        var moduleSize = qrSize / cols;

        foreach (var page in pdf.Pages)
        {
            using var gfx = XGraphics.FromPdfPage(
                page,
                XGraphicsPdfPageOptions.Append);

            var x0 = page.Width.Point - rightMargin - qrSize;
            var y0 = page.Height.Point - bottomMargin - qrSize;

            // Fondo blanco para conservar contraste y zona silenciosa.
            gfx.DrawRectangle(XBrushes.White, x0, y0, qrSize, qrSize);

            for (var row = 0; row < rows; row++)
            {
                for (var col = 0; col < cols; col++)
                {
                    if (!qrMatrix[row, col])
                        continue;

                    // Ligero solapamiento evita micro-grietas por redondeo
                    // al visualizar o imprimir el PDF.
                    var x = x0 + (col * moduleSize);
                    var y = y0 + (row * moduleSize);

                    gfx.DrawRectangle(
                        XBrushes.Black,
                        x,
                        y,
                        moduleSize + 0.02,
                        moduleSize + 0.02);
                }
            }
        }

        using var output = new MemoryStream();
        pdf.Save(output, false);
        return output.ToArray();
    }

    private static bool[,] GenerarQrMatrix(string contenido)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(
            contenido,
            QRCodeGenerator.ECCLevel.Q);

        var moduleMatrix = qrData.ModuleMatrix;
        var size = moduleMatrix.Count;
        var result = new bool[size, size];

        for (var row = 0; row < size; row++)
        {
            for (var col = 0; col < size; col++)
            {
                result[row, col] = moduleMatrix[row][col];
            }
        }

        return result;
    }

    private static void AgregarFilaDatos(
        Table table,
        string etiqueta1,
        string valor1,
        string etiqueta2,
        string valor2)
    {
        var row = table.AddRow();

        Etiqueta(row.Cells[0], etiqueta1);
        ValorCelda(row.Cells[1], valor1);

        Etiqueta(row.Cells[2], etiqueta2);
        ValorCelda(row.Cells[3], valor2);
    }

    private static void Etiqueta(Cell cell, string texto)
    {
        cell.Format.Font.Size = 7;
        cell.Format.Font.Bold = true;
        cell.VerticalAlignment = VerticalAlignment.Center;
        cell.AddParagraph(texto);
    }

    private static void ValorCelda(Cell cell, string texto)
    {
        cell.VerticalAlignment = VerticalAlignment.Center;
        cell.AddParagraph(texto);
    }

    private static void Encabezado(Cell cell, string texto)
    {
        cell.Shading.Color = Colors.LightGray;
        cell.VerticalAlignment = VerticalAlignment.Center;

        var p = cell.AddParagraph();
        p.Format.Font.Size = 7;
        p.Format.Font.Bold = true;
        p.AddText(texto);
    }

    private static void Celda(Cell cell, string texto)
    {
        cell.VerticalAlignment = VerticalAlignment.Center;

        var p = cell.AddParagraph();
        p.Format.Font.Size = 7;
        p.AddText(texto);
    }

    private static string NombreCompleto(PacientePdfDb p) =>
        string.Join(
            " ",
            new[] { p.Nombre, p.PrimerApellido, p.SegundoApellido }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

    private static string ConstruirDireccion(DomicilioPdfDb d)
    {
        if (!string.IsNullOrWhiteSpace(d.DireccionTexto))
            return d.DireccionTexto;

        return string.Join(
            ", ",
            new[]
            {
                Unir(d.Calle, d.NumeroExterior),
                string.IsNullOrWhiteSpace(d.NumeroInterior)
                    ? null
                    : $"Int. {d.NumeroInterior}",
                d.Colonia,
                d.Localidad,
                d.MunicipioNombre,
                d.EstadoNombre,
                string.IsNullOrWhiteSpace(d.CodigoPostal)
                    ? null
                    : $"C.P. {d.CodigoPostal}"
            }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private static string? Unir(string? a, string? b)
    {
        var valor = string.Join(
            " ",
            new[] { a, b }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

        return string.IsNullOrWhiteSpace(valor)
            ? null
            : valor;
    }

    private static string Valor(string? valor) =>
        string.IsNullOrWhiteSpace(valor)
            ? "—"
            : valor.Trim();

    private sealed class PacientePdfDb
    {
        public Guid Id { get; set; }
        public string? Curp { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string PrimerApellido { get; set; } = string.Empty;
        public string? SegundoApellido { get; set; }
        public DateTime FechaNacimiento { get; set; }
        public string Sexo { get; set; } = string.Empty;
        public string? TipoCartilla { get; set; }
    }

    private sealed class DomicilioPdfDb
    {
        public string? EstadoClave { get; set; }
        public string? EstadoNombre { get; set; }
        public int? MunicipioId { get; set; }
        public string? MunicipioNombre { get; set; }
        public string? Localidad { get; set; }
        public string? Colonia { get; set; }
        public string? CodigoPostal { get; set; }
        public string? Calle { get; set; }
        public string? NumeroExterior { get; set; }
        public string? NumeroInterior { get; set; }
        public string? DireccionTexto { get; set; }
        public string? AgebClave { get; set; }
        public string? Cvegeo { get; set; }
        public string? CluesSugerida { get; set; }
        public string? CluesSugeridaNombre { get; set; }
    }
}

public sealed record CartillaPdfResultado(
    byte[] Contenido,
    string NombreArchivo);
