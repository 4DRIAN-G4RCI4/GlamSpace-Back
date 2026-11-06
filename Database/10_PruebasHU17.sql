/* =========================================================================
   GlamSpaces — Pruebas de la HU-17 (estado de solicitud y bloqueo de fecha)
   ⚠️ SOLO PARA BASES LOCALES O DE PRUEBA. Requiere 09_EstadoSolicitudesHU17.sql (o la migración 008).

   Crea sus propios datos de ejemplo (usuarios *@hu17.prueba, salón "[PruebaHU17] Salón") en fechas de
   junio de 2030, ejecuta los criterios de aceptación con los stored procedures reales, muestra una
   tabla de resultados y BORRA todo lo que creó (también si algo falla). Si alguna prueba no pasa,
   termina con THROW. No usa una transacción externa porque los SP manejan sus propias transacciones.
   ========================================================================= */

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- Limpieza de una corrida anterior que se haya interrumpido.
DELETE s FROM Solicitudes s JOIN Salones sa ON sa.Id = s.SalonId WHERE sa.Nombre LIKE N'[[]PruebaHU17]%';
DELETE r FROM Reservaciones r JOIN Salones sa ON sa.Id = r.SalonId WHERE sa.Nombre LIKE N'[[]PruebaHU17]%';
DELETE p FROM Paquetes p JOIN Salones sa ON sa.Id = p.SalonId WHERE sa.Nombre LIKE N'[[]PruebaHU17]%';
DELETE FROM Salones WHERE Nombre LIKE N'[[]PruebaHU17]%';
DELETE FROM Usuarios WHERE Correo LIKE N'%@hu17.prueba';
GO

-- Todo lo demás va en un solo lote: las tablas temporales no sobreviven entre conexiones
-- (scripts/migraciones.ps1 abre una por lote).
CREATE TABLE #Resultados (N INT IDENTITY, Prueba NVARCHAR(150), Esperado NVARCHAR(120), Obtenido NVARCHAR(120), Paso BIT);
DECLARE @Error NVARCHAR(4000) = NULL;

BEGIN TRY
    -- ---------------------------------------------------------------------
    -- Datos de ejemplo
    -- ---------------------------------------------------------------------
    INSERT INTO Usuarios (NombreCompleto, Correo, PasswordHash, TipoCuenta, NombreSalon, FechaRegistro) VALUES
        (N'Admin HU17',  N'admin@hu17.prueba',  N'sin-login', 'administrador', N'[PruebaHU17] Salón', SYSUTCDATETIME()),
        (N'Otro Admin',  N'admin2@hu17.prueba', N'sin-login', 'administrador', NULL, SYSUTCDATETIME()),
        (N'Cliente Uno', N'c1@hu17.prueba',     N'sin-login', 'cliente', NULL, SYSUTCDATETIME()),
        (N'Cliente Dos', N'c2@hu17.prueba',     N'sin-login', 'cliente', NULL, SYSUTCDATETIME()),
        (N'Cliente Tres', N'c3@hu17.prueba',    N'sin-login', 'cliente', NULL, SYSUTCDATETIME()),
        (N'Cliente Cuatro', N'c4@hu17.prueba',  N'sin-login', 'cliente', NULL, SYSUTCDATETIME());

    DECLARE @Admin INT = (SELECT Id FROM Usuarios WHERE Correo = N'admin@hu17.prueba');
    DECLARE @Admin2 INT = (SELECT Id FROM Usuarios WHERE Correo = N'admin2@hu17.prueba');
    DECLARE @C1 INT = (SELECT Id FROM Usuarios WHERE Correo = N'c1@hu17.prueba');
    DECLARE @C2 INT = (SELECT Id FROM Usuarios WHERE Correo = N'c2@hu17.prueba');
    DECLARE @C3 INT = (SELECT Id FROM Usuarios WHERE Correo = N'c3@hu17.prueba');
    DECLARE @C4 INT = (SELECT Id FROM Usuarios WHERE Correo = N'c4@hu17.prueba');

    INSERT INTO Salones (AdminId, Nombre, Zona, Capacidad, Descripcion, Estado, FechaCreacion)
    VALUES (@Admin, N'[PruebaHU17] Salón', N'Tula de Allende, Hgo.', 200, NULL, 'publicado', SYSUTCDATETIME());
    DECLARE @Salon INT = SCOPE_IDENTITY();
    INSERT INTO Paquetes (SalonId, NombrePaquete, Precio) VALUES (@Salon, N'Paquete HU17', 9000);

    DECLARE @F1 DATE = '2030-06-01', @F2 DATE = '2030-06-08', @F3 DATE = '2030-06-15', @F4 DATE = '2030-06-22';
    DECLARE @Codigo INT, @Mensaje NVARCHAR(200);

    -- Inserta una solicitud pendiente de ejemplo y regresa su Id en @Nueva.
    DECLARE @Nueva INT;

    -- ---------------------------------------------------------------------
    -- CA1: dos pendientes de la misma fecha y salón; se confirma una → la otra queda rechazada
    -- ---------------------------------------------------------------------
    INSERT INTO Solicitudes (SalonId, ClienteId, FechaEvento, TipoEvento, NumeroInvitados, TelefonoContacto, Estado, FechaCreacion)
    VALUES (@Salon, @C1, @F1, N'Boda', 100, N'7710000001', 'pendiente', SYSUTCDATETIME());
    DECLARE @S1 INT = SCOPE_IDENTITY();
    INSERT INTO Solicitudes (SalonId, ClienteId, FechaEvento, TipoEvento, NumeroInvitados, TelefonoContacto, Estado, FechaCreacion)
    VALUES (@Salon, @C2, @F1, N'XV años', 80, N'7710000002', 'pendiente', SYSUTCDATETIME());
    DECLARE @S2 INT = SCOPE_IDENTITY();

    EXEC sp_Solicitud_CambiarEstado @Id = @S1, @AdminId = @Admin, @Estado = 'confirmada', @Codigo = @Codigo OUTPUT, @Mensaje = @Mensaje OUTPUT;

    INSERT INTO #Resultados (Prueba, Esperado, Obtenido, Paso)
    SELECT N'CA1 · confirmar una de dos pendientes (código)', N'0', CONCAT(@Codigo, N' ', @Mensaje), IIF(@Codigo = 0, 1, 0)
    UNION ALL
    SELECT N'CA1 · la confirmada', N'confirmada', Estado, IIF(Estado = 'confirmada', 1, 0) FROM Solicitudes WHERE Id = @S1
    UNION ALL
    SELECT N'CA1 · la otra cambia sola', N'rechazada', Estado, IIF(Estado = 'rechazada', 1, 0) FROM Solicitudes WHERE Id = @S2;

    -- ---------------------------------------------------------------------
    -- CA2: tras confirmar, la disponibilidad muestra la fecha ocupada de inmediato
    -- ---------------------------------------------------------------------
    CREATE TABLE #Ocupadas (FechaEvento DATE);
    INSERT INTO #Ocupadas EXEC sp_Salon_Disponibilidad @SalonId = @Salon, @Anio = 2030, @Mes = 6, @Codigo = @Codigo OUTPUT, @Mensaje = @Mensaje OUTPUT;

    INSERT INTO #Resultados (Prueba, Esperado, Obtenido, Paso)
    SELECT N'CA2 · fecha confirmada en la disponibilidad', CONVERT(NVARCHAR(10), @F1, 23),
           ISNULL((SELECT STRING_AGG(CONVERT(NVARCHAR(10), FechaEvento, 23), N', ') FROM #Ocupadas), N'(ninguna)'),
           IIF(EXISTS (SELECT 1 FROM #Ocupadas WHERE FechaEvento = @F1), 1, 0);

    -- Y crear otra solicitud para esa fecha ya no se puede.
    EXEC sp_Solicitud_Crear @ClienteId = @C3, @SalonId = @Salon, @FechaEvento = @F1, @TipoEvento = N'Bautizo',
         @NumeroInvitados = 50, @TelefonoContacto = N'7710000003', @Codigo = @Codigo OUTPUT, @Mensaje = @Mensaje OUTPUT;
    INSERT INTO #Resultados VALUES (N'CA2 · crear otra solicitud en la fecha ocupada', N'4002', CONCAT(@Codigo, N' ', @Mensaje), IIF(@Codigo = 4002, 1, 0));

    -- ---------------------------------------------------------------------
    -- CA3: rechazar una solicitud sin otras de esa fecha → la fecha sigue disponible
    -- ---------------------------------------------------------------------
    INSERT INTO Solicitudes (SalonId, ClienteId, FechaEvento, TipoEvento, NumeroInvitados, TelefonoContacto, Estado, FechaCreacion)
    VALUES (@Salon, @C3, @F2, N'Cumpleaños', 40, N'7710000003', 'pendiente', SYSUTCDATETIME());
    DECLARE @S3 INT = SCOPE_IDENTITY();

    EXEC sp_Solicitud_CambiarEstado @Id = @S3, @AdminId = @Admin, @Estado = 'rechazada', @Codigo = @Codigo OUTPUT, @Mensaje = @Mensaje OUTPUT;

    DELETE FROM #Ocupadas;
    INSERT INTO #Ocupadas EXEC sp_Salon_Disponibilidad @SalonId = @Salon, @Anio = 2030, @Mes = 6, @Codigo = @Codigo OUTPUT, @Mensaje = @Mensaje OUTPUT;

    INSERT INTO #Resultados (Prueba, Esperado, Obtenido, Paso)
    SELECT N'CA3 · la solicitud rechazada', N'rechazada', Estado, IIF(Estado = 'rechazada', 1, 0) FROM Solicitudes WHERE Id = @S3
    UNION ALL
    SELECT N'CA3 · la fecha NO aparece ocupada', N'sin ' + CONVERT(NVARCHAR(10), @F2, 23),
           ISNULL((SELECT STRING_AGG(CONVERT(NVARCHAR(10), FechaEvento, 23), N', ') FROM #Ocupadas), N'(ninguna)'),
           IIF(NOT EXISTS (SELECT 1 FROM #Ocupadas WHERE FechaEvento = @F2), 1, 0);

    EXEC sp_Solicitud_Crear @ClienteId = @C4, @SalonId = @Salon, @FechaEvento = @F2, @TipoEvento = N'Graduación',
         @NumeroInvitados = 60, @TelefonoContacto = N'7710000004', @Codigo = @Codigo OUTPUT, @Mensaje = @Mensaje OUTPUT;
    INSERT INTO #Resultados VALUES (N'CA3 · otro cliente aún puede pedir esa fecha', N'0', CONCAT(@Codigo, N' ', @Mensaje), IIF(@Codigo = 0, 1, 0));

    -- ---------------------------------------------------------------------
    -- CA4: varias solicitudes de la misma fecha; se confirma una → solo esa confirmada, resto rechazadas
    -- ---------------------------------------------------------------------
    INSERT INTO Solicitudes (SalonId, ClienteId, FechaEvento, TipoEvento, NumeroInvitados, TelefonoContacto, Estado, FechaCreacion)
    SELECT @Salon, c.Id, @F3, N'Evento', 50, N'7710000009', 'pendiente', SYSUTCDATETIME()
    FROM (VALUES (@C1), (@C2), (@C3), (@C4)) c (Id);
    DECLARE @S4 INT = (SELECT Id FROM Solicitudes WHERE SalonId = @Salon AND FechaEvento = @F3 AND ClienteId = @C3);

    EXEC sp_Solicitud_CambiarEstado @Id = @S4, @AdminId = @Admin, @Estado = 'confirmada', @Codigo = @Codigo OUTPUT, @Mensaje = @Mensaje OUTPUT;

    INSERT INTO #Resultados (Prueba, Esperado, Obtenido, Paso)
    SELECT N'CA4 · 4 solicitudes de la misma fecha, se confirma 1', N'1 confirmada (la elegida) y 3 rechazadas',
           CONCAT(SUM(IIF(Estado = 'confirmada', 1, 0)), N' confirmada(s), ', SUM(IIF(Estado = 'rechazada', 1, 0)),
                  N' rechazada(s), ', SUM(IIF(Estado = 'pendiente', 1, 0)), N' pendiente(s)'),
           IIF(SUM(IIF(Estado = 'confirmada' AND Id = @S4, 1, 0)) = 1 AND SUM(IIF(Estado = 'confirmada', 1, 0)) = 1
               AND SUM(IIF(Estado = 'rechazada', 1, 0)) = 3, 1, 0)
    FROM Solicitudes WHERE SalonId = @Salon AND FechaEvento = @F3;
    INSERT INTO #Resultados VALUES (N'CA4 · mensaje con las rechazadas', N'...Se rechazaron 3...', @Mensaje, IIF(@Mensaje LIKE N'%Se rechazaron 3 %', 1, 0));

    -- ---------------------------------------------------------------------
    -- Regla de negocio y casos de error
    -- ---------------------------------------------------------------------
    -- La BD no permite una segunda confirmada en el mismo salón y fecha, aunque se intente directo.
    DECLARE @Bloqueo NVARCHAR(120) = N'(se permitió)';
    BEGIN TRY
        UPDATE Solicitudes SET Estado = 'confirmada' WHERE Id = @S2;
    END TRY
    BEGIN CATCH
        SET @Bloqueo = CONCAT(N'error ', ERROR_NUMBER());
    END CATCH
    INSERT INTO #Resultados VALUES (N'Regla · 2a confirmada directa en BD (UX_Solicitudes_Confirmada)', N'error 2601', @Bloqueo, IIF(@Bloqueo = N'error 2601', 1, 0));

    EXEC sp_Solicitud_CambiarEstado @Id = @S2, @AdminId = @Admin, @Estado = 'confirmada', @Codigo = @Codigo OUTPUT, @Mensaje = @Mensaje OUTPUT;
    INSERT INTO #Resultados VALUES (N'Error · confirmar una solicitud ya atendida', N'4006', CONCAT(@Codigo, N' ', @Mensaje), IIF(@Codigo = 4006, 1, 0));

    INSERT INTO Solicitudes (SalonId, ClienteId, FechaEvento, TipoEvento, NumeroInvitados, TelefonoContacto, Estado, FechaCreacion)
    VALUES (@Salon, @C1, @F4, N'Boda', 100, N'7710000001', 'pendiente', SYSUTCDATETIME());
    SET @Nueva = SCOPE_IDENTITY();

    EXEC sp_Solicitud_CambiarEstado @Id = @Nueva, @AdminId = @Admin2, @Estado = 'confirmada', @Codigo = @Codigo OUTPUT, @Mensaje = @Mensaje OUTPUT;
    INSERT INTO #Resultados VALUES (N'Error · otro administrador no puede confirmar', N'2002', CONCAT(@Codigo, N' ', @Mensaje), IIF(@Codigo = 2002, 1, 0));

    EXEC sp_Solicitud_CambiarEstado @Id = @Nueva, @AdminId = @Admin, @Estado = 'cancelada', @Codigo = @Codigo OUTPUT, @Mensaje = @Mensaje OUTPUT;
    INSERT INTO #Resultados VALUES (N'Error · estado inválido', N'1001', CONCAT(@Codigo, N' ', @Mensaje), IIF(@Codigo = 1001, 1, 0));

    -- Una reservación confirmada (HU-11) también ocupa la fecha.
    INSERT INTO Reservaciones (SalonId, ClienteId, FechaEvento, Estado, FechaCreacion)
    VALUES (@Salon, @C2, @F4, 'confirmada', SYSUTCDATETIME());

    EXEC sp_Solicitud_CambiarEstado @Id = @Nueva, @AdminId = @Admin, @Estado = 'confirmada', @Codigo = @Codigo OUTPUT, @Mensaje = @Mensaje OUTPUT;
    INSERT INTO #Resultados (Prueba, Esperado, Obtenido, Paso)
    SELECT N'Consistencia · confirmar si hay reservación confirmada (HU-11)', N'4002 y sigue pendiente',
           CONCAT(@Codigo, N' · ', Estado), IIF(@Codigo = 4002 AND Estado = 'pendiente', 1, 0)
    FROM Solicitudes WHERE Id = @Nueva;

    EXEC sp_Solicitud_Crear @ClienteId = @C3, @SalonId = @Salon, @FechaEvento = @F4, @TipoEvento = N'Boda',
         @NumeroInvitados = 50, @TelefonoContacto = N'7710000003', @Codigo = @Codigo OUTPUT, @Mensaje = @Mensaje OUTPUT;
    INSERT INTO #Resultados VALUES (N'Consistencia · crear solicitud si hay reservación confirmada', N'4002', CONCAT(@Codigo, N' ', @Mensaje), IIF(@Codigo = 4002, 1, 0));
END TRY
BEGIN CATCH
    SET @Error = CONCAT(N'Error inesperado ', ERROR_NUMBER(), N' en la línea ', ERROR_LINE(), N': ', ERROR_MESSAGE());
END CATCH

-- Limpieza: no deja datos de prueba.
DELETE s FROM Solicitudes s JOIN Salones sa ON sa.Id = s.SalonId WHERE sa.Nombre LIKE N'[[]PruebaHU17]%';
DELETE r FROM Reservaciones r JOIN Salones sa ON sa.Id = r.SalonId WHERE sa.Nombre LIKE N'[[]PruebaHU17]%';
DELETE p FROM Paquetes p JOIN Salones sa ON sa.Id = p.SalonId WHERE sa.Nombre LIKE N'[[]PruebaHU17]%';
DELETE FROM Salones WHERE Nombre LIKE N'[[]PruebaHU17]%';
DELETE FROM Usuarios WHERE Correo LIKE N'%@hu17.prueba';

SELECT N, Prueba, Esperado, Obtenido, CASE Paso WHEN 1 THEN 'PASA' ELSE 'FALLA' END AS Resultado FROM #Resultados ORDER BY N;

DECLARE @Total INT = (SELECT COUNT(*) FROM #Resultados), @Pasan INT = (SELECT COUNT(*) FROM #Resultados WHERE Paso = 1);
PRINT CONCAT(N'HU-17: ', @Pasan, N' de ', @Total, N' pruebas pasan. Datos de prueba borrados.');

IF @Error IS NOT NULL THROW 50020, @Error, 1;
IF @Pasan < @Total OR @Total = 0 THROW 50021, 'HU-17: hay pruebas que no pasan.', 1;
GO
