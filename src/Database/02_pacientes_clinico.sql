-- =============================================================================
-- SISTEMA NACIONAL DE CARTILLA ELECTRÓNICA DE VACUNACIÓN (SNCEV)
-- DDL Módulo 02 — Pacientes, Esquemas Clínicos y Registro de Vacunación (SQL Server T-SQL)
-- =============================================================================

-- ---------------------------------------------------------------------------
-- TABLA: Pacientes
-- ---------------------------------------------------------------------------

USE SNCEV;
GO
IF OBJECT_ID('sncev.paciente', 'U') IS NULL
BEGIN
    CREATE TABLE sncev.paciente (
        id                      UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        curp                    VARCHAR(18)      NULL UNIQUE,
        curp_cifrada            VARBINARY(512)   NULL, -- AES-256 encrypted
        nombre                  VARCHAR(100)     NOT NULL,
        primer_apellido         VARCHAR(100)     NOT NULL,
        segundo_apellido        VARCHAR(100)     NULL,
        fecha_nacimiento        DATE             NOT NULL,
        sexo                    CHAR(1)          NOT NULL CHECK (sexo IN ('M', 'F', 'I')),
        entidad_nacimiento_clave CHAR(2)         NOT NULL REFERENCES sncev_geo.estado(clave_inegi),
        municipio_residencia_id  INT             NOT NULL REFERENCES sncev_geo.municipio(id),
        ageb_clave              CHAR(13)         NULL REFERENCES sncev_geo.ageb(clave),
        direccion               VARCHAR(255)     NULL,
        tutor_nombre            VARCHAR(200)     NULL,
        tutor_curp              VARCHAR(18)      NULL,
        tutor_telefono          VARCHAR(20)      NULL,
        pertence_pueblo_indigena BIT             NOT NULL DEFAULT 0,
        es_migrante             BIT              NOT NULL DEFAULT 0,
        version_cartilla        VARCHAR(30)      NOT NULL DEFAULT 'Niñas y Niños 0-9',
        fecha_creacion          DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
        fecha_actualizacion     DATETIME2        NOT NULL DEFAULT SYSDATETIME(),
        eliminado               BIT              NOT NULL DEFAULT 0
    );
    CREATE INDEX idx_paciente_curp ON sncev.paciente(curp);
    CREATE INDEX idx_paciente_municipio ON sncev.paciente(municipio_residencia_id);
END
GO

-- ---------------------------------------------------------------------------
-- TABLA: Catálogo de Vacunas
-- ---------------------------------------------------------------------------
IF OBJECT_ID('sncev.vacuna_catalogo', 'U') IS NULL
BEGIN
    CREATE TABLE sncev.vacuna_catalogo (
        id                  INT             IDENTITY(1,1) PRIMARY KEY,
        clave_censia        VARCHAR(20)     NOT NULL UNIQUE,
        nombre_generico     VARCHAR(100)    NOT NULL,
        nombre_comercial    VARCHAR(100)    NULL,
        descripcion         VARCHAR(500)    NULL,
        dosis_totales       TINYINT         NOT NULL DEFAULT 1,
        via_administracion  VARCHAR(10)     NOT NULL DEFAULT 'IM', -- IM, SC, ID, VO
        edad_ideal_meses    INT             NULL,
        intervalo_dias_min  INT             NULL,
        activo              BIT             NOT NULL DEFAULT 1
    );
END
GO

-- ---------------------------------------------------------------------------
-- TABLA: Vacunas Aplicadas (Dosis)
-- ---------------------------------------------------------------------------
IF OBJECT_ID('sncev.vacuna_aplicada', 'U') IS NULL
BEGIN
    CREATE TABLE sncev.vacuna_aplicada (
        id                  UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        paciente_id         UNIQUEIDENTIFIER NOT NULL REFERENCES sncev.paciente(id),
        vacuna_id           INT              NOT NULL REFERENCES sncev.vacuna_catalogo(id),
        numero_dosis        TINYINT          NOT NULL DEFAULT 1,
        fecha_aplicacion    DATE             NOT NULL,
        lote                VARCHAR(50)      NOT NULL,
        laboratorio         VARCHAR(100)     NULL,
        unidad_medica_clues VARCHAR(20)      NOT NULL,
        aplicador_curp      VARCHAR(18)      NULL,
        es_esquema_completo BIT              NOT NULL DEFAULT 0,
        observaciones       VARCHAR(255)     NULL,
        firma_digital_kms   VARCHAR(512)     NULL,
        sincronizado        BIT              NOT NULL DEFAULT 1,
        fecha_registro      DATETIME2        NOT NULL DEFAULT SYSDATETIME()
    );
    CREATE INDEX idx_vacuna_aplicada_paciente ON sncev.vacuna_aplicada(paciente_id);
    CREATE INDEX idx_vacuna_aplicada_fecha ON sncev.vacuna_aplicada(fecha_aplicacion);
END
GO

-- ---------------------------------------------------------------------------
-- TABLA: Somatometría (Peso, Talla, Perímetro Cefálico)
-- ---------------------------------------------------------------------------
IF OBJECT_ID('sncev.somatometria', 'U') IS NULL
BEGIN
    CREATE TABLE sncev.somatometria (
        id                  UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        paciente_id         UNIQUEIDENTIFIER NOT NULL REFERENCES sncev.paciente(id),
        fecha_medicion      DATE             NOT NULL,
        peso_kg             DECIMAL(5,2)     NOT NULL,
        talla_cm            DECIMAL(5,2)     NOT NULL,
        perimetro_cefalico_cm DECIMAL(5,2)   NULL,
        imc                 DECIMAL(4,2)     NULL,
        percentil_peso_edad VARCHAR(20)      NULL,
        percentil_talla_edad VARCHAR(20)     NULL,
        registrado_por      VARCHAR(100)     NULL,
        fecha_registro      DATETIME2        NOT NULL DEFAULT SYSDATETIME()
    );
    CREATE INDEX idx_somatometria_paciente ON sncev.somatometria(paciente_id);
END
GO
