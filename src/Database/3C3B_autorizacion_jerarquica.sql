-- SNCEV 3C.3B. Ejecutar en ambiente de desarrollo tras respaldo.
-- La jurisdicción NO se infiere de estado_clave ni de rol Administrador.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF SCHEMA_ID('sncev_admin') IS NULL OR SCHEMA_ID('sncev_geo') IS NULL
 THROW 51000,'Esquemas SNCEV ausentes',1;
IF OBJECT_ID('sncev_admin.usuario','U') IS NULL OR OBJECT_ID('sncev_geo.establecimiento_salud','U') IS NULL
 THROW 51001,'Tablas base ausentes',1;
IF OBJECT_ID('sncev_geo.ambito_jurisdiccional','U') IS NULL
 CREATE TABLE sncev_geo.ambito_jurisdiccional(
  id int IDENTITY(1,1) PRIMARY KEY,
  clave varchar(40) NOT NULL UNIQUE,
  nombre nvarchar(180) NOT NULL,
  activo bit NOT NULL DEFAULT 1);
IF OBJECT_ID('sncev_geo.ambito_clues','U') IS NULL
 CREATE TABLE sncev_geo.ambito_clues(
  ambito_id int NOT NULL REFERENCES sncev_geo.ambito_jurisdiccional(id),
  clues varchar(20) NOT NULL,
  activo bit NOT NULL DEFAULT 1,
  CONSTRAINT PK_sncev_ambito_clues PRIMARY KEY(ambito_id,clues));
IF OBJECT_ID('sncev_geo.establecimiento_jerarquia','U') IS NULL
 CREATE TABLE sncev_geo.establecimiento_jerarquia(
  clues_padre varchar(20) NOT NULL,
  clues_hija varchar(20) NOT NULL,
  activo bit NOT NULL DEFAULT 1,
  CONSTRAINT PK_sncev_est_jerarquia PRIMARY KEY(clues_padre,clues_hija),
  CONSTRAINT CK_sncev_jerarquia_distinta CHECK(clues_padre<>clues_hija));
IF OBJECT_ID('sncev_admin.usuario_ambito','U') IS NULL
 CREATE TABLE sncev_admin.usuario_ambito(
  id bigint IDENTITY(1,1) PRIMARY KEY,
  usuario_id uniqueidentifier NOT NULL REFERENCES sncev_admin.usuario(id),
  tipo varchar(20) NOT NULL CHECK(tipo IN ('JURISDICCION','COORDINACION','UNIDAD')),
  ambito_id int NULL REFERENCES sncev_geo.ambito_jurisdiccional(id),
  clues varchar(20) NULL,
  vigente_desde datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
  vigente_hasta datetime2 NULL,
  activo bit NOT NULL DEFAULT 1,
  CONSTRAINT CK_sncev_usuario_ambito_destino CHECK(
   (tipo='JURISDICCION' AND ambito_id IS NOT NULL AND clues IS NULL) OR
   (tipo IN ('COORDINACION','UNIDAD') AND clues IS NOT NULL AND ambito_id IS NULL)));
IF OBJECT_ID('sncev_admin.usuario_permiso','U') IS NULL
 CREATE TABLE sncev_admin.usuario_permiso(
  usuario_id uniqueidentifier NOT NULL REFERENCES sncev_admin.usuario(id),
  permiso varchar(40) NOT NULL,
  activo bit NOT NULL DEFAULT 1,
  CONSTRAINT PK_sncev_usuario_permiso PRIMARY KEY(usuario_id,permiso),
  CONSTRAINT CK_sncev_permiso CHECK(permiso IN
   ('VACUNA_CONSULTAR','VACUNA_REGISTRAR','VACUNA_CORREGIR','VACUNA_ANULAR','PERSONAL_SUPERVISAR','REPORTES_CONSULTAR')));
-- Catálogo territorial explícito, no otorgar jurisdicción completa por coincidencia de alcaldía.
IF NOT EXISTS(SELECT 1 FROM sncev_geo.ambito_jurisdiccional WHERE clave='AZCAPOTZALCO')
 INSERT sncev_geo.ambito_jurisdiccional(clave,nombre) VALUES('AZCAPOTZALCO',N'Jurisdicción Sanitaria Azcapotzalco');
DECLARE @j int=(SELECT id FROM sncev_geo.ambito_jurisdiccional WHERE clave='AZCAPOTZALCO');
DECLARE @unidades TABLE(clues varchar(20) PRIMARY KEY);
INSERT @unidades VALUES
('DFIMB000171'),('DFIMB000142'),('DFIMB000072'),('DFIMB000084'),
('DFIMB000101'),('DFIMB000113'),('DFIMB000130'),('DFIMB000166'),
('DFIMB000125'),('DFIMB000055'),('DFIMB000060'),('DFIMB000154'),
('DFIMB000096'),('DFIMB000043');
IF EXISTS(SELECT 1 FROM @unidades u LEFT JOIN sncev_geo.establecimiento_salud e ON e.clues=u.clues WHERE e.clues IS NULL)
 THROW 51002,'Hay CLUES no existentes en catálogo; no se aplicaron cambios',1;
INSERT sncev_geo.ambito_clues(ambito_id,clues)
SELECT @j,u.clues FROM @unidades u
WHERE NOT EXISTS(SELECT 1 FROM sncev_geo.ambito_clues a WHERE a.ambito_id=@j AND a.clues=u.clues);
DECLARE @rel TABLE(p varchar(20),h varchar(20),PRIMARY KEY(p,h));
INSERT @rel VALUES
('DFIMB000142','DFIMB000072'),('DFIMB000142','DFIMB000084'),
('DFIMB000142','DFIMB000101'),('DFIMB000142','DFIMB000113'),
('DFIMB000166','DFIMB000125'),('DFIMB000166','DFIMB000055'),
('DFIMB000166','DFIMB000060'),('DFIMB000154','DFIMB000096'),
('DFIMB000154','DFIMB000043');
INSERT sncev_geo.establecimiento_jerarquia(clues_padre,clues_hija)
SELECT r.p,r.h FROM @rel r WHERE NOT EXISTS
(SELECT 1 FROM sncev_geo.establecimiento_jerarquia x WHERE x.clues_padre=r.p AND x.clues_hija=r.h);
-- No asignar permisos a usuarios sin validar sus funciones institucionales.
COMMIT;
SELECT clave,nombre FROM sncev_geo.ambito_jurisdiccional WHERE clave='AZCAPOTZALCO';
