-- =============================================================================
-- SISTEMA NACIONAL DE CARTILLA ELECTRÓNICA DE VACUNACIÓN (SNCEV)
-- DDL Módulo 03 — Seguridad, Usuarios, Roles y Auditoría Inmutable (SQL Server T-SQL)
-- =============================================================================
USE SNCEV;
GO
IF OBJECT_ID('sncev_admin.rol', 'U') IS NULL
BEGIN
    CREATE TABLE sncev_admin.rol (
        id              INT             IDENTITY(1,1) PRIMARY KEY,
        nombre          VARCHAR(50)     NOT NULL UNIQUE,
        descripcion     VARCHAR(200)    NULL
    );

    INSERT INTO sncev_admin.rol (nombre, descripcion) VALUES
    ('Administrador', 'Control total del sistema nacional'),
    ('Capturador', 'Personal de salud en unidades médicas'),
    ('Directivo', 'Consulta de tableros de cobertura y rezago'),
    ('Auditor', 'Revisión bitácora e inspección de integridad');
END
GO

IF OBJECT_ID('sncev_admin.usuario', 'U') IS NULL
BEGIN
    CREATE TABLE sncev_admin.usuario (
        id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        username        VARCHAR(50)      NOT NULL UNIQUE,
        email           VARCHAR(100)     NOT NULL UNIQUE,
        password_hash   VARCHAR(255)     NOT NULL,
        nombre_completo VARCHAR(150)     NOT NULL,
        curp            VARCHAR(18)      NOT NULL,
        clues_asignada  VARCHAR(20)      NOT NULL,
        estado_clave    CHAR(2)          NOT NULL REFERENCES sncev_geo.estado(clave_inegi),
        activo          BIT              NOT NULL DEFAULT 1,
        mfa_secret      VARCHAR(100)     NULL,
        fecha_creacion  DATETIME2        NOT NULL DEFAULT SYSDATETIME()
    );
END
GO

IF OBJECT_ID('sncev_admin.usuario_rol', 'U') IS NULL
BEGIN
    CREATE TABLE sncev_admin.usuario_rol (
        usuario_id      UNIQUEIDENTIFIER NOT NULL REFERENCES sncev_admin.usuario(id),
        rol_id          INT              NOT NULL REFERENCES sncev_admin.rol(id),
        PRIMARY KEY (usuario_id, rol_id)
    );
END
GO

IF OBJECT_ID('sncev_admin.sesion_token', 'U') IS NULL
BEGIN
    CREATE TABLE sncev_admin.sesion_token (
        id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        usuario_id      UNIQUEIDENTIFIER NOT NULL REFERENCES sncev_admin.usuario(id),
        refresh_token   VARCHAR(512)     NOT NULL UNIQUE,
        fecha_expiracion DATETIME2       NOT NULL,
        revocado        BIT              NOT NULL DEFAULT 0,
        fecha_creacion  DATETIME2        NOT NULL DEFAULT SYSDATETIME()
    );
    CREATE INDEX idx_sesion_token_exp ON sncev_admin.sesion_token(fecha_expiracion);
END
GO

IF OBJECT_ID('sncev_audit.audit_log', 'U') IS NULL
BEGIN
    CREATE TABLE sncev_audit.audit_log (
        id              BIGINT           IDENTITY(1,1) PRIMARY KEY,
        usuario_id      UNIQUEIDENTIFIER NULL,
        accion          VARCHAR(50)      NOT NULL,
        modulo          VARCHAR(50)      NOT NULL,
        tabla_afectada  VARCHAR(100)     NOT NULL,
        datos_previos   NVARCHAR(MAX)    NULL,
        datos_nuevos    NVARCHAR(MAX)    NULL,
        resultado       VARCHAR(20)      NOT NULL DEFAULT 'OK',
        ip_origen       VARCHAR(45)      NULL,
        fecha_registro  DATETIME2        NOT NULL DEFAULT SYSDATETIME()
    );
    CREATE INDEX idx_audit_log_fecha ON sncev_audit.audit_log(fecha_registro);
END
GO
