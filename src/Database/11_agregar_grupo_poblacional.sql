-- =============================================================================
-- SNCEV — Módulo 11 — Captura de grupo poblacional en la aplicación de vacuna
--
-- Cierra la brecha señalada en el módulo 09/10: hoy la validación solo
-- compara edad + sexo. Vacunas como Tdpa, VSR, COVID-19 o los refuerzos de
-- Influenza dependen de PARA QUIÉN es la dosis (embarazada, personal de
-- salud, jornalero agrícola, población de riesgo...), no solo de la edad.
--
-- Diseño: catálogo controlado (no texto libre) para que los valores
-- coincidan exactamente con sncev.regla_asignacion_biologico.grupo_poblacional
-- y el cruce funcione. Se puebla automáticamente con los valores que ya
-- usa la tabla de reglas.
-- =============================================================================

-- ---------------------------------------------------------------------------
-- CATÁLOGO: grupos poblacionales usados por las reglas de asignación
-- ---------------------------------------------------------------------------

USE SNCEV;
GO
IF OBJECT_ID('sncev.grupo_poblacional_catalogo', 'U') IS NULL
BEGIN
    CREATE TABLE sncev.grupo_poblacional_catalogo (
        clave        VARCHAR(150) NOT NULL PRIMARY KEY,
        descripcion  VARCHAR(300) NULL,
        activo       BIT          NOT NULL DEFAULT 1
    );
END
GO

INSERT INTO sncev.grupo_poblacional_catalogo (clave)
SELECT DISTINCT r.grupo_poblacional
FROM sncev.regla_asignacion_biologico r
WHERE r.grupo_poblacional IS NOT NULL
  AND NOT EXISTS (
        SELECT 1 FROM sncev.grupo_poblacional_catalogo g
        WHERE g.clave = r.grupo_poblacional
      );
GO

-- Un valor explícito para "población general sin distinción", útil para
-- capturarlo activamente en vez de dejar el campo en NULL (ver más abajo
-- por qué importa la diferencia entre "no capturado" y "población general").
IF NOT EXISTS (SELECT 1 FROM sncev.grupo_poblacional_catalogo WHERE clave = 'POBLACION_GENERAL')
    INSERT INTO sncev.grupo_poblacional_catalogo (clave, descripcion)
    VALUES ('POBLACION_GENERAL', 'Sin pertenencia a un grupo poblacional específico');
GO

-- ---------------------------------------------------------------------------
-- COLUMNA: sncev.vacuna_aplicada.grupo_poblacional
--
-- NULL = "no capturado" (dato desconocido, típico de captura retroactiva).
-- 'POBLACION_GENERAL' = capturado explícitamente como sin grupo especial.
-- Esta distinción importa para la validación: NULL no debe generar falsos
-- positivos sobre datos históricos que nunca preguntaron esto; un valor
-- explícito sí se valida estrictamente contra la regla.
-- ---------------------------------------------------------------------------
IF COL_LENGTH('sncev.vacuna_aplicada', 'grupo_poblacional') IS NULL
BEGIN
    ALTER TABLE sncev.vacuna_aplicada
        ADD grupo_poblacional VARCHAR(150) NULL
            REFERENCES sncev.grupo_poblacional_catalogo(clave);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_vacuna_aplicada_grupo')
    CREATE INDEX idx_vacuna_aplicada_grupo ON sncev.vacuna_aplicada(grupo_poblacional);
GO
