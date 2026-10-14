-- =============================================================================
-- SNCEV — Módulo 08 — Vincular vacuna_catalogo con las claves ANX del
-- formato de asignación de biológico
--
-- Si el registro ya existe (mismo nombre_generico), solo actualiza clave_anx.
-- Si no existe, lo inserta con datos mínimos — AJUSTA nombre_comercial,
-- dosis_totales, via_administracion según tu catálogo clínico real antes de
-- usarlo en producción; aquí solo se garantiza la clave_anx para poder
-- correlacionar con sncev.regla_asignacion_biologico.
-- =============================================================================
USE SNCEV;
GO
MERGE sncev.vacuna_catalogo AS destino
USING (VALUES
    ('ANX001', 'BCG',                                             1, 'ID'),
    ('ANX002', 'Hepatitis B',                                     3, 'IM'),
    ('ANX003', 'Hexavalente Acelular (DPaT+IPV+Hib+HB)',          4, 'IM'),
    ('ANX004', 'DPT',                                             1, 'IM'),
    ('ANX005', 'Rotavirus (RV1)',                                 2, 'VO'),
    ('ANX006', 'Neumocócica Conjugada 13 valente',                3, 'IM'),
    ('ANX007', 'Neumocócica Conjugada 20 valente',                3, 'IM'),
    ('ANX008', 'SRP Triple Viral (Sarampión-Rubéola-Parotiditis)',2, 'SC'),
    ('ANX009', 'SR Doble Viral (Sarampión-Rubéola)',              2, 'SC'),
    ('ANX010', 'VPH (Virus del Papiloma Humano)',                 3, 'IM'),
    ('ANX011', 'Varicela',                                        1, 'SC'),
    ('ANX012', 'Hepatitis A',                                     1, 'IM'),
    ('ANX013', 'Td Tétanico Diftérico',                           4, 'IM'),
    ('ANX014', 'Tdpa (embarazadas)',                              1, 'IM'),
    ('ANX015', 'Virus Sincitial Respiratorio (VSR)',              1, 'IM'),
    ('ANX016', 'COVID-19',                                        2, 'IM'),
    ('ANX017', 'Antitifoídica',                                   3, 'IM'),
    ('ANX018', 'Inmunoglobulina Antitetánica',                    1, 'IM'),
    ('ANX019', 'Inmunoglobulina Antitetánica Equina',             1, 'IM'),
    ('ANX020', 'Inmunoglobulina Diftérica Equina',                1, 'IM'),
    ('ANX021', 'Sueros y Faboterápicos',                          1, 'IM'),
    ('ANX022', 'Otros Biológicos',                                1, 'IM'),
    ('INFLUENZA', 'Influenza Estacional',                         2, 'IM')
) AS origen (clave_anx, nombre_generico, dosis_totales, via_administracion)
ON destino.clave_anx = origen.clave_anx
   OR (destino.clave_anx IS NULL AND destino.nombre_generico = origen.nombre_generico)
WHEN MATCHED THEN
    UPDATE SET destino.clave_anx = origen.clave_anx
WHEN NOT MATCHED BY TARGET THEN
    INSERT (clave_censia, nombre_generico, dosis_totales, via_administracion, clave_anx, activo)
    VALUES (origen.clave_anx, origen.nombre_generico, origen.dosis_totales, origen.via_administracion, origen.clave_anx, 1);
GO

-- Verificación rápida: vacunas del catálogo de reglas sin contraparte en vacuna_catalogo
SELECT DISTINCT r.vacuna_clave_anx, r.vacuna_nombre
FROM sncev.regla_asignacion_biologico r
LEFT JOIN sncev.vacuna_catalogo v ON v.clave_anx = r.vacuna_clave_anx
WHERE v.id IS NULL;
GO
