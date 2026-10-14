#requires -Version 5.1
<#
.SYNOPSIS
  Crea el primer administrador local de SNCEV con PBKDF2-SHA256.

.DESCRIPTION
  - SQL Server local con autenticacion integrada de Windows.
  - Solicita CURP, CLUES y estado localmente.
  - Solicita la contrasena como SecureString.
  - Genera:
      PBKDF2$SHA256$210000$<salt-base64>$<hash-base64>
  - Comprueba que sncev_admin.rol id=1 sea "Administrador".
  - Inserta usuario y usuario_rol en una unica transaccion.
  - Usa los defaults SQL para id, activo y fecha_creacion.
  - No muestra ni guarda la contrasena ni su hash.

  Pensado exclusivamente para el entorno LOCAL de SNCEV.
#>

[CmdletBinding()]
param(
    [string]$Servidor = 'LAPTOP-6VL1O8NQ\SQLEXPRESS',
    [string]$BaseDatos = 'SNCEV',
    [string]$Username = 'admin',
    [string]$Email = 'admin@sncev.local',
    [string]$NombreCompleto = 'Administrador SNCEV',
    [int]$Iteraciones = 210000
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Iteraciones -lt 100000) {
    throw "Iteraciones debe ser >= 100000 para ser compatible con AuthController."
}

function Convert-SecureStringToPlainText {
    param([Parameter(Mandatory)][Security.SecureString]$SecureString)

    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureString)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

function New-SncevPasswordHash {
    param(
        [Parameter(Mandatory)][string]$Password,
        [Parameter(Mandatory)][int]$Iterations
    )

    $salt = New-Object byte[] 32
    $derived = $null
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()

    try {
        $rng.GetBytes($salt)
    }
    finally {
        $rng.Dispose()
    }

    try {
        $pbkdf2 = New-Object Security.Cryptography.Rfc2898DeriveBytes(
            $Password,
            $salt,
            $Iterations,
            [Security.Cryptography.HashAlgorithmName]::SHA256
        )
        try {
            $derived = $pbkdf2.GetBytes(32)
        }
        finally {
            $pbkdf2.Dispose()
        }

        return 'PBKDF2$SHA256${0}${1}${2}' -f `
            $Iterations,
            [Convert]::ToBase64String($salt),
            [Convert]::ToBase64String($derived)
    }
    finally {
        if ($salt) { [Array]::Clear($salt, 0, $salt.Length) }
        if ($derived) { [Array]::Clear($derived, 0, $derived.Length) }
    }
}

function Read-RequiredValue {
    param(
        [Parameter(Mandatory)][string]$Prompt,
        [Parameter(Mandatory)][int]$MaxLength
    )

    while ($true) {
        $value = (Read-Host $Prompt).Trim()
        if ([string]::IsNullOrWhiteSpace($value)) {
            Write-Warning "El valor es obligatorio."
            continue
        }
        if ($value.Length -gt $MaxLength) {
            Write-Warning "El valor no puede exceder $MaxLength caracteres."
            continue
        }
        return $value
    }
}

Write-Host ""
Write-Host "=== SNCEV - Crear administrador local ==="
Write-Host "Servidor : $Servidor"
Write-Host "Base     : $BaseDatos"
Write-Host "Usuario  : $Username"
Write-Host ""

if ([string]::IsNullOrWhiteSpace($Username) -or $Username.Length -gt 50) {
    throw "Username es obligatorio y no puede exceder 50 caracteres."
}
if ([string]::IsNullOrWhiteSpace($Email) -or $Email.Length -gt 100) {
    throw "Email es obligatorio y no puede exceder 100 caracteres."
}
if ([string]::IsNullOrWhiteSpace($NombreCompleto) -or $NombreCompleto.Length -gt 150) {
    throw "NombreCompleto es obligatorio y no puede exceder 150 caracteres."
}

# Estos tres campos son NOT NULL en sncev_admin.usuario.
# Se capturan localmente para no incorporar datos personales al archivo.
while ($true) {
    $Curp = (Read-Host "CURP del administrador (18 caracteres)").Trim().ToUpperInvariant()
    if ($Curp.Length -ne 18) {
        Write-Warning "La CURP debe contener exactamente 18 caracteres."
        continue
    }
    if ($Curp -notmatch '^[A-Z0-9]{18}$') {
        Write-Warning "La CURP solo puede contener letras A-Z y numeros."
        continue
    }
    break
}

$CluesAsignada = Read-RequiredValue `
    -Prompt "CLUES asignada (maximo 20 caracteres)" `
    -MaxLength 20
$CluesAsignada = $CluesAsignada.ToUpperInvariant()

while ($true) {
    $EstadoClave = (Read-Host "Clave de estado (2 caracteres; ej. 09)").Trim().ToUpperInvariant()
    if ($EstadoClave.Length -ne 2) {
        Write-Warning "estado_clave debe contener exactamente 2 caracteres."
        continue
    }
    if ($EstadoClave -notmatch '^[A-Z0-9]{2}$') {
        Write-Warning "estado_clave solo puede contener letras A-Z y numeros."
        continue
    }
    break
}

Write-Host ""
$password1 = Read-Host "Contrasena del administrador" -AsSecureString
$password2 = Read-Host "Confirma la contrasena" -AsSecureString

$plain1 = $null
$plain2 = $null
$passwordHash = $null
$connection = $null
$transaction = $null

try {
    $plain1 = Convert-SecureStringToPlainText $password1
    $plain2 = Convert-SecureStringToPlainText $password2

    if ($plain1 -cne $plain2) {
        throw "Las contrasenas no coinciden."
    }
    if ($plain1.Length -lt 12) {
        throw "La contrasena debe tener al menos 12 caracteres."
    }
    if ($plain1 -notmatch '[A-Z]') { throw "La contrasena debe contener al menos una mayuscula." }
    if ($plain1 -notmatch '[a-z]') { throw "La contrasena debe contener al menos una minuscula." }
    if ($plain1 -notmatch '\d')    { throw "La contrasena debe contener al menos un numero." }
    if ($plain1 -notmatch '[^A-Za-z0-9]') {
        throw "La contrasena debe contener al menos un caracter especial."
    }

    $passwordHash = New-SncevPasswordHash -Password $plain1 -Iterations $Iteraciones

    # Quitar referencias al texto plano tan pronto como sea posible.
    $plain1 = $null
    $plain2 = $null
    $password1.Dispose()
    $password2.Dispose()

    $connectionString =
        "Server=$Servidor;Database=$BaseDatos;Integrated Security=True;" +
        "Encrypt=True;TrustServerCertificate=True;Application Name=SNCEV Crear Admin;"

    $connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)
    $connection.Open()
    $transaction = $connection.BeginTransaction()

    # Verificar que el rol esperado no haya cambiado.
    $cmdRol = $connection.CreateCommand()
    $cmdRol.Transaction = $transaction
    $cmdRol.CommandText = @"
SELECT nombre
FROM sncev_admin.rol
WHERE id = @RolId;
"@
    [void]$cmdRol.Parameters.Add("@RolId", [Data.SqlDbType]::Int)
    $cmdRol.Parameters["@RolId"].Value = 1
    $rolNombre = $cmdRol.ExecuteScalar()

    if ($null -eq $rolNombre -or [string]$rolNombre -ne 'Administrador') {
        throw "No se encontro el rol id=1 con nombre 'Administrador'. No se realizo ningun cambio."
    }

    # Evitar duplicar username.
    $cmdExiste = $connection.CreateCommand()
    $cmdExiste.Transaction = $transaction
    $cmdExiste.CommandText = @"
SELECT COUNT(1)
FROM sncev_admin.usuario
WHERE username = @Username;
"@
    [void]$cmdExiste.Parameters.Add("@Username", [Data.SqlDbType]::VarChar, 50)
    $cmdExiste.Parameters["@Username"].Value = $Username

    if ([int]$cmdExiste.ExecuteScalar() -gt 0) {
        throw "Ya existe el usuario '$Username'. El script no sobrescribe cuentas existentes."
    }

    # Evitar duplicar CURP.
    $cmdExisteCurp = $connection.CreateCommand()
    $cmdExisteCurp.Transaction = $transaction
    $cmdExisteCurp.CommandText = @"
SELECT COUNT(1)
FROM sncev_admin.usuario
WHERE curp = @Curp;
"@
    [void]$cmdExisteCurp.Parameters.Add("@Curp", [Data.SqlDbType]::VarChar, 18)
    $cmdExisteCurp.Parameters["@Curp"].Value = $Curp

    if ([int]$cmdExisteCurp.ExecuteScalar() -gt 0) {
        throw "Ya existe un usuario con esa CURP. No se realizo ningun cambio."
    }

    # SQL Server genera id, activo y fecha_creacion con sus DEFAULT.
    # OUTPUT INSERTED.id recupera el UUID generado por SQL Server.
    $cmdUsuario = $connection.CreateCommand()
    $cmdUsuario.Transaction = $transaction
    $cmdUsuario.CommandText = @"
INSERT INTO sncev_admin.usuario
(
    username,
    email,
    password_hash,
    nombre_completo,
    curp,
    clues_asignada,
    estado_clave,
    mfa_secret
)
OUTPUT INSERTED.id
VALUES
(
    @Username,
    @Email,
    @PasswordHash,
    @NombreCompleto,
    @Curp,
    @CluesAsignada,
    @EstadoClave,
    NULL
);
"@

    [void]$cmdUsuario.Parameters.Add("@Username", [Data.SqlDbType]::VarChar, 50)
    [void]$cmdUsuario.Parameters.Add("@Email", [Data.SqlDbType]::VarChar, 100)
    [void]$cmdUsuario.Parameters.Add("@PasswordHash", [Data.SqlDbType]::VarChar, 255)
    [void]$cmdUsuario.Parameters.Add("@NombreCompleto", [Data.SqlDbType]::VarChar, 150)
    [void]$cmdUsuario.Parameters.Add("@Curp", [Data.SqlDbType]::VarChar, 18)
    [void]$cmdUsuario.Parameters.Add("@CluesAsignada", [Data.SqlDbType]::VarChar, 20)
    [void]$cmdUsuario.Parameters.Add("@EstadoClave", [Data.SqlDbType]::Char, 2)

    $cmdUsuario.Parameters["@Username"].Value = $Username
    $cmdUsuario.Parameters["@Email"].Value = $Email
    $cmdUsuario.Parameters["@PasswordHash"].Value = $passwordHash
    $cmdUsuario.Parameters["@NombreCompleto"].Value = $NombreCompleto
    $cmdUsuario.Parameters["@Curp"].Value = $Curp
    $cmdUsuario.Parameters["@CluesAsignada"].Value = $CluesAsignada
    $cmdUsuario.Parameters["@EstadoClave"].Value = $EstadoClave

    $usuarioIdObj = $cmdUsuario.ExecuteScalar()
    if ($null -eq $usuarioIdObj) {
        throw "SQL Server no devolvio el id del usuario insertado."
    }
    $usuarioId = [Guid]$usuarioIdObj

    # Asignar Administrador.
    $cmdUsuarioRol = $connection.CreateCommand()
    $cmdUsuarioRol.Transaction = $transaction
    $cmdUsuarioRol.CommandText = @"
INSERT INTO sncev_admin.usuario_rol (usuario_id, rol_id)
VALUES (@UsuarioId, @RolId);
"@
    [void]$cmdUsuarioRol.Parameters.Add("@UsuarioId", [Data.SqlDbType]::UniqueIdentifier)
    [void]$cmdUsuarioRol.Parameters.Add("@RolId", [Data.SqlDbType]::Int)
    $cmdUsuarioRol.Parameters["@UsuarioId"].Value = $usuarioId
    $cmdUsuarioRol.Parameters["@RolId"].Value = 1

    if ($cmdUsuarioRol.ExecuteNonQuery() -ne 1) {
        throw "No fue posible asignar el rol Administrador."
    }

    $transaction.Commit()
    $transaction = $null

    Write-Host ""
    Write-Host "Administrador creado correctamente."
    Write-Host "Username : $Username"
    Write-Host "Rol      : Administrador"
    Write-Host "ID       : $usuarioId"
    Write-Host "CLUES    : $CluesAsignada"
    Write-Host "Estado   : $EstadoClave"
    Write-Host ""
    Write-Host "La contrasena y su hash no se muestran por seguridad."
}
catch {
    if ($null -ne $transaction) {
        try { $transaction.Rollback() } catch {}
    }
    Write-Error $_.Exception.Message
    exit 1
}
finally {
    $plain1 = $null
    $plain2 = $null
    $passwordHash = $null

    if ($null -ne $connection) {
        $connection.Dispose()
    }
    if ($null -ne $password1) {
        try { $password1.Dispose() } catch {}
    }
    if ($null -ne $password2) {
        try { $password2.Dispose() } catch {}
    }
}
