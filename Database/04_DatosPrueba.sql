/* =========================================================================
   GlamSpaces — Datos de prueba para la búsqueda (Sprint 3 — HU-08)
   ⚠️ SOLO PARA BASES DE PRUEBA / LOCALES. No correr en producción.

   6 salones con distintas zonas, capacidades y precios (5 publicados y 1 sin publicar),
   para verificar los filtros de zona, capacidad y precio de sp_Salon_Buscar.
   Idempotente: si los salones ya existen, no los duplica.
   ========================================================================= */

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON; -- requerido para insertar en tablas con índices filtrados

DECLARE @AdminId INT = (SELECT Id FROM Usuarios WHERE Correo = 'admin.prueba@glamspaces.com');

IF @AdminId IS NULL
BEGIN
    -- PasswordHash no válido a propósito: esta cuenta no puede iniciar sesión.
    -- Fechas explícitas: en glamspaces-db las tablas las creó EF y no tienen DEFAULT.
    INSERT INTO Usuarios (NombreCompleto, Correo, PasswordHash, TipoCuenta, NombreSalon, FechaRegistro)
    VALUES ('Admin de Prueba', 'admin.prueba@glamspaces.com', 'sin-login', 'administrador', 'Salones de Prueba', GETDATE());
    SET @AdminId = SCOPE_IDENTITY();
END

DECLARE @Salones TABLE (Nombre NVARCHAR(150), Zona NVARCHAR(200), Capacidad INT, Estado NVARCHAR(20), Precios NVARCHAR(100));
INSERT INTO @Salones VALUES
    (N'[Prueba] Jardín Encanto',     N'Tula de Allende, Hgo.', 150, 'publicado',    N'8500,15000'),
    (N'[Prueba] Terraza Monarca',    N'Tepeji del Río, Hgo.',   80, 'publicado',    N'5000'),
    (N'[Prueba] Hacienda El Rosario', N'Tula de Allende, Hgo.', 400, 'publicado',   N'30000,45000'),
    (N'[Prueba] Salón Fiesta Kids',  N'Tlahuelilpan, Hgo.',    100, 'publicado',    N'3500,6000'),
    (N'[Prueba] Quinta Las Palmas',  N'Mixquiahuala, Hgo.',    250, 'publicado',    N'12000'),
    (N'[Prueba] Jardín Oculto',      N'Tula de Allende, Hgo.', 500, 'no_publicado', N'1000');

INSERT INTO Salones (AdminId, Nombre, Zona, Capacidad, Estado, FechaCreacion)
SELECT @AdminId, s.Nombre, s.Zona, s.Capacidad, s.Estado, GETDATE()
FROM @Salones s
WHERE NOT EXISTS (SELECT 1 FROM Salones x WHERE x.Nombre = s.Nombre);

INSERT INTO Paquetes (SalonId, NombrePaquete, Precio)
SELECT x.Id, CONCAT(N'Paquete ', TRIM(p.value)), CAST(TRIM(p.value) AS DECIMAL(10,2))
FROM @Salones s
JOIN Salones x ON x.Nombre = s.Nombre
CROSS APPLY STRING_SPLIT(s.Precios, ',') p
WHERE NOT EXISTS (SELECT 1 FROM Paquetes y WHERE y.SalonId = x.Id);

SELECT x.Id, x.Nombre, x.Zona, x.Capacidad, x.Estado, MIN(p.Precio) AS PrecioDesde
FROM Salones x JOIN Paquetes p ON p.SalonId = x.Id
WHERE x.Nombre LIKE N'[[]Prueba]%'
GROUP BY x.Id, x.Nombre, x.Zona, x.Capacidad, x.Estado
ORDER BY x.Id;
