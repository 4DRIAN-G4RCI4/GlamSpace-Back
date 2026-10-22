/* =========================================================================
   GlamSpaces — Pruebas de HU-11: Modelo de calendario de disponibilidad (Sprint 4)
   Responsable: Adrian Adonai García Jiménez

   Verifica los criterios de aceptación de HU-11 sobre la tabla Reservaciones,
   el índice único UX_Reservaciones_Confirmada y sp_Salon_Disponibilidad.

   Uso: correr DESPUÉS de 01_Tablas.sql, 02_StoredProcedures.sql, 03_Indices.sql
   y 04_DatosPrueba.sql, en una base local o de prueba.
   Todo corre dentro de una transacción que al final se deshace (ROLLBACK):
   no deja datos y se puede correr las veces que sea.
   ========================================================================= */

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON; -- requerido para escribir en tablas con índices filtrados

DECLARE @Codigo INT, @Mensaje NVARCHAR(200);
DECLARE @Ok INT = 0, @Total INT = 0;
DECLARE @Ocupadas TABLE (FechaEvento DATE);

DECLARE @Encanto INT = (SELECT Id FROM Salones WHERE Nombre = N'[Prueba] Jardín Encanto');
DECLARE @Palmas  INT = (SELECT Id FROM Salones WHERE Nombre = N'[Prueba] Quinta Las Palmas');
DECLARE @Cliente INT = (SELECT Id FROM Usuarios WHERE Correo = 'admin.prueba@glamspaces.com');

IF @Encanto IS NULL OR @Palmas IS NULL OR @Cliente IS NULL
BEGIN
    PRINT 'No se encontraron los datos de prueba. Corre primero Database/04_DatosPrueba.sql.';
    RETURN;
END

BEGIN TRANSACTION;

-- -------------------------------------------------------------------------
-- CA4: el script semilla deja al menos un salón con fechas ocupadas de ejemplo
-- -------------------------------------------------------------------------
DECLARE @Confirmadas INT = (SELECT COUNT(*) FROM Reservaciones WHERE Estado = 'confirmada');
SET @Total += 1;
IF @Confirmadas >= 1
BEGIN SET @Ok += 1; PRINT CONCAT('OK    CA4: el script semilla dejó ', @Confirmadas, ' reservaciones confirmadas de ejemplo.'); END
ELSE PRINT 'FALLA CA4: no hay reservaciones confirmadas de ejemplo.';

-- -------------------------------------------------------------------------
-- CA1: una reservación confirmada aparece como fecha ocupada.
-- Semilla: Jardín Encanto, noviembre 2026 → 14 y 21 confirmadas, 28 pendiente.
-- -------------------------------------------------------------------------
INSERT INTO @Ocupadas
EXEC sp_Salon_Disponibilidad @SalonId = @Encanto, @Anio = 2026, @Mes = 11,
     @Codigo = @Codigo OUTPUT, @Mensaje = @Mensaje OUTPUT;

DECLARE @Lista NVARCHAR(200) = (SELECT STRING_AGG(CONVERT(CHAR(10), FechaEvento, 23), ', ')
                                WITHIN GROUP (ORDER BY FechaEvento) FROM @Ocupadas);
SET @Total += 1;
IF @Codigo = 0 AND @Lista = '2026-11-14, 2026-11-21'
BEGIN SET @Ok += 1; PRINT CONCAT('OK    CA1: fechas ocupadas de Jardín Encanto en nov-2026: ', @Lista, ' (el 28 está pendiente y no ocupa).'); END
ELSE PRINT CONCAT('FALLA CA1: se esperaba 2026-11-14, 2026-11-21 y llegó: ', ISNULL(@Lista, '(nada)'), ' | código ', @Codigo);

-- -------------------------------------------------------------------------
-- CA2: la base de datos impide dos reservaciones confirmadas el mismo día y salón.
-- a) Insertar directamente una segunda confirmada.
-- -------------------------------------------------------------------------
SET @Total += 1;
BEGIN TRY
    INSERT INTO Reservaciones (SalonId, ClienteId, FechaEvento, Estado, FechaCreacion)
    VALUES (@Encanto, @Cliente, '2026-11-14', 'confirmada', GETDATE());
    PRINT 'FALLA CA2a: la base de datos permitió dos confirmadas el 14 de noviembre.';
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() IN (2601, 2627) -- violación de índice único
    BEGIN SET @Ok += 1; PRINT CONCAT('OK    CA2a: la base de datos impidió la segunda confirmada (error ', ERROR_NUMBER(), ').'); END
    ELSE PRINT CONCAT('FALLA CA2a: error inesperado ', ERROR_NUMBER(), ' - ', ERROR_MESSAGE());
END CATCH

-- b) Confirmar una pendiente en un día que ya tiene otra confirmada.
INSERT INTO Reservaciones (SalonId, ClienteId, FechaEvento, Estado, FechaCreacion)
VALUES (@Encanto, @Cliente, '2026-11-21', 'pendiente', GETDATE());
DECLARE @Pendiente INT = SCOPE_IDENTITY();

SET @Total += 1;
BEGIN TRY
    UPDATE Reservaciones SET Estado = 'confirmada' WHERE Id = @Pendiente;
    PRINT 'FALLA CA2b: se pudo confirmar una segunda reservación el 21 de noviembre.';
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() IN (2601, 2627)
    BEGIN SET @Ok += 1; PRINT CONCAT('OK    CA2b: la base de datos impidió confirmar la segunda reservación (error ', ERROR_NUMBER(), ').'); END
    ELSE PRINT CONCAT('FALLA CA2b: error inesperado ', ERROR_NUMBER(), ' - ', ERROR_MESSAGE());
END CATCH

-- -------------------------------------------------------------------------
-- Regla de negocio: solo "confirmada" bloquea la fecha; varias pendientes sí pueden coexistir.
-- -------------------------------------------------------------------------
SET @Total += 1;
BEGIN TRY
    INSERT INTO Reservaciones (SalonId, ClienteId, FechaEvento, Estado, FechaCreacion)
    VALUES (@Encanto, @Cliente, '2026-11-28', 'pendiente', GETDATE());
    SET @Ok += 1;
    PRINT 'OK    Regla: dos reservaciones pendientes el mismo día se permiten (solo la confirmada bloquea).';
END TRY
BEGIN CATCH
    PRINT CONCAT('FALLA Regla: se bloqueó una reservación pendiente (error ', ERROR_NUMBER(), ').');
END CATCH

-- -------------------------------------------------------------------------
-- CA3: un salón sin reservaciones confirmadas no tiene fechas ocupadas
-- (en el calendario, todas sus fechas futuras aparecen como disponibles).
-- -------------------------------------------------------------------------
DELETE FROM @Ocupadas;
INSERT INTO @Ocupadas
EXEC sp_Salon_Disponibilidad @SalonId = @Palmas, @Anio = 2026, @Mes = 11,
     @Codigo = @Codigo OUTPUT, @Mensaje = @Mensaje OUTPUT;

DECLARE @CantidadPalmas INT = (SELECT COUNT(*) FROM @Ocupadas);
SET @Total += 1;
IF @Codigo = 0 AND @CantidadPalmas = 0
BEGIN SET @Ok += 1; PRINT CONCAT('OK    CA3: Quinta Las Palmas no tiene fechas ocupadas ("', @Mensaje, '").'); END
ELSE PRINT CONCAT('FALLA CA3: Quinta Las Palmas regresó ', @CantidadPalmas, ' fechas | código ', @Codigo);

-- Se deshace todo lo que insertaron las pruebas.
ROLLBACK TRANSACTION;

PRINT '-------------------------------------------------------------';
PRINT CONCAT('RESULTADO HU-11: ', @Ok, ' de ', @Total, ' pruebas correctas.');
