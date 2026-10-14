-- =============================================================================
-- SISTEMA NACIONAL DE CARTILLA ELECTRÓNICA DE VACUNACIÓN (SNCEV)
-- DDL Módulo 06 — Reglas de Asignación de Biológico por Edad / Población
-- Fuente: asigancion_de_biologico.pdf (Secretaría de Salud)
-- =============================================================================

-- ---------------------------------------------------------------------------
-- TABLA: Catálogo extendido de vacunas (clave ANX del formato oficial)
-- Complementa a sncev.vacuna_catalogo (clave_censia) sin reemplazarla:
-- clave_censia = clave interna del catálogo clínico ya existente
-- clave_anx    = clave del formato de asignación / reporte (ANX001, ANX002...)
-- ---------------------------------------------------------------------------
USE SNCEV;
GO

IF COL_LENGTH('sncev.vacuna_catalogo', 'clave_anx') IS NULL
BEGIN
    ALTER TABLE sncev.vacuna_catalogo ADD clave_anx VARCHAR(20) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'idx_vacuna_catalogo_clave_anx'
                 AND object_id = OBJECT_ID('sncev.vacuna_catalogo'))
BEGIN
    CREATE UNIQUE INDEX idx_vacuna_catalogo_clave_anx
        ON sncev.vacuna_catalogo(clave_anx) WHERE clave_anx IS NOT NULL;
END
GO

-- ---------------------------------------------------------------------------
-- TABLA: Regla de asignación de biológico por edad / grupo poblacional
-- Cada fila = una combinación válida de
--   (vacuna, número de dosis, tipo de dosis, grupo poblacional, sexo)
-- con su rango de edad permitido, expresado en DÍAS DE VIDA para permitir
-- comparación directa contra DATEDIFF(day, fecha_nacimiento, fecha_aplicacion).
-- ---------------------------------------------------------------------------
IF OBJECT_ID('sncev.regla_asignacion_biologico', 'U') IS NULL
BEGIN
    CREATE TABLE sncev.regla_asignacion_biologico (
        id                   INT IDENTITY(1,1) PRIMARY KEY,
        vacuna_clave_anx     VARCHAR(20)   NOT NULL,   -- ANX001, ANX002, ... o 'INFLUENZA'
        vacuna_nombre        VARCHAR(150)  NOT NULL,
        numero_dosis         TINYINT       NULL,       -- NULL = no aplica (conteos sin dosis numerada)
        tipo_dosis           VARCHAR(20)   NOT NULL
            CHECK (tipo_dosis IN ('REGULAR','REZAGADO','REFUERZO','DOSIS_CERO',
                                   'ADICIONAL','UNICA','REVACUNACION','SIN_REGLA_EDAD')),
        grupo_poblacional    VARCHAR(150)  NULL,        -- NULL = población general sin distinción
        sexo                 CHAR(1)       NULL CHECK (sexo IN ('F','M')),  -- NULL = ambos
        edad_texto_original  VARCHAR(60)   NULL,        -- texto tal como aparece en el PDF, trazabilidad
        edad_min_dias        INT           NULL,
        edad_max_dias        INT           NULL,
        observaciones        VARCHAR(400)  NULL,
        requiere_verificacion BIT          NOT NULL DEFAULT 0,  -- filas marcadas para revisión manual
        fecha_creacion       DATETIME2     NOT NULL DEFAULT SYSDATETIME()
    );
    CREATE INDEX idx_regla_vacuna ON sncev.regla_asignacion_biologico(vacuna_clave_anx);
    CREATE INDEX idx_regla_edad ON sncev.regla_asignacion_biologico(edad_min_dias, edad_max_dias);
END
GO

-- ---------------------------------------------------------------------------
-- FUNCIÓN: dado un paciente (fecha_nacimiento, sexo) y una fecha de
-- aplicación, regresa las reglas de vacuna vigentes para esa edad.
-- Úsese como base de la validación de elegibilidad en el API / MobileApp.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('sncev.fn_vacunas_elegibles', 'IF') IS NOT NULL
    DROP FUNCTION sncev.fn_vacunas_elegibles;
GO

CREATE FUNCTION sncev.fn_vacunas_elegibles(
    @FechaNacimiento DATE,
    @Sexo CHAR(1),
    @FechaAplicacion DATE
)
RETURNS TABLE
AS
RETURN
(
    SELECT r.*
    FROM sncev.regla_asignacion_biologico r
    WHERE r.tipo_dosis <> 'SIN_REGLA_EDAD'
      AND r.edad_min_dias IS NOT NULL
      AND DATEDIFF(DAY, @FechaNacimiento, @FechaAplicacion)
          BETWEEN r.edad_min_dias AND r.edad_max_dias
      AND (r.sexo IS NULL OR r.sexo = @Sexo)
);
GO
