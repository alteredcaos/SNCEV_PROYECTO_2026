-- =============================================================================
-- SISTEMA NACIONAL DE CARTILLA ELECTRÓNICA DE VACUNACIÓN (SNCEV)
-- DDL Módulo 05 — Stored Procedure T-SQL de Cálculo de Coberturas Epidemiológicas
-- Traducido de la función PostgreSQL fn_calcular_cobertura
-- =============================================================================
USE SNCEV;
GO
IF OBJECT_ID('sncev_report.sp_CalcularCobertura', 'P') IS NOT NULL
    DROP PROCEDURE sncev_report.sp_CalcularCobertura;
GO

CREATE PROCEDURE sncev_report.sp_CalcularCobertura
    @Anio SMALLINT,
    @Semana SMALLINT,
    @FilasAfectadas INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    BEGIN TRY
        -- 1. Eliminar cálculos previos para el mismo periodo
        DELETE FROM sncev_report.cobertura_vacunacion
        WHERE anio = @Anio AND semana_epidemiologica = @Semana;

        -- 2. Insertar consolidado por Municipio, Vacuna y Dosis
        INSERT INTO sncev_report.cobertura_vacunacion (
            anio,
            semana_epidemiologica,
            estado_clave,
            municipio_id,
            vacuna_id,
            numero_dosis,
            poblacion_objetivo,
            dosis_aplicadas,
            porcentaje_cobertura,
            fecha_calculo
        )
        SELECT
            @Anio AS anio,
            @Semana AS semana_epidemiologica,
            m.estado_clave,
            m.id AS municipio_id,
            v.id AS vacuna_id,
            va.numero_dosis,
            ISNULL(SUM(a.poblacion_menor_5), 1000) AS poblacion_objetivo,
            COUNT(va.id) AS dosis_aplicadas,
            CAST(ROUND(
                CASE WHEN ISNULL(SUM(a.poblacion_menor_5), 1000) > 0
                     THEN (CAST(COUNT(va.id) AS FLOAT) / ISNULL(SUM(a.poblacion_menor_5), 1000)) * 100.0
                     ELSE 0.0 END, 2) AS DECIMAL(5,2)) AS porcentaje_cobertura,
            SYSDATETIME() AS fecha_calculo
        FROM sncev.vacuna_aplicada va
        INNER JOIN sncev.paciente p ON va.paciente_id = p.id
        INNER JOIN sncev_geo.municipio m ON p.municipio_residencia_id = m.id
        INNER JOIN sncev.vacuna_catalogo v ON va.vacuna_id = v.id
        LEFT JOIN sncev_geo.ageb a ON p.ageb_clave = a.clave
        GROUP BY m.estado_clave, m.id, v.id, va.numero_dosis;

        SET @FilasAfectadas = @@ROWCOUNT;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        RAISERROR (@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END;
GO
