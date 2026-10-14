-- =============================================================================
-- SNCEV — Módulo 12 (v2) — Validación de asignación considerando grupo
-- poblacional — CORRIGE un falso-positivo del primer diseño.
--
-- Bug encontrado al diseñar los casos de prueba: reglas como "Tdpa,
-- embarazadas desde la semana 20 de gestación" no tienen edad tabulada en
-- el PDF (es un criterio clínico/gestacional, no una edad). La versión
-- anterior de este módulo interpretaba "sin edad" como "nunca aplica",
-- lo que habría marcado como erróneas aplicaciones correctas. Ahora:
-- "sin edad en la regla" = "sin restricción de edad" (aplica a cualquier
-- edad siempre que el grupo coincida), igual que ya hacíamos con sexo.
--
-- REEMPLAZA por completo los objetos creados en el módulo 09 y en la v1
-- del módulo 12. Unifica "fuera de rango" y "grupo incongruente" en una
-- sola vista, porque en el fondo son el mismo hallazgo: no existe ninguna
-- regla que respalde la aplicación tal como quedó capturada.
-- =============================================================================
USE SNCEV;
GO
IF OBJECT_ID('sncev_report.vw_grupo_poblacional_incongruente', 'V') IS NOT NULL
    DROP VIEW sncev_report.vw_grupo_poblacional_incongruente;
GO
IF OBJECT_ID('sncev_report.vw_aplicaciones_fuera_de_rango', 'V') IS NOT NULL
    DROP VIEW sncev_report.vw_aplicaciones_fuera_de_rango;
GO
IF OBJECT_ID('sncev_report.vw_grupo_poblacional_no_capturado', 'V') IS NOT NULL
    DROP VIEW sncev_report.vw_grupo_poblacional_no_capturado;
GO

-- ---------------------------------------------------------------------------
-- VISTA PRINCIPAL: aplicaciones sin ninguna regla que las respalde
-- (edad, sexo, dosis y grupo poblacional, todo evaluado juntos).
--
-- Semántica de "sin restricción" por columna de la regla:
--   numero_dosis IS NULL      -> aplica a cualquier número de dosis
--   sexo IS NULL              -> aplica a cualquier sexo
--   edad_min_dias IS NULL     -> aplica a cualquier edad (criterio no etario)
--   grupo_poblacional IS NULL -> aplica a cualquier grupo
--
-- Y grupo_poblacional de la APLICACIÓN en NULL (no capturado) nunca causa
-- que se marque como error — solo aparece en la vista informativa de abajo.
-- ---------------------------------------------------------------------------
CREATE VIEW sncev_report.vw_aplicaciones_sin_regla_valida AS
SELECT
    va.id                                                        AS vacuna_aplicada_id,
    p.id                                                         AS paciente_id,
    p.curp,
    p.sexo,
    p.fecha_nacimiento,
    vc.clave_anx,
    vc.nombre_generico,
    va.numero_dosis,
    va.grupo_poblacional                                         AS grupo_poblacional_capturado,
    va.fecha_aplicacion,
    DATEDIFF(DAY, p.fecha_nacimiento, va.fecha_aplicacion)        AS edad_dias_aplicacion,
    DATEDIFF(MONTH, p.fecha_nacimiento, va.fecha_aplicacion)      AS edad_meses_aplicacion
FROM sncev.vacuna_aplicada va
INNER JOIN sncev.paciente p        ON va.paciente_id = p.id
INNER JOIN sncev.vacuna_catalogo vc ON va.vacuna_id = vc.id
WHERE vc.clave_anx IS NOT NULL
  AND EXISTS (      -- la vacuna sí tiene reglas de asignación (no es de las "sin regla de edad" tipo sueros/inmunoglobulinas)
        SELECT 1 FROM sncev.regla_asignacion_biologico r
        WHERE r.vacuna_clave_anx = vc.clave_anx
          AND r.tipo_dosis <> 'SIN_REGLA_EDAD'
      )
  AND NOT EXISTS (  -- y ninguna de esas reglas respalda esta aplicación puntual
        SELECT 1 FROM sncev.regla_asignacion_biologico r
        WHERE r.vacuna_clave_anx = vc.clave_anx
          AND r.tipo_dosis <> 'SIN_REGLA_EDAD'
          AND (r.numero_dosis = va.numero_dosis OR r.numero_dosis IS NULL)
          AND (r.sexo IS NULL OR r.sexo = p.sexo)
          AND (
                r.edad_min_dias IS NULL
                OR DATEDIFF(DAY, p.fecha_nacimiento, va.fecha_aplicacion)
                   BETWEEN r.edad_min_dias AND r.edad_max_dias
              )
          AND (
                r.grupo_poblacional IS NULL
                OR va.grupo_poblacional IS NULL          -- no capturado: no se penaliza aquí
                OR r.grupo_poblacional = va.grupo_poblacional
              )
      );
GO

-- ---------------------------------------------------------------------------
-- VISTA INFORMATIVA (no es error): aplicaciones donde el único motivo por
-- el que "pasó" la validación de arriba es que no se capturó grupo
-- poblacional, pero existe al menos una regla de esa vacuna/edad/dosis que
-- sí depende de un grupo específico. Sirve para priorizar dónde agregar
-- la captura de grupo en el formulario de aplicación (App 1).
-- ---------------------------------------------------------------------------
CREATE VIEW sncev_report.vw_grupo_poblacional_no_capturado AS
SELECT
    va.id           AS vacuna_aplicada_id,
    p.id            AS paciente_id,
    p.curp,
    vc.clave_anx,
    vc.nombre_generico,
    va.numero_dosis,
    va.fecha_aplicacion,
    DATEDIFF(DAY, p.fecha_nacimiento, va.fecha_aplicacion) AS edad_dias_aplicacion
FROM sncev.vacuna_aplicada va
INNER JOIN sncev.paciente p        ON va.paciente_id = p.id
INNER JOIN sncev.vacuna_catalogo vc ON va.vacuna_id = vc.id
WHERE va.grupo_poblacional IS NULL
  AND EXISTS (
        SELECT 1 FROM sncev.regla_asignacion_biologico r
        WHERE r.vacuna_clave_anx = vc.clave_anx
          AND r.grupo_poblacional IS NOT NULL
          AND (r.numero_dosis = va.numero_dosis OR r.numero_dosis IS NULL)
          AND (
                r.edad_min_dias IS NULL
                OR DATEDIFF(DAY, p.fecha_nacimiento, va.fecha_aplicacion)
                   BETWEEN r.edad_min_dias AND r.edad_max_dias
              )
      );
GO

-- ---------------------------------------------------------------------------
-- FUNCIÓN: fn_vacunas_elegibles — misma corrección de "sin edad = sin
-- restricción" aplicada aquí también.
-- ---------------------------------------------------------------------------
IF OBJECT_ID('sncev.fn_vacunas_elegibles', 'IF') IS NOT NULL
    DROP FUNCTION sncev.fn_vacunas_elegibles;
GO

CREATE FUNCTION sncev.fn_vacunas_elegibles(
    @FechaNacimiento DATE,
    @Sexo CHAR(1),
    @FechaAplicacion DATE,
    @GrupoPoblacional VARCHAR(150) = NULL
)
RETURNS TABLE
AS
RETURN
(
    SELECT r.*
    FROM sncev.regla_asignacion_biologico r
    WHERE r.tipo_dosis <> 'SIN_REGLA_EDAD'
      AND (r.sexo IS NULL OR r.sexo = @Sexo)
      AND (
            r.edad_min_dias IS NULL
            OR DATEDIFF(DAY, @FechaNacimiento, @FechaAplicacion)
               BETWEEN r.edad_min_dias AND r.edad_max_dias
          )
      AND (
            r.grupo_poblacional IS NULL
            OR @GrupoPoblacional IS NULL
            OR r.grupo_poblacional = @GrupoPoblacional
          )
);
GO

-- ---------------------------------------------------------------------------
-- FUNCIÓN AUXILIAR: reglas más cercanas a un caso marcado como sin regla
-- válida — para diagnóstico manual. Ordena primero por si el problema fue
-- el grupo (mismo rango de edad, grupo distinto) y luego por cercanía de
-- edad.
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
        CASE WHEN r.grupo_poblacional IS NOT NULL
                  AND r.grupo_poblacional <> va.grupo_poblacional
             THEN 1 ELSE 0 END                                                        AS grupo_no_coincide,
        ABS(DATEDIFF(DAY, p.fecha_nacimiento, va.fecha_aplicacion)
            - ISNULL(r.edad_min_dias, DATEDIFF(DAY, p.fecha_nacimiento, va.fecha_aplicacion))) AS dias_de_diferencia
    FROM sncev.vacuna_aplicada va
    INNER JOIN sncev.paciente p ON va.paciente_id = p.id
    INNER JOIN sncev.vacuna_catalogo vc ON va.vacuna_id = vc.id
    INNER JOIN sncev.regla_asignacion_biologico r ON r.vacuna_clave_anx = vc.clave_anx
    WHERE va.id = @VacunaAplicadaId
      AND r.tipo_dosis <> 'SIN_REGLA_EDAD'
    ORDER BY grupo_no_coincide ASC, dias_de_diferencia ASC
);
GO

-- ---------------------------------------------------------------------------
-- SP: resumen ejecutivo
-- ---------------------------------------------------------------------------
IF OBJECT_ID('sncev_report.sp_ResumenAplicacionesFueraDeRango', 'P') IS NOT NULL
    DROP PROCEDURE sncev_report.sp_ResumenAplicacionesFueraDeRango;
GO

CREATE PROCEDURE sncev_report.sp_ResumenAplicacionesFueraDeRango
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 'SIN_REGLA_VALIDA (edad/sexo/dosis/grupo)' AS tipo_hallazgo,
           clave_anx, nombre_generico,
           COUNT(*) AS total, MIN(fecha_aplicacion) AS primera_fecha, MAX(fecha_aplicacion) AS ultima_fecha
    FROM sncev_report.vw_aplicaciones_sin_regla_valida
    GROUP BY clave_anx, nombre_generico

    UNION ALL

    SELECT 'GRUPO_NO_CAPTURADO (informativo)', clave_anx, nombre_generico,
           COUNT(*), MIN(fecha_aplicacion), MAX(fecha_aplicacion)
    FROM sncev_report.vw_grupo_poblacional_no_capturado
    GROUP BY clave_anx, nombre_generico

    ORDER BY tipo_hallazgo, total DESC;
END;
GO

-- ---------------------------------------------------------------------------
-- Ejemplos de uso:
--   EXEC sncev_report.sp_ResumenAplicacionesFueraDeRango;
--   SELECT * FROM sncev_report.vw_aplicaciones_sin_regla_valida;
--   SELECT * FROM sncev_report.fn_reglas_cercanas('<vacuna_aplicada_id>');
-- ---------------------------------------------------------------------------
