/* =========================================================================
   GlamSpaces — Stored procedures de solicitudes (Sprint 5 — HU-15)
   Correr DESPUÉS de 06_SolicitudesHU14.sql y 07_TelefonoSolicitudesHU16.sql
   (necesita la tabla Solicitudes con TelefonoContacto). Idempotente (CREATE OR ALTER).
   La migración M007_ProcedimientosSolicitudes crea estos mismos 3 SP.

   Misma convención que 02_StoredProcedures.sql: @Codigo INT OUTPUT y @Mensaje NVARCHAR(200) OUTPUT,
   @Codigo = 0 es éxito y los demás números son los de GlamSpaces.Domain/Comun/CodigosError.cs.
   ========================================================================= */

-- Requeridos por el índice filtrado UX_Solicitudes_Cliente_Salon_Fecha_Pendiente:
-- con OFF, el INSERT de sp_Solicitud_Crear fallaría.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- Crea una solicitud del cliente. Siempre nace 'pendiente' con FechaCreacion en UTC.
-- Valida en orden: cliente, salón publicado, paquete del salón, datos, capacidad y fecha disponible.
-- La fecha deja de estar disponible cuando el salón ya tiene una solicitud 'confirmada' ese día.
-- Si hay error hace RETURN sin result set; si no, regresa la solicitud creada.
CREATE OR ALTER PROCEDURE sp_Solicitud_Crear
    @ClienteId        INT,
    @SalonId          INT,
    @PaqueteId        INT = NULL,
    @FechaEvento      DATE,
    @TipoEvento       NVARCHAR(100),
    @NumeroInvitados  INT,
    @TelefonoContacto NVARCHAR(20),
    @Comentarios      NVARCHAR(500) = NULL,
    @Codigo           INT OUTPUT,
    @Mensaje          NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @TipoCuenta NVARCHAR(20) = (SELECT TipoCuenta FROM Usuarios WHERE Id = @ClienteId);

    IF @TipoCuenta IS NULL
    BEGIN SELECT @Codigo = 1005, @Mensaje = N'El usuario no existe.'; RETURN; END

    IF @TipoCuenta <> 'cliente'
    BEGIN SELECT @Codigo = 4003, @Mensaje = N'Solo los clientes pueden enviar solicitudes.'; RETURN; END

    DECLARE @Capacidad INT = (SELECT Capacidad FROM Salones WHERE Id = @SalonId AND Estado = 'publicado');

    IF @Capacidad IS NULL
    BEGIN SELECT @Codigo = 2001, @Mensaje = N'El salón no existe o no está publicado.'; RETURN; END

    IF @PaqueteId IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM Paquetes WHERE Id = @PaqueteId AND SalonId = @SalonId)
    BEGIN SELECT @Codigo = 3001, @Mensaje = N'El paquete no existe o no es de ese salón.'; RETURN; END

    SET @TipoEvento = TRIM(@TipoEvento);
    SET @TelefonoContacto = TRIM(@TelefonoContacto);
    SET @Comentarios = NULLIF(TRIM(@Comentarios), '');

    -- "Hoy" en la hora del centro de México: la BD corre en UTC y de noche ya sería mañana.
    DECLARE @Hoy DATE = CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'Central Standard Time (Mexico)' AS DATE);

    IF ISNULL(@TipoEvento, '') = ''
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'El tipo de evento es obligatorio.'; RETURN; END

    IF ISNULL(@TelefonoContacto, '') = ''
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'El teléfono de contacto es obligatorio.'; RETURN; END

    IF @FechaEvento IS NULL OR @FechaEvento < @Hoy
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'La fecha del evento no puede ser una fecha pasada.'; RETURN; END

    IF ISNULL(@NumeroInvitados, 0) <= 0
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'El número de invitados debe ser mayor a 0.'; RETURN; END

    IF @NumeroInvitados > @Capacidad
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'Los invitados exceden la capacidad del salón.'; RETURN; END

    DECLARE @Id INT;

    BEGIN TRY
        BEGIN TRAN;

        -- UPDLOCK + HOLDLOCK bloquean el salón y la fecha hasta el COMMIT: si al mismo tiempo
        -- se confirma una solicitud de ese día, una operación espera a la otra.
        IF EXISTS (SELECT 1 FROM Solicitudes WITH (UPDLOCK, HOLDLOCK)
                   WHERE SalonId = @SalonId AND FechaEvento = @FechaEvento AND Estado = 'confirmada')
        BEGIN
            ROLLBACK;
            SELECT @Codigo = 4002, @Mensaje = N'La fecha ya no está disponible.';
            RETURN;
        END

        -- Estado y FechaCreacion explícitos para no depender de los DEFAULT.
        INSERT INTO Solicitudes (SalonId, ClienteId, PaqueteId, FechaEvento, TipoEvento, NumeroInvitados,
                                 Comentarios, Estado, FechaCreacion, TelefonoContacto)
        VALUES (@SalonId, @ClienteId, @PaqueteId, @FechaEvento, @TipoEvento, @NumeroInvitados,
                @Comentarios, 'pendiente', SYSUTCDATETIME(), @TelefonoContacto);

        SET @Id = SCOPE_IDENTITY();

        COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;

        -- UX_Solicitudes_Cliente_Salon_Fecha_Pendiente rechaza una segunda solicitud pendiente
        -- del mismo cliente, salón y fecha (también si llegan dos al mismo tiempo).
        IF ERROR_NUMBER() IN (2601, 2627)
        BEGIN SELECT @Codigo = 4004, @Mensaje = N'Ya tienes una solicitud pendiente para ese salón y fecha.'; RETURN; END;

        THROW;
    END CATCH

    SELECT @Codigo = 0, @Mensaje = N'Solicitud enviada correctamente.';

    SELECT s.Id, s.SalonId, sa.Nombre AS NombreSalon, s.ClienteId, s.PaqueteId, p.NombrePaquete,
           s.FechaEvento, s.TipoEvento, s.NumeroInvitados, s.TelefonoContacto, s.Comentarios,
           s.Estado, s.FechaCreacion
    FROM Solicitudes s
    JOIN Salones sa ON sa.Id = s.SalonId
    LEFT JOIN Paquetes p ON p.Id = s.PaqueteId
    WHERE s.Id = @Id;
END
GO

-- Datos y estado actual de una solicitud, con el nombre del salón y del paquete.
-- Solo el cliente que la creó puede consultarla.
CREATE OR ALTER PROCEDURE sp_Solicitud_Obtener
    @Id        INT,
    @ClienteId INT,
    @Codigo    INT OUTPUT,
    @Mensaje   NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TipoCuenta NVARCHAR(20) = (SELECT TipoCuenta FROM Usuarios WHERE Id = @ClienteId);

    IF @TipoCuenta IS NULL
    BEGIN SELECT @Codigo = 1005, @Mensaje = N'El usuario no existe.'; RETURN; END

    IF @TipoCuenta <> 'cliente'
    BEGIN SELECT @Codigo = 4003, @Mensaje = N'Solo los clientes pueden consultar solicitudes.'; RETURN; END

    DECLARE @DuenoId INT = (SELECT ClienteId FROM Solicitudes WHERE Id = @Id);

    IF @DuenoId IS NULL
    BEGIN SELECT @Codigo = 4001, @Mensaje = N'La solicitud no existe.'; RETURN; END

    IF @DuenoId <> @ClienteId
    BEGIN SELECT @Codigo = 4005, @Mensaje = N'No tienes acceso a esta solicitud.'; RETURN; END

    SELECT @Codigo = 0, @Mensaje = N'Consulta exitosa.';

    SELECT s.Id, s.SalonId, sa.Nombre AS NombreSalon, s.ClienteId, s.PaqueteId, p.NombrePaquete,
           s.FechaEvento, s.TipoEvento, s.NumeroInvitados, s.TelefonoContacto, s.Comentarios,
           s.Estado, s.FechaCreacion
    FROM Solicitudes s
    JOIN Salones sa ON sa.Id = s.SalonId
    LEFT JOIN Paquetes p ON p.Id = s.PaqueteId
    WHERE s.Id = @Id;
END
GO

-- Solicitudes del cliente paginadas, de la más reciente a la más antigua.
-- Sin solicitudes NO es error: éxito con lista vacía.
-- Siempre regresa los 2 result sets (paginado y página), también con error, igual que
-- sp_Salon_Listar; con error la página va vacía y el paginado en 0.
CREATE OR ALTER PROCEDURE sp_Solicitud_ListarPorCliente
    @ClienteId    INT,
    @Pagina       INT = 1,
    @TamanoPagina INT = 10,
    @Codigo       INT OUTPUT,
    @Mensaje      NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @Pagina = CASE WHEN ISNULL(@Pagina, 0) < 1 THEN 1 ELSE @Pagina END;
    SET @TamanoPagina = CASE
        WHEN ISNULL(@TamanoPagina, 0) < 1 THEN 10
        WHEN @TamanoPagina > 100 THEN 100
        ELSE @TamanoPagina END;

    DECLARE @TipoCuenta NVARCHAR(20) = (SELECT TipoCuenta FROM Usuarios WHERE Id = @ClienteId);
    DECLARE @Total INT = 0;

    IF @TipoCuenta IS NULL
        SELECT @Codigo = 1005, @Mensaje = N'El usuario no existe.';
    ELSE IF @TipoCuenta <> 'cliente'
        SELECT @Codigo = 4003, @Mensaje = N'Solo los clientes pueden consultar solicitudes.';
    ELSE
    BEGIN
        SET @Total = (SELECT COUNT(*) FROM Solicitudes WHERE ClienteId = @ClienteId);
        SELECT @Codigo = 0,
               @Mensaje = CASE WHEN @Total = 0 THEN N'No tienes solicitudes registradas.'
                               ELSE N'Consulta exitosa.' END;
    END

    SELECT @Total AS TotalRegistros,
           (@Total + @TamanoPagina - 1) / @TamanoPagina AS TotalPaginas,
           @Pagina AS Pagina,
           @TamanoPagina AS TamanoPagina;

    SELECT s.Id, s.SalonId, sa.Nombre AS NombreSalon, s.ClienteId, s.PaqueteId, p.NombrePaquete,
           s.FechaEvento, s.TipoEvento, s.NumeroInvitados, s.TelefonoContacto, s.Comentarios,
           s.Estado, s.FechaCreacion
    FROM Solicitudes s
    JOIN Salones sa ON sa.Id = s.SalonId
    LEFT JOIN Paquetes p ON p.Id = s.PaqueteId
    WHERE s.ClienteId = @ClienteId AND @Codigo = 0
    ORDER BY s.FechaCreacion DESC, s.Id DESC
    OFFSET (@Pagina - 1) * @TamanoPagina ROWS
    FETCH NEXT @TamanoPagina ROWS ONLY;
END
GO
