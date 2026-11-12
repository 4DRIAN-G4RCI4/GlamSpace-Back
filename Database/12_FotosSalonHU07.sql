/* =========================================================================
   GlamSpaces — Fotos de los salones (HU-07 (fotos): subida real de imágenes, Sprint 7)
   Correr DESPUÉS de 11_SolicitudesAdminHU18.sql. Idempotente (CREATE OR ALTER).
   La migración M010_FotosSalonHU07 ejecuta este mismo script.

   La tabla FotosSalon ya existe (01_Tablas.sql) y no cambia: aquí solo van los SP para
   agregar y eliminar fotos. El archivo vive en Azure Blob Storage; la tabla guarda su URL pública.
   ========================================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- Registra la URL de una foto ya subida al almacenamiento. Solo el dueño, máximo 8 fotos por salón.
-- El salón se bloquea (UPDLOCK) mientras se cuenta: dos subidas al mismo tiempo no pasan de 8.
-- Con error no inserta nada y el API borra el archivo que ya había subido (sin huérfanos).
CREATE OR ALTER PROCEDURE sp_FotoSalon_Agregar
    @SalonId INT,
    @AdminId INT,
    @Url     NVARCHAR(500),
    @Codigo  INT OUTPUT,
    @Mensaje NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @DuenoId INT, @Id INT;

    BEGIN TRANSACTION;

    SELECT @DuenoId = AdminId FROM Salones WITH (UPDLOCK, HOLDLOCK) WHERE Id = @SalonId;

    IF @DuenoId IS NULL
    BEGIN ROLLBACK; SELECT @Codigo = 2001, @Mensaje = N'El salón no existe.'; RETURN; END

    IF @DuenoId <> ISNULL(@AdminId, 0)
    BEGIN ROLLBACK; SELECT @Codigo = 2002, @Mensaje = N'Solo el administrador dueño del salón puede modificarlo.'; RETURN; END

    IF (SELECT COUNT(*) FROM FotosSalon WHERE SalonId = @SalonId) >= 8
    BEGIN ROLLBACK; SELECT @Codigo = 2008, @Mensaje = N'El salón ya tiene 8 fotos. Elimina una para subir otra.'; RETURN; END

    INSERT INTO FotosSalon (SalonId, Url) VALUES (@SalonId, @Url);
    SET @Id = SCOPE_IDENTITY();

    COMMIT;

    SELECT @Codigo = 0, @Mensaje = N'Foto subida correctamente.';
    SELECT Id, SalonId, Url FROM FotosSalon WHERE Id = @Id;
END
GO

-- Elimina una foto del salón y regresa su URL para que el API borre el archivo.
-- @SoloValidar = 1: hace las mismas validaciones y regresa la foto SIN borrarla. El API lo usa
-- para borrar primero el archivo: si el almacenamiento falla, el registro sigue y se puede reintentar.
CREATE OR ALTER PROCEDURE sp_FotoSalon_Eliminar
    @SalonId     INT,
    @FotoId      INT,
    @AdminId     INT,
    @SoloValidar BIT = 0,
    @Codigo      INT OUTPUT,
    @Mensaje     NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @DuenoId INT = (SELECT AdminId FROM Salones WHERE Id = @SalonId);

    IF @DuenoId IS NULL
    BEGIN SELECT @Codigo = 2001, @Mensaje = N'El salón no existe.'; RETURN; END

    IF @DuenoId <> ISNULL(@AdminId, 0)
    BEGIN SELECT @Codigo = 2002, @Mensaje = N'Solo el administrador dueño del salón puede modificarlo.'; RETURN; END

    IF NOT EXISTS (SELECT 1 FROM FotosSalon WHERE Id = @FotoId AND SalonId = @SalonId)
    BEGIN SELECT @Codigo = 2009, @Mensaje = N'La foto no existe o no es de este salón.'; RETURN; END

    SELECT Id, SalonId, Url FROM FotosSalon WHERE Id = @FotoId;

    IF ISNULL(@SoloValidar, 0) = 0
    BEGIN
        DELETE FROM FotosSalon WHERE Id = @FotoId;
        SELECT @Codigo = 0, @Mensaje = N'Foto eliminada correctamente.';
    END
    ELSE
        SELECT @Codigo = 0, @Mensaje = N'Foto encontrada.';
END
GO
