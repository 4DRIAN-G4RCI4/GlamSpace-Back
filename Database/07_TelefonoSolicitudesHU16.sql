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
