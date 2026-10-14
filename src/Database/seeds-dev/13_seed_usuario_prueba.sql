-- =============================================================================
-- SNCEV — Seed de usuario de prueba (SOLO desarrollo/staging)
-- NO correr contra producción. Usuario: vacunador.prueba / Contraseña: Vacunador2026!
-- El hash de abajo es un BCrypt real (verificado con la librería BCrypt.Net-Next).
-- =============================================================================

IF NOT EXISTS (SELECT 1 FROM sncev_admin.usuario WHERE username = 'vacunador.prueba')
BEGIN
    INSERT INTO sncev_admin.usuario
        (username, email, password_hash, nombre_completo, curp, clues_asignada, estado_clave, activo)
    VALUES (
        'vacunador.prueba',
        'vacunador.prueba@salud.gob.mx',
        '$2b$11$xNpKMay.gz9meknJvri7w.3PxiTOIe76z19EhgTQxeLxf0jSGodLK',  -- Vacunador2026!
        'Vacunador de Prueba',
        'TEST070707HDFAAA07',
        'DFSSA000001',
        '09',
        1
    );

    INSERT INTO sncev_admin.usuario_rol (usuario_id, rol_id)
    SELECT u.id, r.id
    FROM sncev_admin.usuario u, sncev_admin.rol r
    WHERE u.username = 'vacunador.prueba' AND r.nombre = 'Capturador';
END
GO
