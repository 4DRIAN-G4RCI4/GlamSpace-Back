/* =========================================================================
   GlamSpaces — "Mis salones" del administrador (mejoras de HU-07 / HU-08, Sprint 7)
   Correr DESPUÉS de 13_FotosPreviewBuscar.sql. Idempotente (CREATE OR ALTER). No cambia tablas.

   1) sp_Salon_Listar: cada renglón trae además FotoPrincipal (primera foto por Id, o NULL) y
      TotalFotos. Mismas columnas de antes + 2 nuevas al final, así que la API vieja sigue
      funcionando. Subconsultas por renglón de la página (sin N+1); usa IX_FotosSalon_SalonId.
   2) sp_Salon_Eliminar (nuevo): borra el salón y todo lo que depende de él.
      - 2001 no existe · 2002 no es el dueño · 2010 tiene solicitudes pendientes o confirmadas
        (o reservaciones confirmadas) · 5000 falló el borrado (se hace ROLLBACK de todo).
      - Orden según las llaves foráneas (todas NO ACTION): Solicitudes y Reservaciones (apuntan
        también a Paquetes), FotosSalon, Paquetes, BusquedasFavoritas y al final Salones.
      - Los archivos de las fotos (carpeta {salonId}/ del contenedor) los borra la API DESPUÉS
        del COMMIT, como mejor esfuerzo.
   ========================================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- Paginado: primer result set = datos del paginado, segundo = registros de la página.
-- @AdminId NULL lista todos. @Pagina < 1 se toma como 1; @TamanoPagina va de 1 a 100.
-- FotoPrincipal = la primera foto del salón (por Id, igual que la búsqueda); NULL si no tiene.
CREATE OR ALTER PROCEDURE sp_Salon_Listar
    @AdminId      INT = NULL,
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

    DECLARE @Total INT = (SELECT COUNT(*) FROM Salones WHERE @AdminId IS NULL OR AdminId = @AdminId);

    SELECT @Codigo = 0, @Mensaje = N'Consulta exitosa.';

    SELECT @Total AS TotalRegistros,
           (@Total + @TamanoPagina - 1) / @TamanoPagina AS TotalPaginas,
           @Pagina AS Pagina,
           @TamanoPagina AS TamanoPagina;

    SELECT s.Id, s.Nombre, s.Zona, s.Capacidad, s.Estado,
           (SELECT COUNT(*) FROM Paquetes p WHERE p.SalonId = s.Id) AS TotalPaquetes,
           (SELECT TOP 1 f.Url FROM FotosSalon f WHERE f.SalonId = s.Id ORDER BY f.Id) AS FotoPrincipal,
           (SELECT COUNT(*) FROM FotosSalon f WHERE f.SalonId = s.Id) AS TotalFotos
    FROM Salones s
    WHERE @AdminId IS NULL OR s.AdminId = @AdminId
    ORDER BY s.Id DESC
    OFFSET (@Pagina - 1) * @TamanoPagina ROWS
    FETCH NEXT @TamanoPagina ROWS ONLY;
END
GO

-- Elimina el salón con sus fotos (registros), paquetes, favoritas, solicitudes rechazadas y
-- reservaciones no confirmadas. Solo el dueño. No se elimina si hay algo por atender o ya
-- confirmado: primero hay que resolverlo.
CREATE OR ALTER PROCEDURE sp_Salon_Eliminar
    @Id      INT,
    @AdminId INT,
    @Codigo  INT OUTPUT,
    @Mensaje NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @DuenoId INT = (SELECT AdminId FROM Salones WHERE Id = @Id);

    IF @DuenoId IS NULL
    BEGIN SELECT @Codigo = 2001, @Mensaje = N'El salón no existe.'; RETURN; END

    IF @DuenoId <> @AdminId
    BEGIN SELECT @Codigo = 2002, @Mensaje = N'No eres el dueño de este salón.'; RETURN; END

    BEGIN TRY
        BEGIN TRAN;

        -- UPDLOCK + HOLDLOCK: si al mismo tiempo llega una solicitud o se confirma una, una
        -- operación espera a la otra (no se borra un salón con algo recién confirmado).
        IF EXISTS (SELECT 1 FROM Solicitudes WITH (UPDLOCK, HOLDLOCK)
                   WHERE SalonId = @Id AND Estado IN ('pendiente', 'confirmada'))
           OR EXISTS (SELECT 1 FROM Reservaciones WITH (UPDLOCK, HOLDLOCK)
                      WHERE SalonId = @Id AND Estado = 'confirmada')
        BEGIN
            ROLLBACK;
            SELECT @Codigo = 2010, @Mensaje = N'Tiene solicitudes pendientes o confirmadas; resuélvelas antes de eliminar el salón.';
            RETURN;
        END

        DELETE FROM Solicitudes        WHERE SalonId = @Id; -- solo quedan rechazadas
        DELETE FROM Reservaciones      WHERE SalonId = @Id; -- solo quedan pendientes o canceladas
        DELETE FROM FotosSalon         WHERE SalonId = @Id;
        DELETE FROM Paquetes           WHERE SalonId = @Id;
        DELETE FROM BusquedasFavoritas WHERE SalonId = @Id;
        DELETE FROM Salones            WHERE Id = @Id;

        COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        SELECT @Codigo = 5000, @Mensaje = N'No se pudo eliminar el salón. Intenta de nuevo más tarde.';
        RETURN;
    END CATCH

    SELECT @Codigo = 0, @Mensaje = N'Salón eliminado correctamente.';
END
GO
