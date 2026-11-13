/* =========================================================================
   GlamSpaces — Foto del salón en las solicitudes y convertir cliente en administrador (Sprint 7)
   Correr DESPUÉS de 14_MisSalonesAdmin.sql (15 a 17 son la carga de demostración y no se necesitan).
   Idempotente (CREATE OR ALTER). No cambia tablas. La migración M013 ejecuta este mismo script.

   1) sp_Solicitud_ListarPorCliente y sp_Solicitud_ListarPorSalon: cada renglón trae además
      FotoPrincipal (primera foto del salón por Id, o NULL), con OUTER APPLY TOP 1 sobre FotosSalon
      (usa IX_FotosSalon_SalonId). Mismas columnas de antes + 1 al final; paginado igual.
   2) sp_Usuario_ObtenerCredenciales (nuevo): el usuario por Id CON su PasswordHash, para que la API
      verifique la contraseña con el mismo PBKDF2 del login. 1005 si no existe.
   3) sp_Usuario_ConvertirAAdministrador (nuevo): cambia TipoCuenta de 'cliente' a 'administrador'.
      No toca correo, contraseña ni solicitudes. 1005 no existe · 1006 ya es administrador.
   ========================================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- Solicitudes del cliente paginadas, de la más reciente a la más antigua.
-- Sin solicitudes NO es error: éxito con lista vacía.
-- Siempre regresa los 2 result sets (paginado y página), también con error, igual que
-- sp_Salon_Listar; con error la página va vacía y el paginado en 0.
-- FotoPrincipal = la primera foto del salón (por Id); NULL si no tiene.
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
           s.Estado, s.FechaCreacion, f.Url AS FotoPrincipal
    FROM Solicitudes s
    JOIN Salones sa ON sa.Id = s.SalonId
    LEFT JOIN Paquetes p ON p.Id = s.PaqueteId
    OUTER APPLY (SELECT TOP 1 fs.Url FROM FotosSalon fs WHERE fs.SalonId = s.SalonId ORDER BY fs.Id) f
    WHERE s.ClienteId = @ClienteId AND @Codigo = 0
    ORDER BY s.FechaCreacion DESC, s.Id DESC
    OFFSET (@Pagina - 1) * @TamanoPagina ROWS
    FETCH NEXT @TamanoPagina ROWS ONLY;
END
GO

-- Solicitudes de UN salón, solo para el administrador dueño, ordenadas por fecha del evento
-- (las más próximas primero). @Estado: 'pendiente' (por defecto), 'confirmada', 'rechazada' o 'todas'.
-- Incluye el nombre del cliente y el precio del paquete para el panel del administrador (HU-19).
-- Siempre regresa los 2 result sets (paginado y página), como sp_Solicitud_ListarPorCliente;
-- con error, la página va vacía y el paginado en 0. FotoPrincipal: primera foto del salón o NULL.
CREATE OR ALTER PROCEDURE sp_Solicitud_ListarPorSalon
    @SalonId      INT,
    @AdminId      INT,
    @Estado       NVARCHAR(20) = NULL,
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
    SET @Estado = ISNULL(NULLIF(LOWER(TRIM(@Estado)), ''), 'pendiente');

    DECLARE @Total INT = 0;
    SET @Codigo = 0;

    IF @Estado NOT IN ('pendiente', 'confirmada', 'rechazada', 'todas')
        SELECT @Codigo = 1001, @Mensaje = N'El estado debe ser pendiente, confirmada, rechazada o todas.';
    ELSE IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Id = @AdminId AND TipoCuenta = 'administrador')
        SELECT @Codigo = 1004, @Mensaje = N'El usuario no existe o no es administrador.';
    ELSE IF NOT EXISTS (SELECT 1 FROM Salones WHERE Id = @SalonId)
        SELECT @Codigo = 2001, @Mensaje = N'El salón no existe.';
    ELSE IF NOT EXISTS (SELECT 1 FROM Salones WHERE Id = @SalonId AND AdminId = @AdminId)
        SELECT @Codigo = 2002, @Mensaje = N'No eres el dueño de este salón.';
    ELSE
    BEGIN
        SET @Total = (SELECT COUNT(*) FROM Solicitudes
                      WHERE SalonId = @SalonId AND (@Estado = 'todas' OR Estado = @Estado));
        SELECT @Mensaje = CASE WHEN @Total = 0 THEN N'El salón no tiene solicitudes con ese estado.'
                               ELSE N'Consulta exitosa.' END;
    END

    SELECT @Total AS TotalRegistros,
           (@Total + @TamanoPagina - 1) / @TamanoPagina AS TotalPaginas,
           @Pagina AS Pagina,
           @TamanoPagina AS TamanoPagina;

    SELECT s.Id, s.SalonId, sa.Nombre AS NombreSalon, s.ClienteId, u.NombreCompleto AS NombreCliente,
           s.PaqueteId, p.NombrePaquete, p.Precio AS PrecioPaquete,
           s.FechaEvento, s.TipoEvento, s.NumeroInvitados, s.TelefonoContacto, s.Comentarios,
           s.Estado, s.FechaCreacion, f.Url AS FotoPrincipal
    FROM Solicitudes s
    JOIN Salones sa ON sa.Id = s.SalonId
    JOIN Usuarios u ON u.Id = s.ClienteId
    LEFT JOIN Paquetes p ON p.Id = s.PaqueteId
    OUTER APPLY (SELECT TOP 1 fs.Url FROM FotosSalon fs WHERE fs.SalonId = s.SalonId ORDER BY fs.Id) f
    WHERE s.SalonId = @SalonId AND @Codigo = 0
      AND (@Estado = 'todas' OR s.Estado = @Estado)
    ORDER BY s.FechaEvento, s.Id
    OFFSET (@Pagina - 1) * @TamanoPagina ROWS
    FETCH NEXT @TamanoPagina ROWS ONLY;
END
GO

-- El usuario CON su PasswordHash: solo lo usa la API para verificar la contraseña (nunca sale en
-- una respuesta). 1005 si no existe.
CREATE OR ALTER PROCEDURE sp_Usuario_ObtenerCredenciales
    @Id      INT,
    @Codigo  INT OUTPUT,
    @Mensaje NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Id = @Id)
    BEGIN SELECT @Codigo = 1005, @Mensaje = N'El usuario no existe.'; RETURN; END

    SELECT @Codigo = 0, @Mensaje = N'Consulta exitosa.';

    SELECT Id, NombreCompleto, Correo, PasswordHash, TipoCuenta, NombreSalon, FechaRegistro
    FROM Usuarios WHERE Id = @Id;
END
GO

-- Convierte una cuenta de cliente en administrador. La API verifica la contraseña ANTES.
-- El UPDATE exige TipoCuenta = 'cliente': si dos peticiones llegan a la vez, solo una convierte.
-- Correo, contraseña, solicitudes y reservaciones no cambian. NombreSalon queda como estaba (NULL).
CREATE OR ALTER PROCEDURE sp_Usuario_ConvertirAAdministrador
    @Id      INT,
    @Codigo  INT OUTPUT,
    @Mensaje NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE Usuarios SET TipoCuenta = 'administrador' WHERE Id = @Id AND TipoCuenta = 'cliente';

    IF @@ROWCOUNT = 0
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Id = @Id)
            SELECT @Codigo = 1005, @Mensaje = N'El usuario no existe.';
        ELSE
            SELECT @Codigo = 1006, @Mensaje = N'La cuenta ya es de administrador.';
        RETURN;
    END

    SELECT @Codigo = 0, @Mensaje = N'Tu cuenta ahora es de administrador.';

    SELECT Id, NombreCompleto, Correo, TipoCuenta, NombreSalon, FechaRegistro
    FROM Usuarios WHERE Id = @Id;
END
GO
