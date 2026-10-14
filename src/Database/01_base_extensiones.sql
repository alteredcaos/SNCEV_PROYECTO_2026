-- =============================================================================
-- SISTEMA NACIONAL DE CARTILLA ELECTRÓNICA DE VACUNACIÓN (SNCEV)
-- DDL Módulo 01 — Schemas, tablas base y catálogos geográficos (SQL Server T-SQL)
-- Estándar: NOM-024-SSA3-2012 | HL7 FHIR R4
-- =============================================================================
USE SNCEV;
GO
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'sncev')
    EXEC('CREATE SCHEMA sncev;');
GO
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'sncev_admin')
    EXEC('CREATE SCHEMA sncev_admin;');
GO
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'sncev_geo')
    EXEC('CREATE SCHEMA sncev_geo;');
GO
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'sncev_audit')
    EXEC('CREATE SCHEMA sncev_audit;');
GO
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'sncev_report')
    EXEC('CREATE SCHEMA sncev_report;');
GO

-- ---------------------------------------------------------------------------
-- CATÁLOGO: Estados de la República Mexicana
-- ---------------------------------------------------------------------------
IF OBJECT_ID('sncev_geo.estado', 'U') IS NULL
BEGIN
    CREATE TABLE sncev_geo.estado (
        clave_inegi     CHAR(2)         NOT NULL PRIMARY KEY,
        nombre          VARCHAR(60)     NOT NULL,
        abreviatura     VARCHAR(4)      NOT NULL,
        activo          BIT             NOT NULL DEFAULT 1
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sncev_geo.estado)
BEGIN
    INSERT INTO sncev_geo.estado (clave_inegi, nombre, abreviatura) VALUES
    ('01','Aguascalientes','AGS'),('02','Baja California','BC'),
    ('03','Baja California Sur','BCS'),('04','Campeche','CAM'),
    ('05','Coahuila','COAH'),('06','Colima','COL'),
    ('07','Chiapas','CHIS'),('08','Chihuahua','CHIH'),
    ('09','Ciudad de México','CDMX'),('10','Durango','DGO'),
    ('11','Guanajuato','GTO'),('12','Guerrero','GRO'),
    ('13','Hidalgo','HGO'),('14','Jalisco','JAL'),
    ('15','Estado de México','MEX'),('16','Michoacán','MICH'),
    ('17','Morelos','MOR'),('18','Nayarit','NAY'),
    ('19','Nuevo León','NL'),('20','Oaxaca','OAX'),
    ('21','Puebla','PUE'),('22','Querétaro','QRO'),
    ('23','Quintana Roo','QROO'),('24','San Luis Potosí','SLP'),
    ('25','Sinaloa','SIN'),('26','Sonora','SON'),
    ('27','Tabasco','TAB'),('28','Tamaulipas','TMP'),
    ('29','Tlaxcala','TLAX'),('30','Veracruz','VER'),
    ('31','Yucatán','YUC'),('32','Zacatecas','ZAC');
END
GO

-- ---------------------------------------------------------------------------
-- CATÁLOGO: Municipios
-- ---------------------------------------------------------------------------
IF OBJECT_ID('sncev_geo.municipio', 'U') IS NULL
BEGIN
    CREATE TABLE sncev_geo.municipio (
        id              INT             IDENTITY(1,1) PRIMARY KEY,
        clave_inegi     CHAR(5)         NOT NULL UNIQUE,
        nombre          VARCHAR(100)    NOT NULL,
        estado_clave    CHAR(2)         NOT NULL REFERENCES sncev_geo.estado(clave_inegi),
        activo          BIT             NOT NULL DEFAULT 1
    );
    CREATE INDEX idx_municipio_estado ON sncev_geo.municipio(estado_clave);
END
GO

-- ---------------------------------------------------------------------------
-- CATÁLOGO: AGEBs (Área Geoestadística Básica)
-- ---------------------------------------------------------------------------
IF OBJECT_ID('sncev_geo.ageb', 'U') IS NULL
BEGIN
    CREATE TABLE sncev_geo.ageb (
        clave               CHAR(13)        NOT NULL PRIMARY KEY,
        nombre              VARCHAR(200)    NULL,
        tipo                CHAR(1)         NOT NULL DEFAULT 'U' CHECK (tipo IN ('U','R')),
        municipio_id        INT             NOT NULL REFERENCES sncev_geo.municipio(id),
        estado_clave        CHAR(2)         NOT NULL REFERENCES sncev_geo.estado(clave_inegi),
        poblacion_total     INT             NULL,
        poblacion_menor_5   INT             NULL,
        poblacion_5_14      INT             NULL,
        poblacion_15_49_f   INT             NULL,
        poblacion_60_mas    INT             NULL
    );
    CREATE INDEX idx_ageb_municipio ON sncev_geo.ageb(municipio_id);
END
GO
