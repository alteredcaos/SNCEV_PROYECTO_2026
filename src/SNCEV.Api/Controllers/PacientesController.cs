using Dapper;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SNCEV.Api.Models;
using SNCEV.Api.Services;
using System.Security.Claims;
using System.Text.Json;


namespace SNCEV.Api.Controllers;

[ApiController]
[Authorize]
[Route("v1/pacientes")]
public class PacientesController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly AesEncryptionService _aesService;

    private readonly MotorEsquemaVacunacionService _motorEsquema;

    public PacientesController(
     IConfiguration config,
     AesEncryptionService aesService,
     MotorEsquemaVacunacionService motorEsquema)
    {
        _config = config;
        _aesService = aesService;
        _motorEsquema = motorEsquema;
    }


    [HttpGet("{id:guid}/esquema-vacunacion")]
    [Authorize(Roles = "Administrador,Capturador,Directivo,Auditor")]
    public async Task<IActionResult> ObtenerEsquemaVacunacion(
    Guid id,
    CancellationToken cancellationToken)
    {
        var resultado =
            await _motorEsquema.EvaluarAsync(
                id,
                cancellationToken);

        if (resultado is null)
        {
            return NotFound(new
            {
                message =
                    "No se encontró un paciente activo con ese identificador."
            });
        }

        return Ok(resultado);
    }

    [HttpGet]
    public async Task<IActionResult> ObtenerPacientes(
        [FromQuery] string? curp,
        [FromQuery] string? nombre,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var offset = (page - 1) * pageSize;

        await using var conn = new SqlConnection(GetConnectionString());

        const string where = @"
            FROM sncev.paciente
            WHERE eliminado = 0
              AND (@Curp IS NULL OR curp = @Curp)
              AND (@Nombre IS NULL OR
                   nombre LIKE '%' + @Nombre + '%' OR
                   primer_apellido LIKE '%' + @Nombre + '%' OR
                   segundo_apellido LIKE '%' + @Nombre + '%')";

        var args = new
        {
            Curp = string.IsNullOrWhiteSpace(curp) ? null : curp.Trim(),
            Nombre = string.IsNullOrWhiteSpace(nombre) ? null : nombre.Trim(),
            Offset = offset,
            PageSize = pageSize
        };

        var total = await conn.ExecuteScalarAsync<int>("SELECT COUNT(1) " + where, args);
        var pacientes = await conn.QueryAsync(@"
            SELECT id, curp, nombre, primer_apellido, segundo_apellido, fecha_nacimiento, sexo
            " + where + @"
            ORDER BY primer_apellido, segundo_apellido, nombre, id
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY", args);

        return Ok(new
        {
            items = pacientes,
            page,
            pageSize,
            total,
            totalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }


    public  class CrearPacienteRequest
    {
        public string Curp { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string PrimerApellido { get; set; } = string.Empty;
        public string? SegundoApellido { get; set; }
        public DateTime FechaNacimiento { get; set; }
        public string Sexo { get; set; } = string.Empty;
        public string EntidadNacimientoClave { get; set; } = string.Empty;
        public int MunicipioResidenciaId { get; set; }
        public string? Localidad { get; set; }
        public int TipoCartillaId { get; set; }
        public string? AgebClave { get; set; }
        public string? Direccion { get; set; }
        public string? TutorNombre { get; set; }
        public string? TutorCurp { get; set; }
        public string? TutorTelefono { get; set; }
        public bool PertencePuebloIndigena { get; set; }
        public bool EsMigrante { get; set; }
    }

    [HttpPost]
    [Authorize(Roles = "Administrador,Capturador")]
    public async Task<IActionResult> CrearPaciente([FromBody] CrearPacienteRequest dto)
    {
        var curp = (dto.Curp ?? string.Empty).Trim().ToUpperInvariant();
        var nombre = (dto.Nombre ?? string.Empty).Trim();
        var primerApellido = (dto.PrimerApellido ?? string.Empty).Trim();
        var segundoApellido = string.IsNullOrWhiteSpace(dto.SegundoApellido) ? null : dto.SegundoApellido.Trim();
        var sexo = (dto.Sexo ?? string.Empty).Trim().ToUpperInvariant();
        var entidadNacimiento = (dto.EntidadNacimientoClave ?? string.Empty).Trim();
        var localidad = string.IsNullOrWhiteSpace(dto.Localidad) ? null : dto.Localidad.Trim();
        var ageb = string.IsNullOrWhiteSpace(dto.AgebClave) ? null : dto.AgebClave.Trim();
        var tutorCurp = string.IsNullOrWhiteSpace(dto.TutorCurp) ? null : dto.TutorCurp.Trim().ToUpperInvariant();

        if (curp.Length != 18)
            return BadRequest(new { error = "La CURP debe contener exactamente 18 caracteres." });
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 100)
            return BadRequest(new { error = "Nombre es obligatorio y admite hasta 100 caracteres." });
        if (string.IsNullOrWhiteSpace(primerApellido) || primerApellido.Length > 100)
            return BadRequest(new { error = "Primer apellido es obligatorio y admite hasta 100 caracteres." });
        if (segundoApellido?.Length > 100)
            return BadRequest(new { error = "Segundo apellido admite hasta 100 caracteres." });
        if (dto.FechaNacimiento.Date > DateTime.Today)
            return BadRequest(new { error = "La fecha de nacimiento no puede ser futura." });
        if (dto.FechaNacimiento.Date < new DateTime(1900, 1, 1))
            return BadRequest(new { error = "La fecha de nacimiento no es válida." });
        if (sexo is not ("M" or "F"))
            return BadRequest(new { error = "Sexo debe ser M o F." });
        if (entidadNacimiento.Length != 2)
            return BadRequest(new { error = "Selecciona una entidad de nacimiento válida." });
        if (dto.MunicipioResidenciaId <= 0)
            return BadRequest(new { error = "Selecciona un municipio de residencia válido." });
        if (localidad?.Length > 150)
            return BadRequest(new { error = "Localidad admite hasta 150 caracteres." });
        if (dto.TipoCartillaId <= 0)
            return BadRequest(new { error = "Selecciona un tipo de cartilla válido." });
        if (ageb?.Length > 13)
            return BadRequest(new { error = "La clave AGEB admite hasta 13 caracteres." });
        if (dto.Direccion?.Length > 255 || dto.TutorNombre?.Length > 200 || dto.TutorTelefono?.Length > 20)
            return BadRequest(new { error = "Uno de los campos de contacto excede la longitud permitida." });
        if (tutorCurp is not null && tutorCurp.Length != 18)
            return BadRequest(new { error = "La CURP del tutor debe contener 18 caracteres." });

        await using var conn = new SqlConnection(GetConnectionString());
        await conn.OpenAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

        try
        {
            var entidadExiste = await conn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(1)
                FROM sncev_geo.estado
                WHERE clave_inegi = @Clave", new { Clave = entidadNacimiento }, tx);
            if (entidadExiste == 0)
            {
                await tx.RollbackAsync();
                return BadRequest(new { error = "La entidad de nacimiento no existe en el catálogo." });
            }

            var municipioExiste = await conn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(1)
                FROM sncev_geo.municipio
                WHERE id = @Id", new { Id = dto.MunicipioResidenciaId }, tx);
            if (municipioExiste == 0)
            {
                await tx.RollbackAsync();
                return BadRequest(new { error = "El municipio de residencia no existe en el catálogo." });
            }

            var cartilla = await conn.QueryFirstOrDefaultAsync<(int Id, string Nombre, int EdadMinima, int? EdadMaxima, string? Sexo)>(@"
                SELECT id AS Id, nombre AS Nombre, edad_minima AS EdadMinima,
                       edad_maxima AS EdadMaxima, sexo AS Sexo
                FROM sncev.tipo_cartilla
                WHERE id = @Id AND activo = 1", new { Id = dto.TipoCartillaId }, tx);

            if (cartilla.Id == 0)
            {
                await tx.RollbackAsync();
                return BadRequest(new { error = "El tipo de cartilla seleccionado no existe o está inactivo." });
            }

            var hoy = DateTime.Today;
            var nacimiento = dto.FechaNacimiento.Date;
            var edad = hoy.Year - nacimiento.Year;
            if (nacimiento > hoy.AddYears(-edad)) edad--;

            var cartillaCompatible = edad >= cartilla.EdadMinima
                && (!cartilla.EdadMaxima.HasValue || edad <= cartilla.EdadMaxima.Value)
                && (string.IsNullOrWhiteSpace(cartilla.Sexo) || cartilla.Sexo == sexo);

            if (!cartillaCompatible)
            {
                await tx.RollbackAsync();
                return BadRequest(new { error = $"La cartilla '{cartilla.Nombre}' no corresponde a la edad y sexo del paciente." });
            }

            if (ageb is not null)
            {
                var agebExiste = await conn.ExecuteScalarAsync<int>(@"
                    SELECT COUNT(1)
                    FROM sncev_geo.ageb
                    WHERE clave = @Clave", new { Clave = ageb }, tx);
                if (agebExiste == 0)
                {
                    await tx.RollbackAsync();
                    return BadRequest(new { error = "La clave AGEB no existe en el catálogo." });
                }
            }

            var existente = await conn.QueryFirstOrDefaultAsync<Guid?>(@"
                SELECT id
                FROM sncev.paciente WITH (UPDLOCK, HOLDLOCK)
                WHERE curp = @Curp AND eliminado = 0", new { Curp = curp }, tx);
            if (existente.HasValue)
            {
                await tx.RollbackAsync();
                return Conflict(new { error = "Ya existe un paciente registrado con esa CURP.", pacienteId = existente.Value });
            }

            var id = Guid.NewGuid();
            var curpCifrada = _aesService.Encrypt(curp);

            await conn.ExecuteAsync(@"
                INSERT INTO sncev.paciente
                (
                    id, curp, curp_cifrada, nombre, primer_apellido, segundo_apellido,
                    fecha_nacimiento, sexo, entidad_nacimiento_clave, municipio_residencia_id,
                    ageb_clave, localidad, direccion, tutor_nombre, tutor_curp, tutor_telefono,
                    pertence_pueblo_indigena, es_migrante, tipo_cartilla_id, version_cartilla
                )
                VALUES
                (
                    @Id, @Curp, @CurpCifrada, @Nombre, @PrimerApellido, @SegundoApellido,
                    @FechaNacimiento, @Sexo, @EntidadNacimientoClave, @MunicipioResidenciaId,
                    @AgebClave, @Localidad, @Direccion, @TutorNombre, @TutorCurp, @TutorTelefono,
                    @PertencePuebloIndigena, @EsMigrante, @TipoCartillaId, @VersionCartilla
                )", new
            {
                Id = id,
                Curp = curp,
                CurpCifrada = curpCifrada,
                Nombre = nombre,
                PrimerApellido = primerApellido,
                SegundoApellido = segundoApellido,
                FechaNacimiento = dto.FechaNacimiento.Date,
                Sexo = sexo,
                EntidadNacimientoClave = entidadNacimiento,
                MunicipioResidenciaId = dto.MunicipioResidenciaId,
                AgebClave = ageb,
                Localidad = localidad,
                Direccion = string.IsNullOrWhiteSpace(dto.Direccion) ? null : dto.Direccion.Trim(),
                TutorNombre = string.IsNullOrWhiteSpace(dto.TutorNombre) ? null : dto.TutorNombre.Trim(),
                TutorCurp = tutorCurp,
                TutorTelefono = string.IsNullOrWhiteSpace(dto.TutorTelefono) ? null : dto.TutorTelefono.Trim(),
                dto.PertencePuebloIndigena,
                dto.EsMigrante,
                TipoCartillaId = dto.TipoCartillaId,
                VersionCartilla = cartilla.Nombre
            }, tx);

            var snapshotAlta = JsonSerializer.Serialize(new
            {
                id, nombre, primerApellido, segundoApellido,
                fechaNacimiento = dto.FechaNacimiento.Date, sexo, entidadNacimiento,
                municipioResidenciaId = dto.MunicipioResidenciaId, ageb, localidad,
                direccion = string.IsNullOrWhiteSpace(dto.Direccion) ? null : dto.Direccion.Trim(),
                tutorNombre = string.IsNullOrWhiteSpace(dto.TutorNombre) ? null : dto.TutorNombre.Trim(),
                tutorTelefono = string.IsNullOrWhiteSpace(dto.TutorTelefono) ? null : dto.TutorTelefono.Trim(),
                dto.PertencePuebloIndigena, dto.EsMigrante, tipoCartillaId = dto.TipoCartillaId,
                versionCartilla = cartilla.Nombre
            });
            await RegistrarAuditoriaAsync(conn, tx, id, "ALTA", "Alta de paciente", null, snapshotAlta);

            await tx.CommitAsync();

            return CreatedAtAction(nameof(ObtenerPorId), new { id }, new
            {
                id,
                curp,
                nombre,
                primerApellido,
                segundoApellido,
                fechaNacimiento = dto.FechaNacimiento.Date,
                sexo
            });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObtenerPorId(Guid id)
    {
        await using var conn = new SqlConnection(GetConnectionString());
        var paciente = await conn.QueryFirstOrDefaultAsync(@"
            SELECT id, curp, nombre, primer_apellido, segundo_apellido, fecha_nacimiento, sexo
            FROM sncev.paciente
            WHERE id = @Id AND eliminado = 0", new { Id = id });

        return paciente is null
            ? NotFound(new { error = "Paciente no encontrado" })
            : Ok(paciente);
    }

    public sealed class ActualizarPacienteRequest
    {
        public string Curp { get; set; } = string.Empty;

        public string Nombre { get; set; } = string.Empty;

        public string PrimerApellido { get; set; } = string.Empty;

        public string? SegundoApellido { get; set; }

        public DateTime FechaNacimiento { get; set; }

        public string Sexo { get; set; } = string.Empty;

        public string EntidadNacimientoClave { get; set; } = string.Empty;

        public int TipoCartillaId { get; set; }

        public string? TutorNombre { get; set; }

        public string? TutorCurp { get; set; }

        public string? TutorTelefono { get; set; }

        public bool PertencePuebloIndigena { get; set; }

        public bool EsMigrante { get; set; }
    }

    public sealed class BajaPacienteRequest
    {
        public string Motivo { get; set; } = string.Empty;
    }

    
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Administrador,Capturador")]
    public async Task<IActionResult> ActualizarPaciente(
    Guid id,
    [FromBody] ActualizarPacienteRequest dto)
    {
        var curp = (dto.Curp ?? string.Empty)
            .Trim()
            .ToUpperInvariant();

        var nombre = (dto.Nombre ?? string.Empty).Trim();

        var primerApellido =
            (dto.PrimerApellido ?? string.Empty).Trim();

        var segundoApellido =
            string.IsNullOrWhiteSpace(dto.SegundoApellido)
                ? null
                : dto.SegundoApellido.Trim();

        var sexo = (dto.Sexo ?? string.Empty)
            .Trim()
            .ToUpperInvariant();

        var entidadNacimiento =
            (dto.EntidadNacimientoClave ?? string.Empty).Trim();

        var tutorNombre =
            string.IsNullOrWhiteSpace(dto.TutorNombre)
                ? null
                : dto.TutorNombre.Trim();

        var tutorCurp =
            string.IsNullOrWhiteSpace(dto.TutorCurp)
                ? null
                : dto.TutorCurp.Trim().ToUpperInvariant();

        var tutorTelefono =
            string.IsNullOrWhiteSpace(dto.TutorTelefono)
                ? null
                : dto.TutorTelefono.Trim();

        // ============================================================
        // VALIDACIONES NOMINALES
        // ============================================================

        if (curp.Length != 18)
            return BadRequest(new
            {
                error = "La CURP debe contener exactamente 18 caracteres."
            });

        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 100)
            return BadRequest(new
            {
                error = "Nombre es obligatorio y admite hasta 100 caracteres."
            });

        if (string.IsNullOrWhiteSpace(primerApellido) ||
            primerApellido.Length > 100)
            return BadRequest(new
            {
                error = "Primer apellido es obligatorio y admite hasta 100 caracteres."
            });

        if (segundoApellido?.Length > 100)
            return BadRequest(new
            {
                error = "Segundo apellido admite hasta 100 caracteres."
            });

        if (dto.FechaNacimiento.Date > DateTime.Today ||
            dto.FechaNacimiento.Date < new DateTime(1900, 1, 1))
            return BadRequest(new
            {
                error = "La fecha de nacimiento no es válida."
            });

        if (sexo is not ("M" or "F"))
            return BadRequest(new
            {
                error = "Sexo debe ser M o F."
            });

        if (entidadNacimiento.Length != 2)
            return BadRequest(new
            {
                error = "Selecciona una entidad de nacimiento válida."
            });

        if (dto.TipoCartillaId <= 0)
            return BadRequest(new
            {
                error = "Selecciona un tipo de cartilla válido."
            });

        if (tutorNombre?.Length > 200)
            return BadRequest(new
            {
                error = "El nombre del tutor admite hasta 200 caracteres."
            });

        if (tutorTelefono?.Length > 20)
            return BadRequest(new
            {
                error = "El teléfono admite hasta 20 caracteres."
            });

        if (tutorCurp is not null && tutorCurp.Length != 18)
            return BadRequest(new
            {
                error = "La CURP del tutor debe contener 18 caracteres."
            });

        await using var conn =
            new SqlConnection(GetConnectionString());

        await conn.OpenAsync();

        await using var tx =
            (SqlTransaction)await conn.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);

        try
        {
            // ========================================================
            // PACIENTE ACTUAL
            //
            // Importante:
            // aquí ya NO recuperamos residencia para modificarla.
            // ========================================================

            var anterior = await conn.QueryFirstOrDefaultAsync(@"
            SELECT
                id,
                nombre,
                primer_apellido,
                segundo_apellido,
                fecha_nacimiento,
                sexo,
                entidad_nacimiento_clave,
                tutor_nombre,
                tutor_telefono,
                pertence_pueblo_indigena,
                es_migrante,
                tipo_cartilla_id,
                version_cartilla
            FROM sncev.paciente WITH (UPDLOCK, HOLDLOCK)
            WHERE id = @Id
              AND eliminado = 0",
                new { Id = id },
                tx);

            if (anterior is null)
            {
                await tx.RollbackAsync();

                return NotFound(new
                {
                    error = "Paciente no encontrado"
                });
            }

            var snapshotAnterior =
                JsonSerializer.Serialize(anterior);

            // ========================================================
            // ENTIDAD DE NACIMIENTO
            // ========================================================

            var entidadExiste =
                await conn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(1)
                FROM sncev_geo.estado
                WHERE clave_inegi = @Clave",
                    new
                    {
                        Clave = entidadNacimiento
                    },
                    tx);

            if (entidadExiste == 0)
            {
                await tx.RollbackAsync();

                return BadRequest(new
                {
                    error =
                        "La entidad de nacimiento no existe en el catálogo."
                });
            }

            // ========================================================
            // TIPO DE CARTILLA
            // ========================================================

            var cartilla =
                await conn.QueryFirstOrDefaultAsync<
                    (int Id,
                     string Nombre,
                     int EdadMinima,
                     int? EdadMaxima,
                     string? Sexo)>(@"
                SELECT
                    id AS Id,
                    nombre AS Nombre,
                    edad_minima AS EdadMinima,
                    edad_maxima AS EdadMaxima,
                    sexo AS Sexo
                FROM sncev.tipo_cartilla
                WHERE id = @Id
                  AND activo = 1",
                    new
                    {
                        Id = dto.TipoCartillaId
                    },
                    tx);

            if (cartilla.Id == 0)
            {
                await tx.RollbackAsync();

                return BadRequest(new
                {
                    error =
                        "El tipo de cartilla seleccionado no existe o está inactivo."
                });
            }

            // ========================================================
            // COMPATIBILIDAD EDAD / SEXO / CARTILLA
            // ========================================================

            var hoy = DateTime.Today;
            var nacimiento = dto.FechaNacimiento.Date;

            var edad = hoy.Year - nacimiento.Year;

            if (nacimiento > hoy.AddYears(-edad))
                edad--;

            var cartillaCompatible =
                edad >= cartilla.EdadMinima &&
                (!cartilla.EdadMaxima.HasValue ||
                 edad <= cartilla.EdadMaxima.Value) &&
                (string.IsNullOrWhiteSpace(cartilla.Sexo) ||
                 cartilla.Sexo == sexo);

            if (!cartillaCompatible)
            {
                await tx.RollbackAsync();

                return BadRequest(new
                {
                    error =
                        $"La cartilla '{cartilla.Nombre}' no corresponde a la edad y sexo del paciente."
                });
            }

            // ========================================================
            // CURP DUPLICADA
            // ========================================================

            var duplicado =
                await conn.QueryFirstOrDefaultAsync<Guid?>(@"
                SELECT id
                FROM sncev.paciente WITH (UPDLOCK, HOLDLOCK)
                WHERE curp = @Curp
                  AND id <> @Id
                  AND eliminado = 0",
                    new
                    {
                        Curp = curp,
                        Id = id
                    },
                    tx);

            if (duplicado.HasValue)
            {
                await tx.RollbackAsync();

                return Conflict(new
                {
                    error =
                        "Ya existe otro paciente registrado con esa CURP.",
                    pacienteId = duplicado.Value
                });
            }

            // ========================================================
            // ACTUALIZACIÓN EXCLUSIVAMENTE NOMINAL
            // ========================================================

            await conn.ExecuteAsync(@"
            UPDATE sncev.paciente
            SET
                curp = @Curp,
                curp_cifrada = @CurpCifrada,

                nombre = @Nombre,
                primer_apellido = @PrimerApellido,
                segundo_apellido = @SegundoApellido,

                fecha_nacimiento = @FechaNacimiento,
                sexo = @Sexo,

                entidad_nacimiento_clave =
                    @EntidadNacimientoClave,

                tutor_nombre = @TutorNombre,
                tutor_curp = @TutorCurp,
                tutor_telefono = @TutorTelefono,

                pertence_pueblo_indigena =
                    @PertencePuebloIndigena,

                es_migrante =
                    @EsMigrante,

                tipo_cartilla_id =
                    @TipoCartillaId,

                version_cartilla =
                    @VersionCartilla,

                fecha_actualizacion =
                    SYSDATETIME()

            WHERE id = @Id
              AND eliminado = 0",
                new
                {
                    Id = id,

                    Curp = curp,
                    CurpCifrada =
                        _aesService.Encrypt(curp),

                    Nombre = nombre,
                    PrimerApellido = primerApellido,
                    SegundoApellido = segundoApellido,

                    FechaNacimiento =
                        dto.FechaNacimiento.Date,

                    Sexo = sexo,

                    EntidadNacimientoClave =
                        entidadNacimiento,

                    TutorNombre = tutorNombre,
                    TutorCurp = tutorCurp,
                    TutorTelefono = tutorTelefono,

                    dto.PertencePuebloIndigena,
                    dto.EsMigrante,

                    TipoCartillaId =
                        dto.TipoCartillaId,

                    VersionCartilla =
                        cartilla.Nombre
                },
                tx);

            // ========================================================
            // AUDITORÍA
            //
            // Sin CURP ni CURP del tutor en snapshots.
            // Tampoco se registra residencia aquí.
            // ========================================================

            var snapshotNuevo =
                JsonSerializer.Serialize(new
                {
                    id,

                    nombre,
                    primerApellido,
                    segundoApellido,

                    fechaNacimiento =
                        dto.FechaNacimiento.Date,

                    sexo,

                    entidadNacimiento,

                    tutorNombre,
                    tutorTelefono,

                    dto.PertencePuebloIndigena,
                    dto.EsMigrante,

                    tipoCartillaId =
                        dto.TipoCartillaId,

                    versionCartilla =
                        cartilla.Nombre
                });

            await RegistrarAuditoriaAsync(
                conn,
                tx,
                id,
                "MODIFICACION",
                "Actualización de datos nominales del paciente",
                snapshotAnterior,
                snapshotNuevo);

            await tx.CommitAsync();

            return Ok(new
            {
                id,
                message =
                    "Datos del paciente actualizados correctamente."
            });
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    [HttpPost("{id:guid}/baja")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> DarDeBajaPaciente(Guid id, [FromBody] BajaPacienteRequest dto)
    {
        var motivo = (dto?.Motivo ?? string.Empty).Trim();
        if (motivo.Length < 5 || motivo.Length > 500)
            return BadRequest(new { error = "El motivo de la baja es obligatorio y debe contener entre 5 y 500 caracteres." });

        await using var conn = new SqlConnection(GetConnectionString());
        await conn.OpenAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            var paciente = await conn.QueryFirstOrDefaultAsync(@"
                SELECT id, nombre, primer_apellido, segundo_apellido, fecha_nacimiento, sexo,
                       entidad_nacimiento_clave, municipio_residencia_id, ageb_clave, localidad, direccion,
                       tutor_nombre, tutor_telefono, pertence_pueblo_indigena, es_migrante,
                       tipo_cartilla_id, version_cartilla
                FROM sncev.paciente WITH (UPDLOCK, HOLDLOCK)
                WHERE id=@Id AND eliminado=0", new { Id = id }, tx);

            if (paciente is null)
            {
                await tx.RollbackAsync();
                return NotFound(new { error = "Paciente activo no encontrado." });
            }

            var snapshot = JsonSerializer.Serialize(paciente);
            await conn.ExecuteAsync(@"
                UPDATE sncev.paciente
                SET eliminado=1, fecha_actualizacion=SYSDATETIME()
                WHERE id=@Id AND eliminado=0", new { Id = id }, tx);

            await RegistrarAuditoriaAsync(conn, tx, id, "BAJA", motivo, snapshot, null);
            await tx.CommitAsync();
            return Ok(new { id, message = "Paciente dado de baja correctamente." });
        }
        catch { await tx.RollbackAsync(); throw; }
    }


    [HttpGet("{id:guid}/auditoria")]
    [Authorize(Roles = "Administrador,Auditor")]
    public async Task<IActionResult> ObtenerAuditoriaPaciente(Guid id)
    {
        await using var conn = new SqlConnection(GetConnectionString());
        var items = await conn.QueryAsync(@"
            SELECT id, paciente_id, accion, motivo, usuario_id, usuario_nombre, usuario_rol,
                   fecha, datos_anteriores, datos_nuevos, ip_origen
            FROM sncev.auditoria_paciente
            WHERE paciente_id=@Id
            ORDER BY fecha DESC, id DESC", new { Id = id });
        return Ok(items);
    }

    [HttpGet("{id:guid}/expediente")]
    public async Task<IActionResult> ObtenerExpediente(Guid id)
    {
        await using var conn = new SqlConnection(GetConnectionString());

        // ============================================================
        // 1. DATOS NOMINALES DEL PACIENTE
        // ============================================================

        var paciente = await conn.QueryFirstOrDefaultAsync(@"
        SELECT
            p.id,
            p.curp,
            p.nombre,
            p.primer_apellido,
            p.segundo_apellido,
            p.fecha_nacimiento,
            p.sexo,

            p.entidad_nacimiento_clave,
            en.nombre AS entidad_nacimiento,

            -- Campos históricos/legacy de residencia.
            -- Se mantienen temporalmente por compatibilidad
            -- con el frontend actual.
            p.municipio_residencia_id,
            m.nombre AS municipio_residencia,
            m.estado_clave AS estado_residencia_clave,
            er.nombre AS estado_residencia,

            p.ageb_clave,
            p.localidad,
            p.direccion,

            p.tipo_cartilla_id,
            tc.nombre AS tipo_cartilla,

            p.tutor_nombre,
            p.tutor_curp,
            p.tutor_telefono,

            p.pertence_pueblo_indigena,
            p.es_migrante,

            p.version_cartilla,
            p.fecha_creacion,
            p.fecha_actualizacion

        FROM sncev.paciente p

        INNER JOIN sncev_geo.estado en
            ON en.clave_inegi = p.entidad_nacimiento_clave

        INNER JOIN sncev_geo.municipio m
            ON m.id = p.municipio_residencia_id

        INNER JOIN sncev_geo.estado er
            ON er.clave_inegi = m.estado_clave

        LEFT JOIN sncev.tipo_cartilla tc
            ON tc.id = p.tipo_cartilla_id

        WHERE p.id = @Id
          AND p.eliminado = 0",
            new { Id = id });

        if (paciente is null)
        {
            return NotFound(new
            {
                error = "Paciente no encontrado"
            });
        }

        // ============================================================
        // 2. DOMICILIO VIGENTE
        // ============================================================

        var domicilioVigente =
            await conn.QueryFirstOrDefaultAsync<DomicilioPacienteDto>(@"
        SELECT TOP (1)

            d.id                            AS Id,
            d.paciente_id                   AS PacienteId,

            d.estado_clave                  AS EstadoClave,
            e.nombre                        AS EstadoNombre,

            d.municipio_id                  AS MunicipioId,
            m.clave_inegi                   AS MunicipioClaveInegi,
            m.nombre                        AS MunicipioNombre,

            d.localidad_clave               AS LocalidadClave,
            d.localidad                     AS Localidad,
            d.colonia                       AS Colonia,
            d.codigo_postal                 AS CodigoPostal,

            d.calle                         AS Calle,
            d.numero_exterior               AS NumeroExterior,
            d.numero_interior               AS NumeroInterior,

            d.manzana                       AS Manzana,
            d.lote                          AS Lote,

            d.direccion_texto               AS DireccionTexto,

            CAST(d.latitud AS float)        AS Latitud,
            CAST(d.longitud AS float)       AS Longitud,

            d.ageb_clave                    AS AgebClave,
            d.cvegeo                        AS Cvegeo,
            d.cve_ageb                      AS CveAgeb,

            d.clues_sugerida                AS CluesSugerida,
            es.nombre_unidad                AS CluesSugeridaNombre,
            es.nombre_institucion           AS CluesSugeridaInstitucion,

            d.origen_georreferencia         AS OrigenGeorreferencia,
            d.precision_georreferencia      AS PrecisionGeorreferencia,

            d.vigente                       AS Vigente,
            d.fecha_inicio                  AS FechaInicio,
            d.fecha_fin                     AS FechaFin,
            d.fecha_registro                AS FechaRegistro

        FROM sncev.domicilio_paciente d

        LEFT JOIN sncev_geo.estado e
            ON e.clave_inegi = d.estado_clave

        LEFT JOIN sncev_geo.municipio m
            ON m.id = d.municipio_id

        LEFT JOIN sncev_geo.establecimiento_salud es
            ON es.clues = d.clues_sugerida

        WHERE d.paciente_id = @Id
          AND d.vigente = 1

        ORDER BY d.fecha_inicio DESC;",
            new { Id = id });

        // ============================================================
        // 3. VACUNAS APLICADAS
        // ============================================================

        var vacunas = await conn.QueryAsync(@"
    SELECT
        va.*,
        vc.nombre_generico AS vacuna_nombre,
        vc.clave_censia
    FROM sncev.vacuna_aplicada va
    INNER JOIN sncev.vacuna_catalogo vc
        ON va.vacuna_id = vc.id
    WHERE va.paciente_id = @Id
      AND va.anulada = 0
    ORDER BY va.fecha_aplicacion ASC",
       new { Id = id });

        // ============================================================
        // 4. SOMATOMETRÍA
        // ============================================================

        var somatometria = await conn.QueryAsync(@"
        SELECT *
        FROM sncev.somatometria
        WHERE paciente_id = @Id
        ORDER BY fecha_medicion DESC",
            new { Id = id });

        // ============================================================
        // 5. EXPEDIENTE
        // ============================================================

        return Ok(new
        {
            paciente,
            domicilioVigente,
            vacunasAplicadas = vacunas,
            somatometria
        });
    }
    [HttpGet("{id:guid}/expediente-inactivo")]
    [Authorize(Roles = "Administrador,Auditor")]
    public async Task<IActionResult> ObtenerExpedienteInactivo(Guid id)
    {
        await using var conn = new SqlConnection(GetConnectionString());
        await conn.OpenAsync();

        var paciente = await conn.QueryFirstOrDefaultAsync(@"
        SELECT
            p.id AS Id,
            p.nombre AS Nombre,
            p.primer_apellido AS PrimerApellido,
            p.segundo_apellido AS SegundoApellido,
            p.fecha_nacimiento AS FechaNacimiento,
            p.sexo AS Sexo,
            p.direccion AS Direccion,
            p.localidad AS Localidad,
            p.tutor_nombre AS TutorNombre,
            p.tutor_telefono AS TutorTelefono,
            p.pertence_pueblo_indigena AS PertenecePuebloIndigena,
            p.es_migrante AS EsMigrante,
            p.version_cartilla AS VersionCartilla,
            p.fecha_creacion AS FechaCreacion,
            p.fecha_actualizacion AS FechaActualizacion,
            p.eliminado AS Eliminado,

            m.id AS MunicipioResidenciaId,
            m.nombre AS MunicipioResidencia,
            e.clave_inegi AS EstadoResidenciaClave,
            e.nombre AS EstadoResidencia,

            en.clave_inegi AS EntidadNacimientoClave,
            en.nombre AS EntidadNacimiento,

            tc.id AS TipoCartillaId,
            tc.nombre AS TipoCartilla,

            baja.fecha AS FechaBaja,
            baja.motivo AS MotivoBaja,
            baja.usuario_nombre AS UsuarioBaja,
            baja.usuario_rol AS UsuarioRolBaja

        FROM sncev.paciente p

        INNER JOIN sncev_geo.municipio m
            ON m.id = p.municipio_residencia_id

        INNER JOIN sncev_geo.estado e
            ON e.clave_inegi = m.estado_clave

        INNER JOIN sncev_geo.estado en
            ON en.clave_inegi = p.entidad_nacimiento_clave

        LEFT JOIN sncev.tipo_cartilla tc
            ON tc.id = p.tipo_cartilla_id

        OUTER APPLY
        (
            SELECT TOP (1)
                a.fecha,
                a.motivo,
                a.usuario_nombre,
                a.usuario_rol
            FROM sncev.auditoria_paciente a
            WHERE a.paciente_id = p.id
              AND a.accion = 'BAJA'
            ORDER BY a.fecha DESC
        ) baja

        WHERE p.id = @Id
          AND p.eliminado = 1;
    ", new { Id = id });

        if (paciente is null)
            return NotFound(new
            {
                message = "No se encontró un expediente inactivo con ese identificador."
            });

        var auditoria = await conn.QueryAsync(@"
        SELECT
            id AS Id,
            accion AS Accion,
            motivo AS Motivo,
            usuario_nombre AS UsuarioNombre,
            usuario_rol AS UsuarioRol,
            fecha AS Fecha,
            ip_origen AS IpOrigen
        FROM sncev.auditoria_paciente
        WHERE paciente_id = @Id
        ORDER BY fecha DESC;
    ", new { Id = id });

        return Ok(new
        {
            Paciente = paciente,
            Auditoria = auditoria
        });
    }

    [HttpPost("{id:guid}/reactivar")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> ReactivarPaciente(Guid id)
    {
        await using var conn = new SqlConnection(GetConnectionString());
        await conn.OpenAsync();
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        try
        {
            var paciente = await conn.QueryFirstOrDefaultAsync(@"
                SELECT id, nombre, primer_apellido, segundo_apellido, fecha_nacimiento, sexo,
                       entidad_nacimiento_clave, municipio_residencia_id, ageb_clave, localidad, direccion,
                       tutor_nombre, tutor_telefono, pertence_pueblo_indigena, es_migrante,
                       tipo_cartilla_id, version_cartilla
                FROM sncev.paciente WITH (UPDLOCK, HOLDLOCK)
                WHERE id=@Id AND eliminado=1", new { Id = id }, tx);
            if (paciente is null)
            {
                await tx.RollbackAsync();
                return NotFound(new { error = "Paciente dado de baja no encontrado." });
            }

            await conn.ExecuteAsync(@"
                UPDATE sncev.paciente SET eliminado=0, fecha_actualizacion=SYSDATETIME()
                WHERE id=@Id AND eliminado=1", new { Id = id }, tx);
            var snapshotNuevo = JsonSerializer.Serialize(paciente);
            await RegistrarAuditoriaAsync(conn, tx, id, "REACTIVACION", "Reactivación de paciente", null, snapshotNuevo);
            await tx.CommitAsync();
            return Ok(new { id, message = "Paciente reactivado correctamente." });
        }
        catch { await tx.RollbackAsync(); throw; }
    }
    [HttpGet("/fhir/r4/Patient/{id:guid}")]
    [Produces("application/fhir+json")]
    public async Task<IActionResult> GetFhirPatient(Guid id)
    {
        await using var conn = new SqlConnection(GetConnectionString());
        var p = await conn.QueryFirstOrDefaultAsync(@"
            SELECT id, curp, nombre, primer_apellido, segundo_apellido, fecha_nacimiento, sexo
            FROM sncev.paciente
            WHERE id = @Id AND eliminado = 0", new { Id = id });

        if (p is null)
            return NotFound(new
            {
                resourceType = "OperationOutcome",
                issue = new[] { new { severity = "error", code = "not-found" } }
            });

        var fhirPatient = new Patient
        {
            Id = p.id.ToString(),
            Gender = p.sexo == "M" ? AdministrativeGender.Male :
                     p.sexo == "F" ? AdministrativeGender.Female : AdministrativeGender.Unknown,
            BirthDate = ((DateTime)p.fecha_nacimiento).ToString("yyyy-MM-dd")
        };

        fhirPatient.Identifier.Add(new Identifier
        {
            System = "https://www.gob.mx/curp",
            Value = p.curp
        });

        fhirPatient.Name.Add(new HumanName
        {
            Use = HumanName.NameUse.Official,
            Given = new[] { (string)p.nombre },
            Family = $"{p.primer_apellido} {p.segundo_apellido}".Trim()
        });

        var serializer = new FhirJsonSerializer();
        return Content(serializer.SerializeToString(fhirPatient), "application/fhir+json");
    }

    [HttpGet("bajas")]
    [Authorize(Roles = "Administrador")]
    public async Task<IActionResult> ObtenerPacientesDadosDeBaja()
    {
        await using var conn = new SqlConnection(GetConnectionString());
        await conn.OpenAsync();

        var pacientes = await conn.QueryAsync(@"
        SELECT
            p.id AS Id,
            p.nombre AS Nombre,
            p.primer_apellido AS PrimerApellido,
            p.segundo_apellido AS SegundoApellido,
            p.fecha_nacimiento AS FechaNacimiento,
            p.sexo AS Sexo,

            m.nombre AS MunicipioResidencia,
            e.nombre AS EstadoResidencia,

            tc.nombre AS TipoCartilla,

            a.fecha AS FechaBaja,
            a.motivo AS MotivoBaja,
            a.usuario_nombre AS UsuarioBaja,
            a.usuario_rol AS UsuarioRol

        FROM sncev.paciente p

        INNER JOIN sncev_geo.municipio m
            ON m.id = p.municipio_residencia_id

        INNER JOIN sncev_geo.estado e
            ON e.clave_inegi = m.estado_clave

        LEFT JOIN sncev.tipo_cartilla tc
            ON tc.id = p.tipo_cartilla_id

        OUTER APPLY
        (
            SELECT TOP (1)
                ap.fecha,
                ap.motivo,
                ap.usuario_nombre,
                ap.usuario_rol
            FROM sncev.auditoria_paciente ap
            WHERE ap.paciente_id = p.id
              AND ap.accion = 'BAJA'
            ORDER BY ap.fecha DESC
        ) a

        WHERE p.eliminado = 1

        ORDER BY
            a.fecha DESC,
            p.primer_apellido,
            p.nombre;
    ");

        return Ok(pacientes);
    }
  
    private async System.Threading.Tasks.Task RegistrarAuditoriaAsync(
     SqlConnection conn,
     SqlTransaction tx,
     Guid pacienteId,
     string accion,
     string? motivo,
     string? datosAnteriores,
     string? datosNuevos)
    {
        var usuarioIdTexto = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub") ?? User.FindFirstValue("id");
        Guid? usuarioId = Guid.TryParse(usuarioIdTexto, out var parsed) ? parsed : null;
        var usuarioNombre = User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue("name") ?? User.FindFirstValue("unique_name");
        var usuarioRol = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        if (ip?.Length > 64) ip = ip[..64];

        await conn.ExecuteAsync(@"
            INSERT INTO sncev.auditoria_paciente
            (paciente_id, accion, motivo, usuario_id, usuario_nombre, usuario_rol,
             datos_anteriores, datos_nuevos, ip_origen)
            VALUES
            (@PacienteId, @Accion, @Motivo, @UsuarioId, @UsuarioNombre, @UsuarioRol,
             @DatosAnteriores, @DatosNuevos, @IpOrigen)", new
        {
            PacienteId = pacienteId, Accion = accion, Motivo = motivo, UsuarioId = usuarioId,
            UsuarioNombre = usuarioNombre, UsuarioRol = usuarioRol,
            DatosAnteriores = datosAnteriores, DatosNuevos = datosNuevos, IpOrigen = ip
        }, tx);
    }

    private string GetConnectionString() =>
        _config.GetConnectionString("SqlServerConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:SqlServerConnection no está configurada.");
}
