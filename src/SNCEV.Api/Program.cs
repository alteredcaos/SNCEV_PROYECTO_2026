using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SNCEV.Api.Services;
using SNCEV.Api.Services.Geografia;
using SNCEV.Api.Services.Establecimientos;
using PdfSharp.Fonts;
using SNCEV.Api.Middleware;
using Microsoft.Data.SqlClient;

//GlobalFontSettings.UseWindowsFontsUnderWindows = true;
//var builder = WebApplication.CreateBuilder(args);


//builder.Services.AddControllers();


GlobalFontSettings.UseWindowsFontsUnderWindows = true;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// DIAGNÓSTICO TEMPORAL DE CONEXIÓN SQL SERVER
// FASE 3C.3B-S
// ============================================

if (builder.Environment.IsDevelopment())
{
    var connectionString =
        builder.Configuration.GetConnectionString("SqlServerConnection");

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        Console.WriteLine("[SNCEV SQL] SqlServerConnection no encontrada.");
    }
    else
    {
        var sql = new SqlConnectionStringBuilder(connectionString);

        Console.WriteLine("======================================");
        Console.WriteLine(" SNCEV - DIAGNÓSTICO SQL SERVER");
        Console.WriteLine("======================================");
        Console.WriteLine($"Servidor: {sql.DataSource}");
        Console.WriteLine($"Base de datos: {sql.InitialCatalog}");
        Console.WriteLine($"Seguridad integrada: {sql.IntegratedSecurity}");
        Console.WriteLine($"Autenticación: {(sql.IntegratedSecurity ? "Windows" : "SQL/otra")}");

        try
        {
            await using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("""
                SELECT
                    ORIGINAL_LOGIN() AS login_original,
                    SUSER_SNAME()    AS login_actual,
                    USER_NAME()      AS usuario_base,
                    IS_MEMBER('db_owner') AS es_db_owner
                """, conn);

            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                Console.WriteLine($"Login original: {reader["login_original"]}");
                Console.WriteLine($"Login actual: {reader["login_actual"]}");
                Console.WriteLine($"Usuario BD: {reader["usuario_base"]}");
                Console.WriteLine($"Es db_owner: {reader["es_db_owner"]}");
            }
        }
        catch (SqlException ex)
        {
            Console.WriteLine($"[SNCEV SQL] Error de conexión. Código: {ex.Number}");
        }

        Console.WriteLine("======================================");
    }
}


builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SNCEV.Api",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Introduce el token JWT obtenido en /v1/auth/login."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddSingleton<AesEncryptionService>();
builder.Services.AddScoped<MotorEsquemaVacunacionService>();
builder.Services.AddScoped<AutorizacionCluesService>();
builder.Services.AddSingleton<KmsSignatureService>();
builder.Services.AddSingleton<IGeografiaService, GeografiaService>();
builder.Services.AddScoped<IEstablecimientosService, EstablecimientosService>();
builder.Services.AddScoped<CartillaPdfService>();
builder.Services.AddScoped<PlanRecorridoPdfService>();
builder.Services.AddScoped<EvaluadorCoberturaNominalService>();

var secretKey = builder.Configuration["Jwt:SecretKey"];

Console.WriteLine(
    $"JWT Secret cargado: {!string.IsNullOrWhiteSpace(secretKey)}, " +
    $"bytes: {(secretKey == null ? 0 : Encoding.UTF8.GetByteCount(secretKey))}"
);


if (string.IsNullOrWhiteSpace(secretKey) || Encoding.UTF8.GetByteCount(secretKey) < 32)
    throw new InvalidOperationException("Jwt:SecretKey debe configurarse externamente y contener al menos 32 bytes.");

var issuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Jwt:Issuer no está configurado.");
var audience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Jwt:Audience no está configurado.");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            JwtBearerDefaults.AuthenticationScheme;

        options.DefaultScheme =
            JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata =
            !builder.Environment.IsDevelopment();

        options.SaveToken = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = issuer,
            ValidAudience = audience,

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(secretKey)),

            ClockSkew = TimeSpan.FromMinutes(1)
        };

        // ============================================
        // DIAGNÓSTICO TEMPORAL JWT
        // ============================================
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine(
                    $"JWT VALIDATION ERROR: " +
                    $"{context.Exception.GetType().Name}");

                Console.WriteLine(
                    $"JWT MESSAGE: " +
                    $"{context.Exception.Message}");

                return Task.CompletedTask;
            },

            OnTokenValidated = context =>
            {
                Console.WriteLine(
                    "JWT VALIDADO CORRECTAMENTE.");

                return Task.CompletedTask;
            },

            OnChallenge = context =>
            {
                Console.WriteLine(
                    $"JWT CHALLENGE: Error={context.Error}; " +
                    $"Description={context.ErrorDescription}");

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Sólo para desarrollo local del portal React. No habilita orígenes públicos.
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalReact", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "https://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("LocalReact");
}

// HTTPS obligatorio fuera del entorno de desarrollo.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseMiddleware<ApiExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
