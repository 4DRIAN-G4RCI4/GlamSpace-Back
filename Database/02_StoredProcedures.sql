/* =========================================================================
   GlamSpaces — Stored procedures (glamspaces-db, Azure SQL)
   Correr DESPUÉS de 01_Tablas.sql. Idempotente (CREATE OR ALTER).

   Convención de todos los SP:
     - Reciben @Codigo INT OUTPUT y @Mensaje NVARCHAR(200) OUTPUT.
     - @Codigo = 0 es éxito; los demás números son los de
       GlamSpaces.Domain/Comun/CodigosError.cs.
     - Si hay error, hacen RETURN sin regresar result sets.
   ========================================================================= */

-- Los SP guardan estas opciones al crearse. Deben estar en ON porque Salones tiene
-- índices filtrados (03_Indices.sql); con OFF, los INSERT/UPDATE de los SP fallarían.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- =========================================================================
-- USUARIOS
-- =========================================================================

-- La API manda la contraseña ya hasheada (PBKDF2); el SP nunca ve el texto plano.
CREATE OR ALTER PROCEDURE sp_Usuario_Registrar
    @NombreCompleto NVARCHAR(150),
    @Correo         NVARCHAR(150),
    @PasswordHash   NVARCHAR(MAX),
    @TipoCuenta     NVARCHAR(20),
    @NombreSalon    NVARCHAR(150) = NULL,
    @Codigo         INT OUTPUT,
    @Mensaje        NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @NombreCompleto = TRIM(@NombreCompleto);
    SET @Correo = LOWER(TRIM(@Correo));
    SET @TipoCuenta = LOWER(TRIM(@TipoCuenta));
    SET @NombreSalon = NULLIF(TRIM(@NombreSalon), '');

    IF ISNULL(@NombreCompleto, '') = ''
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'Escribe tu nombre completo.'; RETURN; END

    IF ISNULL(@Correo, '') NOT LIKE '_%@_%._%'
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'El formato del correo no es válido.'; RETURN; END

    IF ISNULL(@TipoCuenta, '') NOT IN ('cliente', 'administrador')
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'El tipo de cuenta debe ser "cliente" o "administrador".'; RETURN; END

    IF @TipoCuenta = 'administrador' AND @NombreSalon IS NULL
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'Escribe el nombre de tu salón.'; RETURN; END

    IF EXISTS (SELECT 1 FROM Usuarios WHERE Correo = @Correo)
    BEGIN SELECT @Codigo = 1002, @Mensaje = N'Ya existe una cuenta con este correo.'; RETURN; END

    INSERT INTO Usuarios (NombreCompleto, Correo, PasswordHash, TipoCuenta, NombreSalon, FechaRegistro)
    VALUES (@NombreCompleto, @Correo, @PasswordHash, @TipoCuenta,
            CASE WHEN @TipoCuenta = 'administrador' THEN @NombreSalon END, GETDATE());

    SELECT @Codigo = 0, @Mensaje = N'Cuenta creada correctamente.';

    SELECT Id, NombreCompleto, Correo, TipoCuenta, NombreSalon, FechaRegistro
    FROM Usuarios WHERE Id = SCOPE_IDENTITY();
END
GO

-- Para el login: regresa el hash y la API lo verifica (el SP no puede verificar PBKDF2).
CREATE OR ALTER PROCEDURE sp_Usuario_ObtenerPorCorreo
    @Correo  NVARCHAR(150),
    @Codigo  INT OUTPUT,
    @Mensaje NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT @Codigo = 0, @Mensaje = N'Consulta exitosa.';

    SELECT Id, NombreCompleto, Correo, PasswordHash, TipoCuenta, NombreSalon, FechaRegistro
    FROM Usuarios WHERE Correo = LOWER(TRIM(@Correo));
END
GO

CREATE OR ALTER PROCEDURE sp_Usuario_Obtener
    @Id      INT,
    @Codigo  INT OUTPUT,
    @Mensaje NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Id = @Id)
    BEGIN SELECT @Codigo = 1005, @Mensaje = N'El usuario no existe.'; RETURN; END

    SELECT @Codigo = 0, @Mensaje = N'Consulta exitosa.';

    SELECT Id, NombreCompleto, Correo, TipoCuenta, NombreSalon, FechaRegistro
    FROM Usuarios WHERE Id = @Id;
END
GO

-- =========================================================================
-- SALONES
-- =========================================================================

CREATE OR ALTER PROCEDURE sp_Salon_Crear
    @AdminId     INT,
    @Nombre      NVARCHAR(150),
    @Zona        NVARCHAR(200),
    @Capacidad   INT,
    @Descripcion NVARCHAR(1000) = NULL,
    @Codigo      INT OUTPUT,
    @Mensaje     NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @Nombre = TRIM(@Nombre);
    SET @Zona = TRIM(@Zona);
    SET @Descripcion = NULLIF(TRIM(@Descripcion), '');

    IF ISNULL(@Nombre, '') = ''
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'El nombre del salón es obligatorio.'; RETURN; END

    IF ISNULL(@Zona, '') = ''
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'La zona es obligatoria.'; RETURN; END

    IF ISNULL(@Capacidad, 0) <= 0
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'La capacidad debe ser mayor a 0.'; RETURN; END

    IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Id = @AdminId AND TipoCuenta = 'administrador')
    BEGIN SELECT @Codigo = 1004, @Mensaje = N'El usuario no existe o no es administrador.'; RETURN; END

    -- Siempre nace "no_publicado": todavía no tiene paquetes.
    INSERT INTO Salones (AdminId, Nombre, Zona, Capacidad, Descripcion, Estado, FechaCreacion)
    VALUES (@AdminId, @Nombre, @Zona, @Capacidad, @Descripcion, 'no_publicado', GETDATE());

    SELECT @Codigo = 0, @Mensaje = N'Salón creado correctamente.';

    SELECT Id, AdminId, Nombre, Zona, Capacidad, Descripcion, Estado, FechaCreacion
    FROM Salones WHERE Id = SCOPE_IDENTITY();
END
GO

-- Regresa SIEMPRE 3 result sets (salón, paquetes, fotos), vacíos si el salón no existe,
-- para que la API los pueda leer en orden.
CREATE OR ALTER PROCEDURE sp_Salon_Obtener
    @Id      INT,
    @Codigo  INT OUTPUT,
    @Mensaje NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM Salones WHERE Id = @Id)
        SELECT @Codigo = 0, @Mensaje = N'Consulta exitosa.';
    ELSE
        SELECT @Codigo = 2001, @Mensaje = N'El salón no existe.';

    SELECT Id, AdminId, Nombre, Zona, Capacidad, Descripcion, Estado, FechaCreacion
    FROM Salones WHERE Id = @Id;

    SELECT Id, SalonId, NombrePaquete, Descripcion, Precio
    FROM Paquetes WHERE SalonId = @Id ORDER BY Id;

    SELECT Id, SalonId, Url
    FROM FotosSalon WHERE SalonId = @Id ORDER BY Id;
END
GO

-- Si @Estado viene NULL se conserva el actual. Descripcion NULL la borra.
CREATE OR ALTER PROCEDURE sp_Salon_Actualizar
    @Id          INT,
    @AdminId     INT,
    @Nombre      NVARCHAR(150),
    @Zona        NVARCHAR(200),
    @Capacidad   INT,
    @Descripcion NVARCHAR(1000) = NULL,
    @Estado      NVARCHAR(20) = NULL,
    @Codigo      INT OUTPUT,
    @Mensaje     NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @DuenoId INT, @EstadoActual NVARCHAR(20);
    SELECT @DuenoId = AdminId, @EstadoActual = Estado FROM Salones WHERE Id = @Id;

    IF @DuenoId IS NULL
    BEGIN SELECT @Codigo = 2001, @Mensaje = N'El salón no existe.'; RETURN; END

    IF @DuenoId <> ISNULL(@AdminId, 0)
    BEGIN SELECT @Codigo = 2002, @Mensaje = N'Solo el administrador dueño del salón puede modificarlo.'; RETURN; END

    SET @Nombre = TRIM(@Nombre);
    SET @Zona = TRIM(@Zona);
    SET @Descripcion = NULLIF(TRIM(@Descripcion), '');
    SET @Estado = ISNULL(NULLIF(LOWER(TRIM(@Estado)), ''), @EstadoActual);

    IF ISNULL(@Nombre, '') = ''
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'El nombre del salón es obligatorio.'; RETURN; END

    IF ISNULL(@Zona, '') = ''
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'La zona es obligatoria.'; RETURN; END

    IF ISNULL(@Capacidad, 0) <= 0
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'La capacidad debe ser mayor a 0.'; RETURN; END

    IF @Estado NOT IN ('publicado', 'no_publicado')
    BEGIN SELECT @Codigo = 2004, @Mensaje = N'El estado debe ser "publicado" o "no_publicado".'; RETURN; END

    IF @Estado = 'publicado' AND NOT EXISTS (SELECT 1 FROM Paquetes WHERE SalonId = @Id)
    BEGIN SELECT @Codigo = 2003, @Mensaje = N'Para publicar el salón se requiere al menos un paquete.'; RETURN; END

    UPDATE Salones
    SET Nombre = @Nombre, Zona = @Zona, Capacidad = @Capacidad,
        Descripcion = @Descripcion, Estado = @Estado
    WHERE Id = @Id;

    SELECT @Codigo = 0, @Mensaje = N'Salón actualizado correctamente.';
END
GO

-- Paginado: primer result set = datos del paginado, segundo = registros de la página.
-- @AdminId NULL lista todos. @Pagina < 1 se toma como 1; @TamanoPagina va de 1 a 100.
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
           (SELECT COUNT(*) FROM Paquetes p WHERE p.SalonId = s.Id) AS TotalPaquetes
    FROM Salones s
    WHERE @AdminId IS NULL OR s.AdminId = @AdminId
    ORDER BY s.Id DESC
    OFFSET (@Pagina - 1) * @TamanoPagina ROWS
    FETCH NEXT @TamanoPagina ROWS ONLY;
END
GO

-- =========================================================================
-- PAQUETES
-- =========================================================================

CREATE OR ALTER PROCEDURE sp_Paquete_Crear
    @SalonId       INT,
    @AdminId       INT,
    @NombrePaquete NVARCHAR(150),
    @Descripcion   NVARCHAR(500) = NULL,
    @Precio        DECIMAL(10,2),
    @Codigo        INT OUTPUT,
    @Mensaje       NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @DuenoId INT = (SELECT AdminId FROM Salones WHERE Id = @SalonId);

    IF @DuenoId IS NULL
    BEGIN SELECT @Codigo = 2001, @Mensaje = N'El salón no existe.'; RETURN; END

    IF @DuenoId <> ISNULL(@AdminId, 0)
    BEGIN SELECT @Codigo = 2002, @Mensaje = N'Solo el administrador dueño del salón puede modificarlo.'; RETURN; END

    SET @NombrePaquete = TRIM(@NombrePaquete);
    SET @Descripcion = NULLIF(TRIM(@Descripcion), '');

    IF ISNULL(@NombrePaquete, '') = ''
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'El nombre del paquete es obligatorio.'; RETURN; END

    IF ISNULL(@Precio, 0) <= 0
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'El precio debe ser mayor a 0.'; RETURN; END

    INSERT INTO Paquetes (SalonId, NombrePaquete, Descripcion, Precio)
    VALUES (@SalonId, @NombrePaquete, @Descripcion, @Precio);

    SELECT @Codigo = 0, @Mensaje = N'Paquete agregado correctamente.';

    SELECT Id, SalonId, NombrePaquete, Descripcion, Precio
    FROM Paquetes WHERE Id = SCOPE_IDENTITY();
END
GO

-- Descripcion NULL la borra.
CREATE OR ALTER PROCEDURE sp_Paquete_Actualizar
    @Id            INT,
    @AdminId       INT,
    @NombrePaquete NVARCHAR(150),
    @Descripcion   NVARCHAR(500) = NULL,
    @Precio        DECIMAL(10,2),
    @Codigo        INT OUTPUT,
    @Mensaje       NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @DuenoId INT = (
        SELECT s.AdminId FROM Paquetes p JOIN Salones s ON s.Id = p.SalonId WHERE p.Id = @Id);

    IF @DuenoId IS NULL
    BEGIN SELECT @Codigo = 3001, @Mensaje = N'El paquete no existe.'; RETURN; END

    IF @DuenoId <> ISNULL(@AdminId, 0)
    BEGIN SELECT @Codigo = 2002, @Mensaje = N'Solo el administrador dueño del salón puede modificarlo.'; RETURN; END

    SET @NombrePaquete = TRIM(@NombrePaquete);
    SET @Descripcion = NULLIF(TRIM(@Descripcion), '');

    IF ISNULL(@NombrePaquete, '') = ''
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'El nombre del paquete es obligatorio.'; RETURN; END

    IF ISNULL(@Precio, 0) <= 0
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'El precio debe ser mayor a 0.'; RETURN; END

    UPDATE Paquetes
    SET NombrePaquete = @NombrePaquete, Descripcion = @Descripcion, Precio = @Precio
    WHERE Id = @Id;

    SELECT @Codigo = 0, @Mensaje = N'Paquete actualizado correctamente.';

    SELECT Id, SalonId, NombrePaquete, Descripcion, Precio
    FROM Paquetes WHERE Id = @Id;
END
GO

-- No deja un salón publicado sin paquetes.
CREATE OR ALTER PROCEDURE sp_Paquete_Eliminar
    @Id      INT,
    @AdminId INT,
    @Codigo  INT OUTPUT,
    @Mensaje NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @SalonId INT, @DuenoId INT, @Estado NVARCHAR(20);
    SELECT @SalonId = s.Id, @DuenoId = s.AdminId, @Estado = s.Estado
    FROM Paquetes p JOIN Salones s ON s.Id = p.SalonId
    WHERE p.Id = @Id;

    IF @SalonId IS NULL
    BEGIN SELECT @Codigo = 3001, @Mensaje = N'El paquete no existe.'; RETURN; END

    IF @DuenoId <> ISNULL(@AdminId, 0)
    BEGIN SELECT @Codigo = 2002, @Mensaje = N'Solo el administrador dueño del salón puede modificarlo.'; RETURN; END

    IF @Estado = 'publicado' AND (SELECT COUNT(*) FROM Paquetes WHERE SalonId = @SalonId) <= 1
    BEGIN SELECT @Codigo = 3002, @Mensaje = N'Un salón publicado debe tener al menos un paquete. Despublícalo antes de borrar el último.'; RETURN; END

    DELETE FROM Paquetes WHERE Id = @Id;

    SELECT @Codigo = 0, @Mensaje = N'Paquete eliminado correctamente.';
END
GO

-- =========================================================================
-- BÚSQUEDA (Sprint 3 — HU-09)
-- =========================================================================

-- Búsqueda pública: solo salones publicados. Todos los filtros son opcionales y combinables;
-- NULL, vacío o <= 0 significa "sin filtro". Nunca regresa error: sin resultados es éxito con lista vacía.
-- Zona: coincidencia parcial sin importar mayúsculas ni acentos ("rio" encuentra "Tepeji del Río").
-- PrecioMaximo: el salón entra si su paquete más barato (PrecioDesde) cuesta eso o menos.
-- Paginado igual que sp_Salon_Listar: primer result set = datos del paginado, segundo = la página.
CREATE OR ALTER PROCEDURE sp_Salon_Buscar
    @Zona            NVARCHAR(200) = NULL,
    @CapacidadMinima INT = NULL,
    @PrecioMaximo    DECIMAL(10,2) = NULL,
    @Pagina          INT = 1,
    @TamanoPagina    INT = 10,
    @Codigo          INT OUTPUT,
    @Mensaje         NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @Pagina = CASE WHEN ISNULL(@Pagina, 0) < 1 THEN 1 ELSE @Pagina END;
    SET @TamanoPagina = CASE
        WHEN ISNULL(@TamanoPagina, 0) < 1 THEN 10
        WHEN @TamanoPagina > 100 THEN 100
        ELSE @TamanoPagina END;
    SET @Zona = NULLIF(TRIM(@Zona), '');
    IF @CapacidadMinima <= 0 SET @CapacidadMinima = NULL;
    IF @PrecioMaximo <= 0 SET @PrecioMaximo = NULL;

    SELECT s.Id, s.Nombre, s.Zona, s.Capacidad, p.PrecioDesde,
           (SELECT TOP 1 f.Url FROM FotosSalon f WHERE f.SalonId = s.Id ORDER BY f.Id) AS FotoPrincipal
    INTO #Resultados
    FROM Salones s
    CROSS APPLY (SELECT MIN(Precio) AS PrecioDesde FROM Paquetes WHERE SalonId = s.Id) p
    WHERE s.Estado = 'publicado'
      AND p.PrecioDesde IS NOT NULL
      AND (@Zona IS NULL
           OR s.Zona COLLATE Latin1_General_CI_AI LIKE N'%' + @Zona + N'%' COLLATE Latin1_General_CI_AI)
      AND (@CapacidadMinima IS NULL OR s.Capacidad >= @CapacidadMinima)
      AND (@PrecioMaximo IS NULL OR p.PrecioDesde <= @PrecioMaximo)
    -- Con filtros opcionales, RECOMPILE arma el plan con los valores reales de cada búsqueda
    -- para que SQL Server sí use los índices de 03_Indices.sql.
    OPTION (RECOMPILE);

    DECLARE @Total INT = (SELECT COUNT(*) FROM #Resultados);

    SELECT @Codigo = 0,
           @Mensaje = CASE WHEN @Total = 0 THEN N'No se encontraron salones con esos filtros.'
                           ELSE N'Consulta exitosa.' END;

    SELECT @Total AS TotalRegistros,
           (@Total + @TamanoPagina - 1) / @TamanoPagina AS TotalPaginas,
           @Pagina AS Pagina,
           @TamanoPagina AS TamanoPagina;

    SELECT Id, Nombre, Zona, Capacidad, PrecioDesde, FotoPrincipal
    FROM #Resultados
    ORDER BY PrecioDesde, Id
    OFFSET (@Pagina - 1) * @TamanoPagina ROWS
    FETCH NEXT @TamanoPagina ROWS ONLY;
END
GO
