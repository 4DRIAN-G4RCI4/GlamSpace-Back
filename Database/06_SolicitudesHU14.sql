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

-- -------------------------------------------------------------------------
-- 4) Semilla: 3 solicitudes, una por estado
-- -------------------------------------------------------------------------
DECLARE @SalonId INT = (SELECT TOP 1 Id FROM dbo.Salones ORDER BY Id);
DECLARE @PaqueteId INT = (SELECT TOP 1 Id FROM dbo.Paquetes WHERE SalonId = @SalonId ORDER BY Id);
DECLARE @ClienteId INT = (SELECT TOP 1 Id FROM dbo.Usuarios WHERE TipoCuenta = 'cliente' ORDER BY Id);

IF @SalonId IS NULL
    THROW 50001, 'Semilla HU-14: no hay ningún salón en dbo.Salones.', 1;
IF @PaqueteId IS NULL
    THROW 50002, 'Semilla HU-14: el primer salón no tiene paquetes.', 1;
IF @ClienteId IS NULL
    THROW 50003, 'Semilla HU-14: no hay ningún usuario con TipoCuenta = ''cliente''.', 1;

-- Fecha y estado explícitos para no depender de los DEFAULT.
INSERT INTO dbo.Solicitudes (SalonId, ClienteId, PaqueteId, FechaEvento, TipoEvento, NumeroInvitados, Comentarios, Estado, FechaCreacion)
SELECT @SalonId, @ClienteId, @PaqueteId, s.FechaEvento, s.TipoEvento, s.NumeroInvitados, s.Comentarios, s.Estado, SYSUTCDATETIME()
FROM (VALUES
    (CAST('2026-12-05' AS DATE), N'Boda',       120, N'Ceremonia y recepción en el jardín.', 'pendiente'),
    (CAST('2026-12-12' AS DATE), N'XV años',     90, N'Requiere pista de baile.',            'confirmada'),
    (CAST('2026-12-19' AS DATE), N'Cumpleaños',  40, NULL,                                    'rechazada')
) s (FechaEvento, TipoEvento, NumeroInvitados, Comentarios, Estado)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Solicitudes x
                  WHERE x.SalonId = @SalonId AND x.ClienteId = @ClienteId AND x.FechaEvento = s.FechaEvento);

SELECT Id, SalonId, ClienteId, PaqueteId, FechaEvento, TipoEvento, NumeroInvitados, Estado
FROM dbo.Solicitudes
WHERE SalonId = @SalonId AND ClienteId = @ClienteId
ORDER BY FechaEvento;
GO
