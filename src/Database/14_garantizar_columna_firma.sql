-- =============================================================================
-- SNCEV — Módulo 14 — Garantiza columna firma_digital_kms en vacuna_aplicada
--
-- NOTA: mi sandbox se reinició y perdí acceso al ZIP original para releer
-- el DDL exacto de 02_pacientes_clinico.sql. VacunaAplicadaLocal.cs (App 1)
-- ya tiene el campo FirmaDigitalKms, lo que sugiere que la columna existía
-- en el diseño original — pero para no arriesgar el INSERT de
-- VacunasAplicadasController.cs contra una columna que quizás no exista,
-- este guard la agrega si falta. Si ya existe, no hace nada.
-- =============================================================================
USE SNCEV;
GO
IF COL_LENGTH('sncev.vacuna_aplicada', 'firma_digital_kms') IS NULL
BEGIN
    ALTER TABLE sncev.vacuna_aplicada ADD firma_digital_kms VARCHAR(500) NULL;
END
GO
