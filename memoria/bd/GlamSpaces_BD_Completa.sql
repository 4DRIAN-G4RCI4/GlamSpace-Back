/* =========================================================================
   GlamSpaces — Script completo de la base de datos (esquema vigente)
   ARCHIVO GENERADO por scripts/generar-bd-completa.ps1. NO editar a mano:
   se cambia el script de Database/ y se vuelve a generar.

   Contiene, en orden: 01_Tablas.sql, 02_StoredProcedures.sql, 03_Indices.sql, 06_SolicitudesHU14.sql, 07_TelefonoSolicitudesHU16.sql, 08_SolicitudesHU15.sql.
   Es idempotente (IF NOT EXISTS / CREATE OR ALTER). Sin datos de prueba ni semillas:
   04_DatosPrueba.sql (solo local) y la semilla de 06_SolicitudesHU14.sql se corren aparte.

   Las migraciones 002 a 004 (BusquedasFavoritas, Salones.Telefono y su índice único) son de
   práctica del Sprint 4: solo existen en GlamSpaces.Migrations, no en producción.
   ========================================================================= */

-- #########################################################################
-- Database/01_Tablas.sql
-- #########################################################################

/* =========================================================================
   GlamSpaces — Tablas (glamspaces-db, Azure SQL)
   Idempotente: se puede volver a correr sin romper nada si ya existen.
   Correr ANTES de 02_StoredProcedures.sql.
   ========================================================================= */

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Usuarios')
BEGIN
    CREATE TABLE Usuarios (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        NombreCompleto  NVARCHAR(150) NOT NULL,
        Correo          NVARCHAR(150) NOT NULL UNIQUE,
        PasswordHash    NVARCHAR(MAX) NOT NULL,
        TipoCuenta      NVARCHAR(20) NOT NULL,   -- 'cliente' o 'administrador'
        NombreSalon     NVARCHAR(150) NULL,      -- solo si TipoCuenta = 'administrador'
        FechaRegistro   DATETIME2 NOT NULL DEFAULT GETDATE()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Salones')
BEGIN
    CREATE TABLE Salones (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        AdminId       INT NOT NULL,
        Nombre        NVARCHAR(150) NOT NULL,
        Zona          NVARCHAR(200) NOT NULL,
        Capacidad     INT NOT NULL,
        Descripcion   NVARCHAR(1000) NULL,
        Estado        NVARCHAR(20) NOT NULL DEFAULT 'no_publicado',
        FechaCreacion DATETIME2 NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_Salones_Usuarios FOREIGN KEY (AdminId) REFERENCES Usuarios(Id),
        CONSTRAINT CK_Salones_Capacidad CHECK (Capacidad > 0),
        CONSTRAINT CK_Salones_Estado CHECK (Estado IN ('publicado', 'no_publicado'))
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Paquetes')
BEGIN
    CREATE TABLE Paquetes (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        SalonId         INT NOT NULL,
        NombrePaquete   NVARCHAR(150) NOT NULL,
        Descripcion     NVARCHAR(500) NULL,
        Precio          DECIMAL(10,2) NOT NULL,
        CONSTRAINT FK_Paquetes_Salones FOREIGN KEY (SalonId) REFERENCES Salones(Id),
        CONSTRAINT CK_Paquetes_Precio CHECK (Precio > 0)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'FotosSalon')
BEGIN
    CREATE TABLE FotosSalon (
        Id      INT IDENTITY(1,1) PRIMARY KEY,
        SalonId INT NOT NULL,
        Url     NVARCHAR(500) NOT NULL,
        CONSTRAINT FK_FotosSalon_Salones FOREIGN KEY (SalonId) REFERENCES Salones(Id)
    );
END
GO

-- Sprint 4 — HU-11. Solo las reservaciones 'confirmada' ocupan la fecha del salón (HU-12).
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Reservaciones')
BEGIN
    CREATE TABLE Reservaciones (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        SalonId       INT NOT NULL,
        ClienteId     INT NOT NULL,
        PaqueteId     INT NULL,
        FechaEvento   DATE NOT NULL,
        Estado        NVARCHAR(20) NOT NULL DEFAULT 'pendiente',
        FechaCreacion DATETIME2 NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_Reservaciones_Salones FOREIGN KEY (SalonId) REFERENCES Salones(Id),
        CONSTRAINT FK_Reservaciones_Usuarios FOREIGN KEY (ClienteId) REFERENCES Usuarios(Id),
        CONSTRAINT FK_Reservaciones_Paquetes FOREIGN KEY (PaqueteId) REFERENCES Paquetes(Id),
        CONSTRAINT CK_Reservaciones_Estado CHECK (Estado IN ('pendiente', 'confirmada', 'cancelada'))
    );
END
GO
GO

-- #########################################################################
-- Database/02_StoredProcedures.sql
-- #########################################################################

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

-- =========================================================================
-- DISPONIBILIDAD (Sprint 4 — HU-12)
-- =========================================================================

-- Fechas ocupadas de un salón en un mes. Solo cuentan las reservaciones 'confirmada';
-- las pendientes y canceladas no ocupan la fecha.
-- Un mes sin fechas ocupadas NO es error: éxito con result set vacío.
-- Regresa un solo result set con las fechas (FechaEvento) ordenadas.
CREATE OR ALTER PROCEDURE sp_Salon_Disponibilidad
    @SalonId INT,
    @Anio    INT,
    @Mes     INT,
    @Codigo  INT OUTPUT,
    @Mensaje NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF ISNULL(@Mes, 0) NOT BETWEEN 1 AND 12 OR ISNULL(@Anio, 0) NOT BETWEEN 2000 AND 2100
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'Mes o año inválido.'; RETURN; END

    IF NOT EXISTS (SELECT 1 FROM Salones WHERE Id = @SalonId)
    BEGIN SELECT @Codigo = 2001, @Mensaje = N'El salón no existe.'; RETURN; END

    DECLARE @Inicio DATE = DATEFROMPARTS(@Anio, @Mes, 1);
    DECLARE @Fin DATE = DATEADD(MONTH, 1, @Inicio);

    SELECT DISTINCT FechaEvento
    INTO #Fechas
    FROM Reservaciones
    WHERE SalonId = @SalonId
      AND Estado = 'confirmada'
      AND FechaEvento >= @Inicio
      AND FechaEvento < @Fin;

    SELECT @Codigo = 0,
           @Mensaje = CASE WHEN NOT EXISTS (SELECT 1 FROM #Fechas)
                           THEN N'El salón no tiene fechas ocupadas en ese mes.'
                           ELSE N'Consulta exitosa.' END;

    SELECT FechaEvento FROM #Fechas ORDER BY FechaEvento;
END
GO
GO

-- #########################################################################
-- Database/03_Indices.sql
-- #########################################################################

/* =========================================================================
   GlamSpaces — Índices para la búsqueda de salones (Sprint 3 — HU-08)
   Correr DESPUÉS de 01_Tablas.sql y 02_StoredProcedures.sql.
   Idempotente: solo crea los índices que no existan.

   Los usa sp_Salon_Buscar (HU-09):
     - filtrado por Estado = 'publicado', zona y capacidad;
     - "precio desde" = paquete más barato de cada salón;
     - foto principal = primera foto de cada salón.
   ========================================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- Salones publicados por capacidad, incluyendo zona y nombre.
--  - Filtrado (WHERE Estado = 'publicado'): solo guarda los salones que participan en la
--    búsqueda, así es más chico y rápido que la tabla completa.
--  - Capacidad es la llave: "capacidad mínima" hace una búsqueda directa (seek) en el índice.
--  - Zona va en INCLUDE y no como llave, porque se busca por coincidencia parcial
--    (LIKE '%tula%'), que no puede usar un índice para saltar directo. Al estar incluida,
--    el filtro de zona se resuelve dentro del índice sin leer la tabla.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Salones_Publicados_Capacidad_Zona')
    CREATE NONCLUSTERED INDEX IX_Salones_Publicados_Capacidad_Zona
        ON Salones (Capacidad)
        INCLUDE (Zona, Nombre)
        WHERE Estado = 'publicado';
GO

-- Paquetes por salón y precio: el precio más bajo de cada salón (MIN) es la primera fila
-- del índice para ese SalonId. Además acelera el FK Paquetes → Salones.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Paquetes_SalonId_Precio')
    CREATE NONCLUSTERED INDEX IX_Paquetes_SalonId_Precio
        ON Paquetes (SalonId, Precio);
GO

-- Fotos por salón: la primera foto (menor Id) sale directo del índice.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FotosSalon_SalonId')
    CREATE NONCLUSTERED INDEX IX_FotosSalon_SalonId
        ON FotosSalon (SalonId)
        INCLUDE (Url);
GO

-- =========================================================================
-- Sprint 4 — Reservaciones (HU-11 / HU-12)
-- =========================================================================

-- Disponibilidad del salón por mes (sp_Salon_Disponibilidad): busca por salón y rango
-- de fechas; Estado va incluido para filtrar las confirmadas sin leer la tabla.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Reservaciones_Salon_Fecha')
    CREATE NONCLUSTERED INDEX IX_Reservaciones_Salon_Fecha
        ON Reservaciones (SalonId, FechaEvento)
        INCLUDE (Estado);
GO

-- Regla de negocio: un salón no puede tener 2 reservaciones confirmadas el mismo día.
-- Único y filtrado: las pendientes y canceladas sí pueden repetir fecha.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Reservaciones_Confirmada')
    CREATE UNIQUE NONCLUSTERED INDEX UX_Reservaciones_Confirmada
        ON Reservaciones (SalonId, FechaEvento)
        WHERE Estado = 'confirmada';
GO
GO

-- #########################################################################
-- Database/06_SolicitudesHU14.sql
-- #########################################################################

/* =========================================================================
   GlamSpaces — Solicitudes de reservación (Sprint 5 — HU-14)
   Correr DESPUÉS de 01_Tablas.sql (necesita Salones, Paquetes y Usuarios).
   Idempotente: la tabla, las restricciones, el índice y la semilla solo se crean si faltan.
   Es el mismo contenido que se aplicó a mano en glamspaces-db; la migración
   M005_CrearSolicitudes crea el mismo esquema (sin la semilla).

   Semilla: 3 solicitudes (pendiente, confirmada y rechazada) con el primer salón,
   su primer paquete y el primer usuario con TipoCuenta = 'cliente'. Si falta alguno, THROW.
   ========================================================================= */

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON; -- requerido por el índice filtrado
GO

-- -------------------------------------------------------------------------
-- 1) Tabla
-- -------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.Solicitudes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Solicitudes (
        Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Solicitudes PRIMARY KEY,
        SalonId         INT           NOT NULL CONSTRAINT FK_Solicitudes_Salones  REFERENCES dbo.Salones(Id),
        ClienteId       INT           NOT NULL CONSTRAINT FK_Solicitudes_Usuarios REFERENCES dbo.Usuarios(Id),
        PaqueteId       INT           NULL     CONSTRAINT FK_Solicitudes_Paquetes REFERENCES dbo.Paquetes(Id),
        FechaEvento     DATE          NOT NULL,
        TipoEvento      NVARCHAR(100) NOT NULL,
        NumeroInvitados INT           NOT NULL,
        Comentarios     NVARCHAR(500) NULL,
        Estado          NVARCHAR(20)  NOT NULL CONSTRAINT DF_Solicitudes_Estado DEFAULT 'pendiente',
        FechaCreacion   DATETIME2(7)  NOT NULL CONSTRAINT DF_Solicitudes_FechaCreacion DEFAULT SYSUTCDATETIME()
    );
    PRINT 'Tabla Solicitudes creada.';
END
ELSE
    PRINT 'La tabla Solicitudes ya existe.';
GO

-- -------------------------------------------------------------------------
-- 2) Restricciones CHECK
-- -------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Solicitudes_Invitados')
    ALTER TABLE dbo.Solicitudes ADD CONSTRAINT CK_Solicitudes_Invitados CHECK (NumeroInvitados > 0);

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Solicitudes_Estado')
    ALTER TABLE dbo.Solicitudes ADD CONSTRAINT CK_Solicitudes_Estado CHECK (Estado IN ('pendiente', 'confirmada', 'rechazada'));
GO

-- -------------------------------------------------------------------------
-- 3) Índice único filtrado: una sola solicitud PENDIENTE por cliente, salón y fecha
-- -------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Solicitudes_Cliente_Salon_Fecha_Pendiente'
               AND object_id = OBJECT_ID(N'dbo.Solicitudes'))
    CREATE UNIQUE NONCLUSTERED INDEX UX_Solicitudes_Cliente_Salon_Fecha_Pendiente
        ON dbo.Solicitudes (ClienteId, SalonId, FechaEvento)
        WHERE Estado = 'pendiente';
GO
GO

-- #########################################################################
-- Database/07_TelefonoSolicitudesHU16.sql
-- #########################################################################

/* =========================================================================
   GlamSpaces — Teléfono de contacto en Solicitudes (Sprint 5 — HU-16)
   Correr DESPUÉS de 06_SolicitudesHU14.sql (necesita la tabla Solicitudes).
   Idempotente: si la columna ya existe, no hace nada.

   TelefonoContacto es NULL en la BD porque ya hay solicitudes sin teléfono;
   el API lo exige al crear una solicitud nueva (HU-15 / HU-16).
   ========================================================================= */

SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.Solicitudes', N'U') IS NULL
    THROW 50004, 'HU-16: no existe la tabla dbo.Solicitudes; corre primero 06_SolicitudesHU14.sql.', 1;

IF COL_LENGTH(N'dbo.Solicitudes', N'TelefonoContacto') IS NULL
BEGIN
    ALTER TABLE dbo.Solicitudes ADD TelefonoContacto NVARCHAR(20) NULL;
    PRINT 'Columna TelefonoContacto agregada a Solicitudes.';
END
ELSE
    PRINT 'La columna TelefonoContacto ya existe.';

-- Verificación
SELECT c.name AS Columna, TYPE_NAME(c.user_type_id) AS Tipo, c.max_length / 2 AS Longitud,
       CASE c.is_nullable WHEN 1 THEN 'NULL' ELSE 'NOT NULL' END AS Nulos
FROM sys.columns c
WHERE c.object_id = OBJECT_ID(N'dbo.Solicitudes') AND c.name = N'TelefonoContacto';
GO

-- #########################################################################
-- Database/08_SolicitudesHU15.sql
-- #########################################################################

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
GO
