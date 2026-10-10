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
