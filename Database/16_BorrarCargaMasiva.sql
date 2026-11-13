/* =========================================================================
   GlamSpaces — Borra la carga de DEMOSTRACIÓN de 15_CargaMasivaSalones.sql (Sprint 7)
   NO es migración: se corre a mano. Solo toca lo ligado a usuarios @demo.glamspaces.test:
   solicitudes, reservaciones y favoritas de sus salones (o hechas por ellos), fotos, paquetes,
   salones y al final esos usuarios. Orden según las llaves foráneas (todas NO ACTION).
   Los archivos de fotos-salones/demo/ NO se borran (son compartidos y se suben a mano).
   @Confirmar = 0 solo cuenta; 1 borra en una transacción (todo o nada).
   ========================================================================= */

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

DECLARE @Confirmar BIT = 0; -- 0 = solo cuenta lo que borraría · 1 = borra

DECLARE @Dominio NVARCHAR(50) = N'@demo.glamspaces.test';

CREATE TABLE #U (Id INT PRIMARY KEY);
INSERT INTO #U SELECT Id FROM Usuarios WHERE Correo LIKE N'%' + @Dominio;
CREATE TABLE #S (Id INT PRIMARY KEY);
INSERT INTO #S SELECT s.Id FROM Salones s JOIN #U u ON u.Id = s.AdminId;

IF NOT EXISTS (SELECT 1 FROM #U)
BEGIN
    PRINT N'No hay carga de demostración que borrar.';
    RETURN;
END

DECLARE @Conteo NVARCHAR(20) = CASE WHEN @Confirmar = 1 THEN N'ANTES' ELSE N'SOLO CONTEO' END;
SELECT @Conteo AS Momento,
       (SELECT COUNT(*) FROM #U) AS Usuarios,
       (SELECT COUNT(*) FROM #S) AS Salones,
       (SELECT COUNT(*) FROM Paquetes x JOIN #S s ON s.Id = x.SalonId) AS Paquetes,
       (SELECT COUNT(*) FROM FotosSalon x JOIN #S s ON s.Id = x.SalonId) AS Fotos,
       (SELECT COUNT(*) FROM Solicitudes x WHERE x.SalonId IN (SELECT Id FROM #S) OR x.ClienteId IN (SELECT Id FROM #U)) AS Solicitudes,
       (SELECT COUNT(*) FROM Reservaciones x WHERE x.SalonId IN (SELECT Id FROM #S) OR x.ClienteId IN (SELECT Id FROM #U)) AS Reservaciones,
       (SELECT COUNT(*) FROM BusquedasFavoritas x WHERE x.SalonId IN (SELECT Id FROM #S) OR x.UsuarioId IN (SELECT Id FROM #U)) AS Favoritas;

IF @Confirmar = 0
BEGIN
    PRINT N'No se borró nada. Vuelve a correr con @Confirmar = 1.';
    RETURN;
END

BEGIN TRY
    BEGIN TRAN;

    DELETE FROM Solicitudes WHERE SalonId IN (SELECT Id FROM #S) OR ClienteId IN (SELECT Id FROM #U);
    DELETE FROM Reservaciones WHERE SalonId IN (SELECT Id FROM #S) OR ClienteId IN (SELECT Id FROM #U);
    DELETE FROM BusquedasFavoritas WHERE SalonId IN (SELECT Id FROM #S) OR UsuarioId IN (SELECT Id FROM #U);
    DELETE FROM FotosSalon WHERE SalonId IN (SELECT Id FROM #S);
    DELETE FROM Paquetes WHERE SalonId IN (SELECT Id FROM #S);
    DELETE FROM Salones WHERE Id IN (SELECT Id FROM #S);
    DELETE FROM Usuarios WHERE Id IN (SELECT Id FROM #U);

    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    DECLARE @Error NVARCHAR(2048) = ERROR_MESSAGE();
    RAISERROR(N'El borrado falló y se deshizo completo (ROLLBACK): %s', 16, 1, @Error);
    RETURN;
END CATCH

SELECT N'DESPUÉS' AS Momento,
       (SELECT COUNT(*) FROM Usuarios WHERE Correo LIKE N'%' + @Dominio) AS Usuarios,
       (SELECT COUNT(*) FROM Salones WHERE Id IN (SELECT Id FROM #S)) AS Salones,
       (SELECT COUNT(*) FROM Paquetes WHERE SalonId IN (SELECT Id FROM #S)) AS Paquetes,
       (SELECT COUNT(*) FROM FotosSalon WHERE SalonId IN (SELECT Id FROM #S)) AS Fotos;
PRINT N'Carga de demostración borrada.';
GO
