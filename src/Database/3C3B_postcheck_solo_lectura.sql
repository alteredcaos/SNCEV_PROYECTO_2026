-- Fase 3C.3B: verificación de migración (solo lectura).
-- Ejecutar únicamente DESPUÉS de aplicar 3C3B_autorizacion_jerarquica.sql en desarrollo.
SET NOCOUNT ON;
IF OBJECT_ID('sncev_geo.ambito_jurisdiccional','U') IS NULL
 OR OBJECT_ID('sncev_geo.ambito_clues','U') IS NULL
 OR OBJECT_ID('sncev_geo.establecimiento_jerarquia','U') IS NULL
 OR OBJECT_ID('sncev_admin.usuario_ambito','U') IS NULL
 OR OBJECT_ID('sncev_admin.usuario_permiso','U') IS NULL
BEGIN
 SELECT 'PENDIENTE' AS resultado, 'Faltan tablas de 3C.3B' AS detalle;
 RETURN;
END;
SELECT j.clave,COUNT(*) AS clues_vinculadas
FROM sncev_geo.ambito_jurisdiccional j
JOIN sncev_geo.ambito_clues c ON c.ambito_id=j.id AND c.activo=1
WHERE j.clave='AZCAPOTZALCO' GROUP BY j.clave;
SELECT h.clues_padre,h.clues_hija
FROM sncev_geo.establecimiento_jerarquia h
WHERE h.activo=1 ORDER BY h.clues_padre,h.clues_hija;
SELECT COUNT(*) AS usuarios_con_ambito FROM sncev_admin.usuario_ambito WHERE activo=1;
SELECT COUNT(*) AS permisos_activos FROM sncev_admin.usuario_permiso WHERE activo=1;
SELECT CASE WHEN (SELECT COUNT(*) FROM sncev_geo.ambito_clues c
 JOIN sncev_geo.ambito_jurisdiccional j ON j.id=c.ambito_id
 WHERE j.clave='AZCAPOTZALCO' AND c.activo=1)=14
 AND (SELECT COUNT(*) FROM sncev_geo.establecimiento_jerarquia WHERE activo=1)=9
 THEN 'OK' ELSE 'REVISAR' END AS validacion_catalogo;
