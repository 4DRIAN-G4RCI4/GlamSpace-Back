/* =========================================================================
   GlamSpaces — Carga masiva de DEMOSTRACIÓN: 1,500 salones en la región Tula–Tepeji (Sprint 7)
   NO es migración ni corre en los workflows: se corre a mano. Correr DESPUÉS de 01 a 14.
   Borrar con 16_BorrarCargaMasiva.sql y revisar con 17_VerificarCargaMasiva.sql.

   - 30 administradores de demo (demoNN@demo.glamspaces.test, 50 salones cada uno). Su PasswordHash
     no tiene el formato {iteraciones}.{salt}.{hash} de PasswordHasher: con ninguna contraseña se
     puede iniciar sesión. No se toca ningún usuario existente.
   - 1,500 salones 'publicado', 1 a 3 paquetes (Básico/Plus/Premium, 3,000 a 45,000 MXN) y 4 o 5
     fotos cada uno. TODO es determinista (aritmética sobre i = número del salón - 1): cada corrida
     produce exactamente lo mismo.
   - Nombre = tipo[i % 8] + tema[(i / 8) % 40]: se repite cada 320 salones (r = i % 320). La zona es
     (r + 11 * (i / 320)) % 54: los salones con el mismo nombre caen en zonas distintas.
   - Zona = "Colonia, Municipio, Hgo., CP 00000" con colonias y CP reales (api.zippopotam.us/mx,
     municipio confirmado con OpenStreetMap o por ser la cabecera). Sin calles ni números.
     Tula de Allende (16 colonias) y Tepeji del Río (10) tienen más peso.
   - Fotos: URL base del contenedor + demo-NN.ext (copias de la carpeta carga-demo-subir).
     Foto j del salón = (base + j * paso) % F, F = número de fotos; base = i * pasoBase y el paso
     se elige entre varios coprimos con F: ninguna foto se repite dentro de un salón y la principal
     cambia de un salón al siguiente.
   - @Confirmar = 0 solo muestra lo que haría; 1 carga todo en una transacción (todo o nada).
   - Idempotente: si ya hay usuarios @demo.glamspaces.test, se rechaza y no cambia nada.
   ========================================================================= */

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

DECLARE @Confirmar BIT = 0; -- 0 = solo cuenta lo que haría · 1 = ejecuta la carga

DECLARE @Total INT = 1500;
DECLARE @Admins INT = 30;
DECLARE @Dominio NVARCHAR(50) = N'@demo.glamspaces.test';
DECLARE @UrlBase NVARCHAR(200) = N'https://glamspacesfotossa.blob.core.windows.net/fotos-salones/demo/';
-- Sin el formato de PasswordHasher ({iteraciones}.{salt}.{hash}): Verificar() siempre da false.
DECLARE @SinLogin NVARCHAR(50) = N'CUENTA-DEMO-SIN-ACCESO';

DECLARE @Previos INT = (SELECT COUNT(*) FROM Usuarios WHERE Correo LIKE N'%' + @Dominio);
IF @Previos > 0
BEGIN
    RAISERROR(N'Ya existe una carga de demostración (%d usuarios %s). No se cambió nada. Para volver a cargar, corre antes 16_BorrarCargaMasiva.sql.', 16, 1, @Previos, @Dominio);
    RETURN;
END

-- Catálogos ---------------------------------------------------------------
DECLARE @Tipos TABLE (I INT PRIMARY KEY, Texto NVARCHAR(20), Apertura NVARCHAR(150));
INSERT INTO @Tipos VALUES
    (0, N'Salón',    N'Elegante salón climatizado con iluminación ambiental.'),
    (1, N'Jardín',   N'Amplio jardín al aire libre con áreas verdes.'),
    (2, N'Quinta',   N'Quinta con jardín y salón techado para cualquier temporada.'),
    (3, N'Terraza',  N'Terraza con vista abierta y zona techada.'),
    (4, N'Hacienda', N'Hacienda de estilo tradicional con patio central.'),
    (5, N'Villa',    N'Villa con salón interior y jardín privado.'),
    (6, N'Palacio',  N'Salón de estilo clásico con candiles y techos altos.'),
    (7, N'Loft',     N'Loft de estilo industrial con espacio abierto.');

DECLARE @Temas TABLE (I INT PRIMARY KEY, Texto NVARCHAR(40));
INSERT INTO @Temas VALUES
    (0, N'Las Palmas'), (1, N'San Marcos'), (2, N'Aurora'), (3, N'Los Arcos'), (4, N'El Encanto'),
    (5, N'Real'), (6, N'Versalles'), (7, N'Bugambilias'), (8, N'Los Pinos'), (9, N'Jacaranda'),
    (10, N'El Mirador'), (11, N'Las Fuentes'), (12, N'Los Olivos'), (13, N'Magnolia'), (14, N'Gardenias'),
    (15, N'El Roble'), (16, N'Cristal'), (17, N'Diamante'), (18, N'Monarca'), (19, N'La Joya'),
    (20, N'Los Encinos'), (21, N'El Lago'), (22, N'Santa Fe'), (23, N'Del Valle'), (24, N'Las Rosas'),
    (25, N'El Paraíso'), (26, N'Imperial'), (27, N'La Herradura'), (28, N'Esmeralda'), (29, N'Primavera'),
    (30, N'Alborada'), (31, N'Los Sauces'), (32, N'El Nogal'), (33, N'Azalea'), (34, N'Orquídea'),
    (35, N'Girasol'), (36, N'Luna Nueva'), (37, N'Alameda'), (38, N'Las Flores'), (39, N'Mediterráneo');

DECLARE @Eventos TABLE (I INT PRIMARY KEY, Texto NVARCHAR(150));
INSERT INTO @Eventos VALUES
    (0, N'Ideal para bodas y XV años.'),
    (1, N'Ideal para bautizos y primeras comuniones.'),
    (2, N'Ideal para graduaciones y fiestas de fin de año.'),
    (3, N'Ideal para bodas, XV años y bautizos.'),
    (4, N'Ideal para graduaciones, cumpleaños y XV años.');

DECLARE @Incluye TABLE (I INT PRIMARY KEY, Texto NVARCHAR(150));
INSERT INTO @Incluye VALUES
    (0, N'Incluye estacionamiento y mobiliario.'),
    (1, N'Incluye cocina equipada y pista de baile.'),
    (2, N'Incluye mobiliario, pista de baile y área para niños.'),
    (3, N'Incluye estacionamiento, cocina equipada y mobiliario.'),
    (4, N'Incluye área para niños y estacionamiento.'),
    (5, N'Incluye pista de baile y mobiliario.');

-- Colonias y CP reales (api.zippopotam.us/mx/{cp}, consultado el 13/11/2026). El nombre de la
-- colonia va tal como lo da la fuente. Municipio confirmado con OpenStreetMap (Nominatim) o porque
-- la colonia es la cabecera municipal; se descartaron los CP dudosos.
DECLARE @Zonas TABLE (I INT PRIMARY KEY, Colonia NVARCHAR(60), Municipio NVARCHAR(60), CP CHAR(5));
INSERT INTO @Zonas VALUES
    (0,  N'Tula de Allende Centro',   N'Tula de Allende', '42800'),
    (1,  N'San Andres',               N'Tula de Allende', '42800'),
    (2,  N'Ingenieros de Refineria',  N'Tula de Allende', '42800'),
    (3,  N'Las Palmas',               N'Tula de Allende', '42800'),
    (4,  N'San Jose',                 N'Tula de Allende', '42805'),
    (5,  N'16 de Enero',              N'Tula de Allende', '42808'),
    (6,  N'El Salitre',               N'Tula de Allende', '42808'),
    (7,  N'Lomas Del Salitre',        N'Tula de Allende', '42808'),
    (8,  N'Heroes Carranza',          N'Tula de Allende', '42810'),
    (9,  N'Santa Maria Macua',        N'Tula de Allende', '42810'),
    (10, N'Tolteca',                  N'Tula de Allende', '42830'),
    (11, N'San Pedro Alpuyeca',       N'Tula de Allende', '42830'),
    (12, N'El Canal',                 N'Tula de Allende', '42830'),
    (13, N'San Marcos',               N'Tula de Allende', '42831'),
    (14, N'San Miguel Vindho',        N'Tula de Allende', '42842'),
    (15, N'Los Naranjos',             N'Tula de Allende', '42842'),
    (16, N'Tepeji de Ocampo Centro',  N'Tepeji del Río de Ocampo', '42850'),
    (17, N'Tinajas',                  N'Tepeji del Río de Ocampo', '42854'),
    (18, N'San Francisco',            N'Tepeji del Río de Ocampo', '42854'),
    (19, N'San Juan Atlaxpa',         N'Tepeji del Río de Ocampo', '42854'),
    (20, N'El Carmen',                N'Tepeji del Río de Ocampo', '42854'),
    (21, N'El Paraiso',               N'Tepeji del Río de Ocampo', '42854'),
    (22, N'Praderas',                 N'Tepeji del Río de Ocampo', '42854'),
    (23, N'Tlaxinacalpan',            N'Tepeji del Río de Ocampo', '42855'),
    (24, N'Noxtongo',                 N'Tepeji del Río de Ocampo', '42855'),
    (25, N'Atengo',                   N'Tepeji del Río de Ocampo', '42855'),
    (26, N'Tepetitlan',               N'Tepetitlán', '42920'),
    (27, N'San Pedro Nextlalpan',     N'Tepetitlán', '42920'),
    (28, N'Tlaxcoapan Centro',        N'Tlaxcoapan', '42950'),
    (29, N'Cruz',                     N'Tlaxcoapan', '42950'),
    (30, N'Lomas de Tlaxcoapan',      N'Tlaxcoapan', '42953'),
    (31, N'Teocalco',                 N'Tlaxcoapan', '42960'),
    (32, N'Doxey',                    N'Tlaxcoapan', '42960'),
    (33, N'Tlahuelilpan',             N'Tlahuelilpan', '42780'),
    (34, N'Munitepec',                N'Tlahuelilpan', '42789'),
    (35, N'Atitalaquia',              N'Atitalaquia', '42970'),
    (36, N'Tlamaco',                  N'Atitalaquia', '42970'),
    (37, N'Tlalminulpa',              N'Atitalaquia', '42970'),
    (38, N'Atotonilco de Tula',       N'Atotonilco de Tula', '42980'),
    (39, N'Ocampo',                   N'Atotonilco de Tula', '42980'),
    (40, N'El Progreso',              N'Atotonilco de Tula', '42980'),
    (41, N'Tezontepec de Aldama',     N'Tezontepec de Aldama', '42760'),
    (42, N'Santiago Acayutlan',       N'Tezontepec de Aldama', '42760'),
    (43, N'Huitel',                   N'Tezontepec de Aldama', '42771'),
    (44, N'Ajacuba',                  N'Ajacuba', '42150'),
    (45, N'Ignacio Zaragoza',         N'Ajacuba', '42150'),
    (46, N'Tetepango',                N'Tetepango', '42940'),
    (47, N'Ulapa de Melchor Ocampo',  N'Tetepango', '42940'),
    (48, N'Chapantongo',              N'Chapantongo', '42900'),
    (49, N'Nopala',                   N'Nopala de Villagrán', '42470'),
    (50, N'Huichapan',                N'Huichapan', '42400'),
    (51, N'Bondojito',                N'Huichapan', '42400'),
    (52, N'Actopan Centro',           N'Actopan', '42500'),
    (53, N'Nuevo Actopan',            N'Actopan', '42500');
DECLARE @TotalZonas INT = (SELECT COUNT(*) FROM @Zonas); -- 54; 11 es coprimo con 54

-- Copias renombradas que se suben a fotos-salones/demo/ (ver README).
DECLARE @Fotos TABLE (I INT PRIMARY KEY, Archivo NVARCHAR(40));
INSERT INTO @Fotos VALUES
    (0, N'demo-01.jpg'), (1, N'demo-02.png'), (2, N'demo-03.jpg'), (3, N'demo-04.jpg'), (4, N'demo-05.webp'),
    (5, N'demo-06.webp'), (6, N'demo-07.webp'), (7, N'demo-08.webp'), (8, N'demo-09.webp'), (9, N'demo-10.webp'),
    (10, N'demo-11.webp'), (11, N'demo-12.webp'), (12, N'demo-13.webp'), (13, N'demo-14.jpeg'), (14, N'demo-15.jpg'),
    (15, N'demo-16.jpeg'), (16, N'demo-17.jpeg'), (17, N'demo-18.jpg'), (18, N'demo-19.jpeg');
DECLARE @F INT = (SELECT COUNT(*) FROM @Fotos);

-- Números 0..9999 sin tablas auxiliares (como la carga del Sprint 6).
DECLARE @Numeros TABLE (N INT PRIMARY KEY);
WITH Diez AS (SELECT n FROM (VALUES (0), (1), (2), (3), (4), (5), (6), (7), (8), (9)) d (n))
INSERT INTO @Numeros SELECT a.n * 1000 + b.n * 100 + c.n * 10 + d.n FROM Diez a, Diez b, Diez c, Diez d;

-- Pasos coprimos con @F (entre 1 y 12, menores que @F): el salón usa uno según i / @F.
DECLARE @Pasos TABLE (K INT PRIMARY KEY, Paso INT);
INSERT INTO @Pasos
SELECT ROW_NUMBER() OVER (ORDER BY p.N) - 1, p.N
FROM @Numeros p
WHERE p.N BETWEEN 1 AND 12 AND p.N < @F
  AND NOT EXISTS (SELECT 1 FROM @Numeros d WHERE d.N BETWEEN 2 AND p.N AND p.N % d.N = 0 AND @F % d.N = 0);
DECLARE @TotalPasos INT = (SELECT COUNT(*) FROM @Pasos);
-- Paso entre la foto principal de un salón y la del siguiente: primer coprimo >= 5.
DECLARE @PasoBase INT = ISNULL((SELECT MIN(Paso) FROM @Pasos WHERE Paso >= 5), 1);

IF @F < 6 OR @TotalPasos = 0
BEGIN
    RAISERROR(N'Se necesitan al menos 6 fotos para repartir 4 o 5 distintas por salón.', 16, 1);
    RETURN;
END

-- Plan de la carga (una fila por salón) -------------------------------------
SELECT n.N AS I,
       n.N % 8 AS Tipo,
       (n.N / 8) % 40 AS Tema,
       ((n.N % 320) + 11 * (n.N / 320)) % @TotalZonas AS Zona,
       n.N % @Admins AS AdminNum,
       50 + ((n.N * 37) % 56) * 10 AS Capacidad,           -- 50 a 600
       1 + (n.N / 2) % 3 AS NumPaquetes,                   -- 1 a 3
       4 + n.N % 2 AS NumFotos,                            -- 4 o 5
       (n.N * @PasoBase) % @F AS FotoBase,
       (SELECT Paso FROM @Pasos WHERE K = (n.N / @F) % @TotalPasos) AS FotoPaso
INTO #Plan
FROM @Numeros n
WHERE n.N < @Total;

SELECT p.I,
       CONCAT('demo', RIGHT(CONCAT('0', p.AdminNum + 1), 2), @Dominio) AS Correo,
       CONCAT(t.Texto, N' ', m.Texto) AS Nombre,
       CONCAT(z.Colonia, N', ', z.Municipio, N', Hgo., CP ', z.CP) AS Zona,
       p.Capacidad,
       CONCAT(t.Apertura, N' ', e.Texto, N' ', x.Texto, N' Capacidad hasta ', p.Capacidad, N' personas.') AS Descripcion,
       3000 + (p.Capacidad - 50) * 20 AS PrecioBasico,      -- 3,000 a 14,000
       p.NumPaquetes, p.NumFotos, p.FotoBase, p.FotoPaso
INTO #Carga
FROM #Plan p
JOIN @Tipos t ON t.I = p.Tipo
JOIN @Temas m ON m.I = p.Tema
JOIN @Zonas z ON z.I = p.Zona
JOIN @Eventos e ON e.I = p.I % 5
JOIN @Incluye x ON x.I = (p.I / 5) % 6;

DECLARE @PaquetesPlan INT = (SELECT SUM(NumPaquetes) FROM #Carga);
DECLARE @FotosPlan INT = (SELECT SUM(NumFotos) FROM #Carga);

IF @Confirmar = 0
BEGIN
    SELECT N'SOLO CONTEO (@Confirmar = 0): no se insertó nada' AS Modo,
           @Admins AS Usuarios, (SELECT COUNT(*) FROM #Carga) AS Salones,
           @PaquetesPlan AS Paquetes, @FotosPlan AS Fotos, @F AS FotosDistintas;
    SELECT TOP 5 I, Correo, Nombre, Zona, Capacidad, PrecioBasico, NumPaquetes, NumFotos FROM #Carga ORDER BY I;
    PRINT N'Revisa los conteos y vuelve a correr con @Confirmar = 1.';
    RETURN;
END

-- Carga ---------------------------------------------------------------------
CREATE TABLE #Usuarios (AdminNum INT PRIMARY KEY, Id INT);
CREATE TABLE #Salones (I INT PRIMARY KEY, Id INT);

BEGIN TRY
    BEGIN TRAN;

    -- MERGE con OUTPUT relaciona cada número de admin con su Id.
    MERGE INTO Usuarios AS u
    USING (SELECT N - 1 AS AdminNum FROM @Numeros WHERE N BETWEEN 1 AND @Admins) AS a
    ON 1 = 0
    WHEN NOT MATCHED THEN
        INSERT (NombreCompleto, Correo, PasswordHash, TipoCuenta, NombreSalon, FechaRegistro)
        VALUES (CONCAT(N'Administrador Demo ', RIGHT(CONCAT('0', a.AdminNum + 1), 2)),
                CONCAT('demo', RIGHT(CONCAT('0', a.AdminNum + 1), 2), @Dominio),
                @SinLogin, 'administrador',
                CONCAT(N'Salones Demo ', RIGHT(CONCAT('0', a.AdminNum + 1), 2)), SYSUTCDATETIME())
    OUTPUT a.AdminNum, inserted.Id INTO #Usuarios (AdminNum, Id);

    -- INSERT ... ORDER BY asigna los Id en el orden de I (así "salón siguiente" = i + 1). OUTPUT
    -- guarda solo los Id de esta carga: el k-ésimo menor es el de I = k - 1, aunque otros inserten a la vez.
    DECLARE @Nuevos TABLE (Id INT PRIMARY KEY);
    INSERT INTO Salones (AdminId, Nombre, Zona, Capacidad, Descripcion, Estado, FechaCreacion)
    OUTPUT inserted.Id INTO @Nuevos (Id)
    SELECT u.Id, c.Nombre, c.Zona, c.Capacidad, c.Descripcion, 'publicado', DATEADD(MINUTE, -c.I, SYSUTCDATETIME())
    FROM #Carga c JOIN #Usuarios u ON u.AdminNum = c.I % @Admins
    ORDER BY c.I;

    INSERT INTO #Salones (I, Id)
    SELECT ROW_NUMBER() OVER (ORDER BY Id) - 1, Id FROM @Nuevos;

    INSERT INTO Paquetes (SalonId, NombrePaquete, Descripcion, Precio)
    SELECT s.Id, p.Nombre, p.Descripcion, ROUND(c.PrecioBasico * p.Factor / 100.0, 0) * 100
    FROM #Carga c
    JOIN #Salones s ON s.I = c.I
    JOIN (VALUES (1, N'Paquete Básico',  N'Renta del espacio, mesas y sillas por 6 horas.', 1.0),
                 (2, N'Paquete Plus',    N'Incluye mantelería, decoración básica e iluminación por 8 horas.', 1.8),
                 (3, N'Paquete Premium', N'Incluye banquete, música y coordinación del evento por 10 horas.', 3.2)
         ) p (Orden, Nombre, Descripcion, Factor) ON p.Orden <= c.NumPaquetes
    ORDER BY s.Id, p.Orden;

    -- En orden de Id: la foto 0 de cada salón queda con el menor Id (es la principal).
    INSERT INTO FotosSalon (SalonId, Url)
    SELECT s.Id, CONCAT(@UrlBase, f.Archivo)
    FROM #Carga c
    JOIN #Salones s ON s.I = c.I
    JOIN @Numeros j ON j.N < c.NumFotos
    JOIN @Fotos f ON f.I = (c.FotoBase + j.N * c.FotoPaso) % @F
    ORDER BY s.Id, j.N;

    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    DECLARE @Error NVARCHAR(2048) = ERROR_MESSAGE();
    RAISERROR(N'La carga falló y se deshizo completa (ROLLBACK): %s', 16, 1, @Error);
    RETURN;
END CATCH

SELECT (SELECT COUNT(*) FROM Usuarios WHERE Correo LIKE N'%' + @Dominio) AS Usuarios,
       (SELECT COUNT(*) FROM Salones s JOIN #Usuarios u ON u.Id = s.AdminId) AS Salones,
       (SELECT COUNT(*) FROM Paquetes p JOIN #Salones s ON s.Id = p.SalonId) AS Paquetes,
       (SELECT COUNT(*) FROM FotosSalon f JOIN #Salones s ON s.Id = f.SalonId) AS Fotos;
PRINT N'Carga de demostración lista. Verifica con 17_VerificarCargaMasiva.sql.';
GO
