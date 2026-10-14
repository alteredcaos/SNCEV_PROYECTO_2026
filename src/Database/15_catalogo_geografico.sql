-- =============================================================================
-- SNCEV — Módulo 15 — Catálogo geográfico INEGI (entidad + municipio)
--
-- sncev_geo.municipio ya existía (clave_inegi, nombre, estado_clave) — se
-- agrega sncev_geo.entidad, que no estaba, más el FK entre ambas. La clave
-- de municipio se mantiene como el compuesto de 5 dígitos que ya usabas
-- (CVE_ENT de 2 dígitos + CVE_MUN de 3 dígitos, ej. '09002'), estándar en
-- INEGI/CONAPO/Salud — no se cambia el diseño existente, solo se completa.
-- =============================================================================
USE SNCEV;
GO
IF OBJECT_ID('sncev_geo.entidad', 'U') IS NULL
BEGIN
    CREATE TABLE sncev_geo.entidad (
        clave_inegi         CHAR(2)       NOT NULL PRIMARY KEY,   -- CVE_ENT, '01'-'32'
        nombre              VARCHAR(100)  NOT NULL,
        nombre_abreviado    VARCHAR(50)   NULL
    );
END
GO

-- FK de municipio -> entidad, solo si todavía no existe
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_municipio_entidad'
)
BEGIN
    ALTER TABLE sncev_geo.municipio
        ADD CONSTRAINT FK_municipio_entidad
        FOREIGN KEY (estado_clave) REFERENCES sncev_geo.entidad(clave_inegi);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'idx_municipio_estado')
    CREATE INDEX idx_municipio_estado ON sncev_geo.municipio(estado_clave);
GO
