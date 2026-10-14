using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.IdentityModel.Tokens;

namespace SNCEV.Api.Controllers;

[ApiController]
[Route("v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IConfiguration config, ILogger<AuthController> logger)
    {
        _config = config;
        _logger = logger;
    }

    public sealed class LoginDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public sealed class TokenRefreshDto
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    private sealed class UsuarioAuth
    {
        public Guid Id { get; init; }
        public string Username { get; init; } = string.Empty;
        public string NombreCompleto { get; init; } = string.Empty;
        public string? CluesAsignada { get; init; }
        public string PasswordHash { get; init; } = string.Empty;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto login)
    {
        if (string.IsNullOrWhiteSpace(login.Username) || string.IsNullOrEmpty(login.Password))
            return Unauthorized(new { error = "Usuario o contraseña inválidos" });

        await using var conn = new SqlConnection(GetConnectionString());
        await conn.OpenAsync();

        var passwordColumn = await DetectPasswordHashColumnAsync(conn);
        if (passwordColumn is null)
        {
            _logger.LogError("sncev_admin.usuario no contiene una columna de hash de contraseña compatible. Login bloqueado por seguridad.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Autenticación no configurada correctamente" });
        }

        // passwordColumn sólo puede provenir de la lista blanca de DetectPasswordHashColumnAsync.
        var sql = $@"
            SELECT id AS Id,
                   username AS Username,
                   nombre_completo AS NombreCompleto,
                   clues_asignada AS CluesAsignada,
                   {passwordColumn} AS PasswordHash
            FROM sncev_admin.usuario
            WHERE username = @Username AND activo = 1";

        var user = await conn.QueryFirstOrDefaultAsync<UsuarioAuth>(sql,
            new { Username = login.Username.Trim() });

        if (user is null || !VerifyPassword(login.Password, user.PasswordHash))
            return Unauthorized(new { error = "Usuario o contraseña inválidos" });

        var roles = (await conn.QueryAsync<string>(@"
            SELECT DISTINCT r.nombre
            FROM sncev_admin.usuario_rol ur
            INNER JOIN sncev_admin.rol r ON r.id = ur.rol_id
            WHERE ur.usuario_id = @UsuarioId",
            new { UsuarioId = user.Id })).ToArray();

        var accessToken = GenerarJwtToken(user.Id, user.Username, user.NombreCompleto, user.CluesAsignada, roles);
        var refreshToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        await conn.ExecuteAsync(@"
            INSERT INTO sncev_admin.sesion_token (usuario_id, refresh_token, fecha_expiracion)
            VALUES (@UsuarioId, @RefreshToken, DATEADD(day, 7, SYSDATETIME()))",
            new { UsuarioId = user.Id, RefreshToken = refreshToken });

        return Ok(new
        {
            accessToken,
            refreshToken,
            expiresIn = 3600,
            user = ToUserResponse(user.Id, user.Username, user.NombreCompleto, user.CluesAsignada, roles)
        });
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] TokenRefreshDto refreshDto)
    {
        if (string.IsNullOrWhiteSpace(refreshDto.RefreshToken))
            return Unauthorized(new { error = "Refresh Token inválido o expirado" });

        await using var conn = new SqlConnection(GetConnectionString());
        await conn.OpenAsync();

        var session = await conn.QueryFirstOrDefaultAsync(@"
            SELECT u.id, u.username, u.nombre_completo, u.clues_asignada
            FROM sncev_admin.sesion_token st
            INNER JOIN sncev_admin.usuario u ON st.usuario_id = u.id
            WHERE st.refresh_token = @RefreshToken
              AND st.revocado = 0
              AND st.fecha_expiracion > SYSDATETIME()
              AND u.activo = 1",
            new { RefreshToken = refreshDto.RefreshToken });

        if (session is null)
            return Unauthorized(new { error = "Refresh Token inválido o expirado" });

        Guid userId = session.id;
        var roles = (await conn.QueryAsync<string>(@"
            SELECT DISTINCT r.nombre
            FROM sncev_admin.usuario_rol ur
            INNER JOIN sncev_admin.rol r ON r.id = ur.rol_id
            WHERE ur.usuario_id = @UsuarioId",
            new { UsuarioId = userId })).ToArray();

        var newAccessToken = GenerarJwtToken(userId, (string)session.username,
            (string)session.nombre_completo, session.clues_asignada as string, roles);

        return Ok(new { accessToken = newAccessToken, expiresIn = 3600 });
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var id = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                 ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        var username = User.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
                       ?? User.Identity?.Name;
        var nombre = User.FindFirstValue("nombre") ?? username;
        var clues = User.FindFirstValue("clues");
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct().ToArray();

        return Ok(new
        {
            id,
            username,
            nombre,
            clues,
            roles,
            permissions = Array.Empty<string>()
        });
    }

    private string GenerarJwtToken(Guid id, string username, string nombre, string? clues, IEnumerable<string> roles)
    {
        var secret = _config["Jwt:SecretKey"];
        Console.WriteLine(
$"AuthController JWT Secret cargado: {!string.IsNullOrWhiteSpace(secret)}, " +
$"bytes: {(secret == null ? 0 : Encoding.UTF8.GetByteCount(secret))}");

        var key = new SymmetricSecurityKey(
     Encoding.UTF8.GetBytes(secret));

        var creds = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
    {
        new(JwtRegisteredClaimNames.Sub, id.ToString()),
        new(JwtRegisteredClaimNames.UniqueName, username),
        new(ClaimTypes.Name, username),
        new("nombre", nombre)
    };

        if (!string.IsNullOrWhiteSpace(clues))
            claims.Add(new Claim("clues", clues));

        claims.AddRange(
            roles.Select(role =>
                new Claim(ClaimTypes.Role, role)));

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static object ToUserResponse(Guid id, string username, string nombre, string? clues, string[] roles) => new
    {
        id,
        username,
        nombre,
        clues,
        roles,
        permissions = Array.Empty<string>()
    };

    private async Task<string?> DetectPasswordHashColumnAsync(SqlConnection conn)
    {
        var columns = (await conn.QueryAsync<string>(@"
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = 'sncev_admin' AND TABLE_NAME = 'usuario'"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Lista blanca: nunca interpolar nombres provenientes directamente del cliente.
        foreach (var candidate in new[] { "password_hash", "contrasena_hash", "password_digest", "clave_hash" })
        {
            if (columns.Contains(candidate))
                return $"[{candidate}]";
        }

        return null;
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash)) return false;

        // Formato soportado sin dependencias externas:
        // PBKDF2$SHA256$<iterations>$<salt-base64>$<hash-base64>
        var parts = storedHash.Split('$');
        if (parts.Length != 5 ||
            !parts[0].Equals("PBKDF2", StringComparison.OrdinalIgnoreCase) ||
            !parts[1].Equals("SHA256", StringComparison.OrdinalIgnoreCase) ||
            !int.TryParse(parts[2], out var iterations) || iterations < 100_000)
            return false;

        try
        {
            var salt = Convert.FromBase64String(parts[3]);
            var expected = Convert.FromBase64String(parts[4]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private string GetConnectionString() =>
        _config.GetConnectionString("SqlServerConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:SqlServerConnection no está configurada.");
}
