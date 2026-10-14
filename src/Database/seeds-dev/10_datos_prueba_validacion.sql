-- =============================================================================
-- SNCEV — Módulo 10 — Datos de prueba para validar el motor de reglas
--
-- Crea pacientes sintéticos y aplicaciones de vacuna DELIBERADAMENTE mezcladas
-- (algunas correctas, algunas fuera de rango) para poder correr
-- sncev_report.vw_aplicaciones_fuera_de_rango / sp_ResumenAplicacionesFueraDeRango
-- de punta a punta ANTES de tener datos reales, y confirmar que el motor
-- detecta lo que debe detectar.
--
-- Requiere haber corrido antes, en orden: 01, 02, 06, 07, 08, 11, 12.
-- (los casos 9 y 10 usan la columna grupo_poblacional que agrega el módulo 11)
-- Los CURP son ficticios (no corresponden a personas reales).
-- =============================================================================

-- ---------------------------------------------------------------------------
-- Catálogo geográfico mínimo (por si la BD está vacía)
-- ---------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sncev_geo.municipio WHERE clave_inegi = '09002')
BEGIN
    INSERT INTO sncev_geo.municipio (clave_inegi, nombre, estado_clave)
    VALUES ('09002', 'Azcapotzalco', '09');
END
GO

DECLARE @MunicipioId INT = (SELECT TOP 1 id FROM sncev_geo.municipio WHERE clave_inegi = '09002');
DECLARE @ClavesQ VARCHAR(2) = '09'; -- CDMX

-- ---------------------------------------------------------------------------
-- Pacientes de prueba
-- Las fechas de nacimiento se calculan relativas a GETDATE() para que el
-- caso siga siendo válido sin importar cuándo corras el script.
-- ---------------------------------------------------------------------------
DECLARE @P1 UNIQUEIDENTIFIER = NEWID();  -- recién nacido, hoy mismo
DECLARE @P2 UNIQUEIDENTIFIER = NEWID();  -- ~9 meses
DECLARE @P3 UNIQUEIDENTIFIER = NEWID();  -- 13 años (para probar SRP)
DECLARE @P4 UNIQUEIDENTIFIER = NEWID();  -- mujer, 10 años (Td)
DECLARE @P5 UNIQUEIDENTIFIER = NEWID();  -- hombre, 10 años (Td)
DECLARE @P6 UNIQUEIDENTIFIER = NEWID();  -- 2 meses (Hexavalente correcto)

INSERT INTO sncev.paciente
    (id, curp, nombre, primer_apellido, fecha_nacimiento, sexo, entidad_nacimiento_clave, municipio_residencia_id)
VALUES
    (@P1, 'TEST010101HDFAAA01', 'Prueba Uno',   'Sintetico', CAST(GETDATE() AS DATE),              'M', @ClavesQ, @MunicipioId),
    (@P2, 'TEST020202HDFAAA02', 'Prueba Dos',   'Sintetico', DATEADD(DAY, -270, CAST(GETDATE() AS DATE)), 'F', @ClavesQ, @MunicipioId),
    (@P3, 'TEST030303HDFAAA03', 'Prueba Tres',  'Sintetico', DATEADD(YEAR, -13, CAST(GETDATE() AS DATE)), 'M', @ClavesQ, @MunicipioId),
    (@P4, 'TEST040404MDFAAA04', 'Prueba Cuatro','Sintetico', DATEADD(YEAR, -10, CAST(GETDATE() AS DATE)), 'F', @ClavesQ, @MunicipioId),
    (@P5, 'TEST050505HDFAAA05', 'Prueba Cinco', 'Sintetico', DATEADD(YEAR, -10, CAST(GETDATE() AS DATE)), 'M', @ClavesQ, @MunicipioId),
    (@P6, 'TEST060606HDFAAA06', 'Prueba Seis',  'Sintetico', DATEADD(DAY, -61, CAST(GETDATE() AS DATE)),  'M', @ClavesQ, @MunicipioId);

-- ---------------------------------------------------------------------------
-- Aplicaciones de prueba
-- ---------------------------------------------------------------------------
DECLARE @BCG      INT = (SELECT id FROM sncev.vacuna_catalogo WHERE clave_anx = 'ANX001');
DECLARE @HEXA     INT = (SELECT id FROM sncev.vacuna_catalogo WHERE clave_anx = 'ANX003');
DECLARE @SRP      INT = (SELECT id FROM sncev.vacuna_catalogo WHERE clave_anx = 'ANX008');
DECLARE @TD       INT = (SELECT id FROM sncev.vacuna_catalogo WHERE clave_anx = 'ANX013');
DECLARE @TDPA     INT = (SELECT id FROM sncev.vacuna_catalogo WHERE clave_anx = 'ANX014');

-- Paciente extra para probar el cruce por grupo_poblacional (módulos 11/12)
DECLARE @P7 UNIQUEIDENTIFIER = NEWID();  -- mujer, 28 años
INSERT INTO sncev.paciente
    (id, curp, nombre, primer_apellido, fecha_nacimiento, sexo, entidad_nacimiento_clave, municipio_residencia_id)
VALUES
    (@P7, 'TEST070707MDFAAA07', 'Prueba Siete', 'Sintetico', DATEADD(YEAR, -28, CAST(GETDATE() AS DATE)), 'F', @ClavesQ, @MunicipioId);

INSERT INTO sncev.vacuna_aplicada
    (paciente_id, vacuna_id, numero_dosis, fecha_aplicacion, lote, unidad_medica_clues, grupo_poblacional)
VALUES
    -- 1) BCG dosis 1 el día de nacimiento -> CORRECTO (regla: primeras 24 hrs, 0 días)
    (@P1, @BCG, 1, CAST(GETDATE() AS DATE), 'LOTE-TEST-001', 'DFSSA000001', NULL),

    -- 2) BCG dosis 2 (no existe en el esquema: BCG es dosis única) -> FUERA DE RANGO
    (@P1, @BCG, 2, CAST(GETDATE() AS DATE), 'LOTE-TEST-002', 'DFSSA000001', NULL),

    -- 3) Hexavalente dosis 1 a los ~61 días (paciente P6, nace hace 61 días) -> CORRECTO (rango 60-89 días)
    (@P6, @HEXA, 1, CAST(GETDATE() AS DATE), 'LOTE-TEST-003', 'DFSSA000001', NULL),

    -- 4) Hexavalente dosis 1 aplicada a los 10 días de nacido (paciente P2, usando una fecha temprana) -> FUERA DE RANGO (antes de los 60 días)
    (@P2, @HEXA, 1, DATEADD(DAY, 10, DATEADD(DAY, -270, CAST(GETDATE() AS DATE))), 'LOTE-TEST-004', 'DFSSA000001', NULL),

    -- 5) SRP dosis 1 a los 3 meses de nacido (paciente P3, usando fecha relativa a su nacimiento) -> FUERA DE RANGO
    --    (ninguna regla de SRP dosis 1 cubre los 3 meses: dosis-cero es 6-11m, regular es 12m, rezagado arranca en 13m)
    (@P3, @SRP, 1, DATEADD(MONTH, 3, DATEADD(YEAR, -13, CAST(GETDATE() AS DATE))), 'LOTE-TEST-005', 'DFSSA000001', NULL),

    -- 6) SRP dosis 1 a los 13 meses de nacido (mismo paciente, otra dosis) -> CORRECTO (rezagado 13-17 meses)
    (@P3, @SRP, 1, DATEADD(MONTH, 13, DATEADD(YEAR, -13, CAST(GETDATE() AS DATE))), 'LOTE-TEST-006', 'DFSSA000001', NULL),

    -- 7) Td dosis 1, mujer de 10 años -> CORRECTO (regla mujeres "de 7 a 14 años" sí existe)
    (@P4, @TD, 1, CAST(GETDATE() AS DATE), 'LOTE-TEST-007', 'DFSSA000001', NULL),

    -- 8) Td dosis 1, hombre de 10 años -> FUERA DE RANGO
    --    (el PDF fuente NO define regla para hombres de 7 a 14 años en primera dosis;
    --     su rango arranca hasta "de 15 a 19 años" — vacío real en el documento oficial,
    --     no un error de esta prueba. Este caso sirve para detectar precisamente ese
    --     tipo de asimetría documentada.)
    (@P5, @TD, 1, CAST(GETDATE() AS DATE), 'LOTE-TEST-008', 'DFSSA000001', NULL),

    -- 9) Tdpa, mujer de 28 años, grupo capturado correctamente como embarazada
    --    -> requiere módulos 11/12. CORRECTO (coincide edad + grupo con la regla)
    (@P7, @TDPA, 1, CAST(GETDATE() AS DATE), 'LOTE-TEST-009', 'DFSSA000001', 'EMBARAZADA_DESDE_SEMANA_20'),

    -- 10) Tdpa, mismo paciente pero capturado como "personal educativo"
    --     (grupo real que sí existe en el catálogo, pero Tdpa no tiene regla
    --     para ese grupo) -> requiere módulos 11/12. INCONGRUENTE
    --     (aparece en vw_grupo_poblacional_incongruente, no en la vista de
    --     "fuera de rango" porque la EDAD sí encaja en alguna regla de Tdpa,
    --     lo que falla es el grupo)
    (@P7, @TDPA, 1, CAST(GETDATE() AS DATE), 'LOTE-TEST-010', 'DFSSA000001', 'PERSONAL_EDUCATIVO');
GO

-- ---------------------------------------------------------------------------
-- Resultado esperado al correr la validación (requiere módulo 12 v2):
--
--   EXEC sncev_report.sp_ResumenAplicacionesFueraDeRango;
--
-- tipo_hallazgo = 'SIN_REGLA_VALIDA (edad/sexo/dosis/grupo)', debe regresar:
--   ANX001 BCG                     -> 1 caso  (la dosis 2 inexistente)
--   ANX003 Hexavalente...          -> 1 caso  (aplicada a los 10 días)
--   ANX008 SRP Triple Viral...     -> 1 caso  (aplicada a los 3 meses)
--   ANX013 Td Tétanico Diftérico   -> 1 caso  (hombre de 10 años)
--   ANX014 Tdpa (embarazadas)      -> 1 caso  (caso 10: grupo "personal educativo")
--
-- Y NO debe aparecer ninguna fila para los casos marcados como CORRECTO arriba
-- (1, 3, 6, 7 y 9) — en particular el caso 9 (Tdpa, embarazada) es el que
-- prueba que "sin edad tabulada" en la regla se interprete como "sin
-- restricción de edad" y no como "nunca aplica".
--
-- Para ver el detalle:
--   SELECT * FROM sncev_report.vw_aplicaciones_sin_regla_valida
--   WHERE curp LIKE 'TEST%' ORDER BY clave_anx;
-- ---------------------------------------------------------------------------

-- ---------------------------------------------------------------------------
-- Limpieza (ejecutar cuando termines de probar)
-- ---------------------------------------------------------------------------
-- DELETE FROM sncev.vacuna_aplicada WHERE lote LIKE 'LOTE-TEST-%';
-- DELETE FROM sncev.paciente WHERE curp LIKE 'TEST%';
