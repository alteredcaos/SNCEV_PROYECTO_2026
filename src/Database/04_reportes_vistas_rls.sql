-- =============================================================================
-- SISTEMA NACIONAL DE CARTILLA ELECTRÓNICA DE VACUNACIÓN (SNCEV)
-- DDL Módulo 04 — Reportes Analíticos, Tablas de Cobertura y Vistas Resumen (SQL Server T-SQL)
-- =============================================================================
USE SNCEV;
GO
IF OBJECT_ID('sncev_report.cobertura_vacunacion', 'U') IS NULL
BEGIN
    CREATE TABLE sncev_report.cobertura_vacunacion (
        id                      BIGINT          IDENTITY(1,1) PRIMARY KEY,
        anio                    SMALLINT        NOT NULL,
        semana_epidemiologica   SMALLINT        NOT NULL,
        estado_clave            CHAR(2)         NOT NULL REFERENCES sncev_geo.estado(clave_inegi),
        municipio_id            INT             NOT NULL REFERENCES sncev_geo.municipio(id),
        vacuna_id               INT             NOT NULL REFERENCES sncev.vacuna_catalogo(id),
        numero_dosis            TINYINT         NOT NULL DEFAULT 1,
        poblacion_objetivo      INT             NOT NULL DEFAULT 0,
        dosis_aplicadas         INT             NOT NULL DEFAULT 0,
        porcentaje_cobertura    DECIMAL(5,2)    NOT NULL DEFAULT 0.00,
        fecha_calculo           DATETIME2       NOT NULL DEFAULT SYSDATETIME(),
        CONSTRAINT uq_cobertura UNIQUE (anio, semana_epidemiologica, estado_clave, municipio_id, vacuna_id, numero_dosis)
    );

    CREATE INDEX idx_cobertura_semana ON sncev_report.cobertura_vacunacion(anio, semana_epidemiologica);
    CREATE INDEX idx_cobertura_estado ON sncev_report.cobertura_vacunacion(estado_clave);
END
GO

-- ---------------------------------------------------------------------------
-- VISTA: Resumen Cobertura Nacional Anual
-- ---------------------------------------------------------------------------
IF OBJECT_ID('sncev_report.vw_cobertura_nacional_anual', 'V') IS NOT NULL
    DROP VIEW sncev_report.vw_cobertura_nacional_anual;
GO

CREATE VIEW sncev_report.vw_cobertura_nacional_anual AS
SELECT
    c.anio,
    e.clave_inegi AS estado_clave,
    e.nombre AS estado_nombre,
    v.id AS vacuna_id,
    v.nombre_generico AS vacuna_nombre,
    c.numero_dosis,
    SUM(c.poblacion_objetivo) AS total_poblacion_objetivo,
    SUM(c.dosis_aplicadas) AS total_dosis_aplicadas,
    CAST(ROUND(
        CASE WHEN SUM(c.poblacion_objetivo) > 0
             THEN (CAST(SUM(c.dosis_aplicadas) AS FLOAT) / SUM(c.poblacion_objetivo)) * 100.0
             ELSE 0.0 END, 2) AS DECIMAL(5,2)) AS cobertura_porcentaje
FROM sncev_report.cobertura_vacunacion c
INNER JOIN sncev_geo.estado e ON c.estado_clave = e.clave_inegi
INNER JOIN sncev.vacuna_catalogo v ON c.vacuna_id = v.id
GROUP BY c.anio, e.clave_inegi, e.nombre, v.id, v.nombre_generico, c.numero_dosis;
GO
