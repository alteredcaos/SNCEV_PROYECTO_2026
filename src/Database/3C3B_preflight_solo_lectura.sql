-- Diagnóstico de solo lectura. Ejecutar en SNCEV antes de aplicar 3C3B.
SELECT DB_NAME() AS base_actual, @@SERVERNAME AS servidor;
SELECT s.name AS esquema,t.name AS tabla,c.name AS columna,
       ty.name AS tipo,c.max_length AS longitud_bytes,c.is_nullable
FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id
JOIN sys.columns c ON c.object_id=t.object_id
JOIN sys.types ty ON ty.user_type_id=c.user_type_id
WHERE (s.name='sncev_admin' AND t.name='usuario' AND c.name IN ('id','activo','clues_asignada'))
   OR (s.name='sncev_geo' AND t.name='establecimiento_salud' AND c.name IN ('clues','jurisdiccion'))
ORDER BY s.name,t.name,c.column_id;
SELECT clues,nombre_unidad,jurisdiccion
FROM sncev_geo.establecimiento_salud
WHERE clues IN ('DFIMB000171','DFIMB000142','DFIMB000072','DFIMB000084',
'DFIMB000101','DFIMB000113','DFIMB000130','DFIMB000166','DFIMB000125',
'DFIMB000055','DFIMB000060','DFIMB000154','DFIMB000096','DFIMB000043')
ORDER BY clues;
SELECT COUNT(*) AS total_unidades_esperadas_en_catalogo
FROM sncev_geo.establecimiento_salud
WHERE clues IN ('DFIMB000171','DFIMB000142','DFIMB000072','DFIMB000084',
'DFIMB000101','DFIMB000113','DFIMB000130','DFIMB000166','DFIMB000125',
'DFIMB000055','DFIMB000060','DFIMB000154','DFIMB000096','DFIMB000043');
