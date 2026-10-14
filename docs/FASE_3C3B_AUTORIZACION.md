# Fase 3C.3B — Autorización territorial

Rama de implementación aislada. No ejecutar la migración sin respaldo y revisión del catálogo.

## Alcance de esta entrega
- Migración aditiva con 14 CLUES verificadas antes de sembrar relaciones; 9 subordinadas y 5 principales.
- Sin permisos implícitos por rol o por entidad federativa.
- Servicio de autorización con denegación por defecto, permiso explícito y ámbito vigente.
- No modifica SyncV2Controller ni confirma dosis: requiere integración y pruebas posteriores.

## Pendientes para habilitar
1. Validar tipos y restricciones reales de las tablas, y ejecutar en una copia SQL Server.
2. Asignar ámbitos y permisos a usuarios tras aprobación institucional.
3. Incorporar validación en endpoints de captura, corrección, reportes y sincronización.
4. Agregar auditoría, restricciones de jerarquía (ciclos, jurisdicción cruzada) y pruebas.
5. Definir subordinadas de Tezozómoc; por ahora únicamente su CLUES propia.
