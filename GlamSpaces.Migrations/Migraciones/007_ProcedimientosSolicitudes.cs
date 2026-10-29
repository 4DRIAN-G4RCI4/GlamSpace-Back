namespace GlamSpaces.Migrations.Migraciones;

// 007 · HU-15 (Sprint 5): stored procedures para crear y consultar solicitudes de reservación.
// Usa solo la tabla Solicitudes de M005 + M006; no cambia el esquema.
// Up() es idempotente (CREATE OR ALTER) y el contenido es el mismo de Database/08_SolicitudesHU15.sql.
// Cada SP va en su propio Execute.Sql porque CREATE PROCEDURE debe ser lo primero del lote (sin GO).
[Migration(202610291200, "HU-15: stored procedures de solicitudes")]
public class M007_ProcedimientosSolicitudes : Migration
{
    public override void Up()
    {
        // Crea la solicitud "pendiente". Valida cliente, salón publicado, paquete, datos, capacidad y
        // que la fecha no tenga una solicitud confirmada (con UPDLOCK, HOLDLOCK). Un duplicado pendiente
        // (índice UX_Solicitudes_Cliente_Salon_Fecha_Pendiente) se traduce a 4004.
        Execute.Sql(@"
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

    -- ""Hoy"" en la hora del centro de México: la BD corre en UTC y de noche ya sería mañana.
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
END");

        // Datos y estado de una solicitud; solo si es del cliente que la pide.
        Execute.Sql(@"
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
END");

        // Solicitudes del cliente, paginadas como sp_Salon_Buscar (FechaCreacion DESC).
        Execute.Sql(@"
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
END");
    }

    public override void Down()
    {
        Execute.Sql("DROP PROCEDURE IF EXISTS dbo.sp_Solicitud_Crear;");
        Execute.Sql("DROP PROCEDURE IF EXISTS dbo.sp_Solicitud_Obtener;");
        Execute.Sql("DROP PROCEDURE IF EXISTS dbo.sp_Solicitud_ListarPorCliente;");
    }
}
