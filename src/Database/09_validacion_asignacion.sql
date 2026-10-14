-- =============================================================================
-- SNCEV — Módulo 09 — Validación de asignación de biológico
--
-- ⚠️ SUPERADO por el módulo 12 (v2), que agrega el cruce por
-- grupo_poblacional y corrige un bug de falsos-positivos en reglas sin
-- edad tabulada (p. ej. Tdpa embarazadas). El módulo 12 hace DROP + CREATE
-- de todos los objetos de este archivo. Se conserva 09 solo como
-- referencia histórica de la primera versión (validación por edad+sexo
-- únicamente) — para uso real, corre 01, 02, 06, 07, 08, 11, 12 y omite
-- este archivo, o corre este primero y déjalo sobrescribir por 12.
--
-- Responde la pregunta original: "¿están asignadas correctamente las
-- vacunas a la población conforme al PDF?" — ahora contra datos reales de
-- sncev.vacuna_aplicada.
-- =============================================================================

-- ---------------------------------------------------------------------------
-- VISTA: aplicaciones cuya edad al momento de la aplicación NO cae en
-- ningún rango válido para esa vacuna+dosis (y sexo, cuando aplica) según
-- sncev.regla_asignacion_biologico.
--
-- Nota: solo evalúa vacunas con clave_anx asignada y reglas con edad
-- (tipo_dosis <> 'SIN_REGLA_EDAD'). Vacunas sin regla de edad (sueros,
-- inmunoglobulinas, etc.) quedan fuera del alcance de esta validación por
-- diseño: no tienen un rango que violar.
-- ---------------------------------------------------------------------------
USE SNCEV;
GO

IF OBJECT_ID('sncev_report.vw_aplicaciones_fuera_de_rango', 'V') IS NOT NULL
    DROP VIEW sncev_report.vw_aplicaciones_fuera_de_rango;
GO

CREATE VIEW sncev_report.vw_aplicaciones_fuera_de_rango AS
SELECT
    va.id                                                        AS vacuna_aplicada_id,
    p.id                                                         AS paciente_id,
    p.curp,
    p.sexo,
    p.fecha_nacimiento,
    vc.clave_anx,
    vc.nombre_generico,
    va.numero_dosis,
    va.grupo_poblacional,
    va.fecha_aplicacion,
    DATEDIFF(DAY, p.fecha_nacimiento, va.fecha_aplicacion)        AS edad_dias_aplicacion,
    DATEDIFF(MONTH, p.fecha_nacimiento, va.fecha_aplicacion)      AS edad_meses_aplicacion
FROM sncev.vacuna_aplicada va
INNER JOIN sncev.paciente p        ON va.paciente_id = p.id
INNER JOIN sncev.vacuna_catalogo vc ON va.vacuna_id = vc.id
WHERE vc.clave_anx IS NOT NULL
  AND EXISTS (   -- solo evalúa si existe al menos una regla CON edad para esa vacuna
        SELECT 1 FROM sncev.regla_asignacion_biologico r
        WHERE r.vacuna_clave_anx = vc.clave_anx
          AND r.tipo_dosis <> 'SIN_REGLA_EDAD'
          AND r.edad_min_dias IS NOT NULL
      )
  AND NOT EXISTS (   -- y ninguna de esas reglas cubre esta aplicación puntual
        SELECT 1 FROM sncev.regla_asignacion_biologico r
        WHERE r.vacuna_clave_anx = vc.clave_anx
          AND r.tipo_dosis <> 'SIN_REGLA_EDAD'
          AND r.edad_min_dias IS NOT NULL
          AND (r.numero_dosis = va.numero_dosis OR r.numero_dosis IS NULL)
          AND DATEDIFF(DAY, p.fecha_nacimiento, va.fecha_aplicacion)
              BETWEEN r.edad_min_dias AND r.edad_max_dias
          AND (r.sexo IS NULL OR r.sexo = p.sexo)
          -- si la regla exige un grupo poblacional específico, la aplicación
          -- debe traer capturado ese mismo grupo; si la regla no exige
          -- ninguno (NULL), aplica a cualquiera
          AND (r.grupo_poblacional IS NULL OR r.grupo_poblacional = va.grupo_poblacional)
      );
GO

-- ---------------------------------------------------------------------------
-- FUNCIÓN AUXILIAR: dado un vacuna_aplicada_id fuera de rango, muestra la(s)
-- regla(s) más cercana(s) para ayudar a diagnosticar el caso (¿dosis
-- adelantada?, ¿número de dosis incorrecto?, ¿fecha de nacimiento mal
-- capturada?).
-- ---------------------------------------------------------------------------
IF OBJECT_ID('sncev_report.fn_reglas_cercanas', 'IF') IS NOT NULL
    DROP FUNCTION sncev_report.fn_reglas_cercanas;
GO

CREATE FUNCTION sncev_report.fn_reglas_cercanas(@VacunaAplicadaId UNIQUEIDENTIFIER)
RETURNS TABLE
AS
RETURN
(
    SELECT TOP 5
        r.tipo_dosis,
        r.numero_dosis,
        r.grupo_poblacional,
        r.edad_texto_original,
        r.edad_min_dias,
        r.edad_max_dias,
        ABS(DATEDIFF(DAY, p.fecha_nacimiento, va.fecha_aplicacion) - r.edad_min_dias) AS dias_de_diferencia
    FROM sncev.vacuna_aplicada va
    INNER JOIN sncev.paciente p ON va.paciente_id = p.id
    INNER JOIN sncev.vacuna_catalogo vc ON va.vacuna_id = vc.id
    INNER JOIN sncev.regla_asignacion_biologico r ON r.vacuna_clave_anx = vc.clave_anx
    WHERE va.id = @VacunaAplicadaId
      AND r.tipo_dosis <> 'SIN_REGLA_EDAD'
      AND r.edad_min_dias IS NOT NULL
    ORDER BY dias_de_diferencia ASC
);
GO

-- ---------------------------------------------------------------------------
-- STORED PROCEDURE: resumen ejecutivo — cuántas aplicaciones fuera de rango
-- hay por vacuna, útil para un primer vistazo de calidad de datos.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('sncev_report.sp_ResumenAplicacionesFueraDeRango', 'P') IS NOT NULL
    DROP PROCEDURE sncev_report.sp_ResumenAplicacionesFueraDeRango;
GO

CREATE PROCEDURE sncev_report.sp_ResumenAplicacionesFueraDeRango
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        clave_anx,
        nombre_generico,
        COUNT(*) AS total_fuera_de_rango,
        MIN(fecha_aplicacion) AS primera_fecha,
        MAX(fecha_aplicacion) AS ultima_fecha
    FROM sncev_report.vw_aplicaciones_fuera_de_rango
    GROUP BY clave_anx, nombre_generico
    ORDER BY total_fuera_de_rango DESC;
END;
GO

-- ---------------------------------------------------------------------------
-- Ejemplos de uso:
--
-- 1) Ver todas las aplicaciones sospechosas:
--    SELECT * FROM sncev_report.vw_aplicaciones_fuera_de_rango;
--
-- 2) Resumen por vacuna:
--    EXEC sncev_report.sp_ResumenAplicacionesFueraDeRango;
--
-- 3) Diagnosticar un caso puntual:
--    SELECT * FROM sncev_report.fn_reglas_cercanas('<vacuna_aplicada_id>');
-- ---------------------------------------------------------------------------
