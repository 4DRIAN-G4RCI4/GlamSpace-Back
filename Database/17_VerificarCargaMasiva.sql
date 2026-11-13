/* =========================================================================
   GlamSpaces — Verifica la carga de DEMOSTRACIÓN de 15_CargaMasivaSalones.sql (Sprint 7)
   Solo lectura (más dos llamadas a sp_Salon_Buscar para medir tiempo). Se corre a mano.
   Cada revisión regresa Resultado = 'OK' o 'FALLA'.
   ========================================================================= */

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

DECLARE @Dominio NVARCHAR(50) = N'@demo.glamspaces.test';

SELECT s.Id, s.Nombre, s.Zona, s.Estado
INTO #S
FROM Salones s JOIN Usuarios u ON u.Id = s.AdminId
WHERE u.Correo LIKE N'%' + @Dominio;

SELECT f.Id, f.SalonId, f.Url,
       ROW_NUMBER() OVER (PARTITION BY f.SalonId ORDER BY f.Id) AS Orden
INTO #F
FROM FotosSalon f JOIN #S s ON s.Id = f.SalonId;

DECLARE @Total INT = (SELECT COUNT(*) FROM #S);
DECLARE @FotosDistintas INT = (SELECT COUNT(DISTINCT Url) FROM #F);

-- Set de fotos de cada salón (ordenado por URL) y su foto principal (menor Id).
SELECT s.Id,
       (SELECT Url FROM #F WHERE SalonId = s.Id AND Orden = 1) AS Principal,
       (SELECT STRING_AGG(Url, '|') WITHIN GROUP (ORDER BY Url) FROM #F WHERE SalonId = s.Id) AS SetFotos,
       LAG(s.Id) OVER (ORDER BY s.Id) AS IdAnterior
INTO #Sets
FROM #S s;

DECLARE @Revisiones TABLE (Revision NVARCHAR(100), Valor NVARCHAR(100), Resultado NVARCHAR(5));

INSERT INTO @Revisiones VALUES
    (N'Salones de demo (esperado 1500)', @Total, IIF(@Total = 1500, 'OK', 'FALLA')),
    (N'Usuarios de demo (esperado 30)',
        (SELECT COUNT(*) FROM Usuarios WHERE Correo LIKE N'%' + @Dominio),
        IIF((SELECT COUNT(*) FROM Usuarios WHERE Correo LIKE N'%' + @Dominio) = 30, 'OK', 'FALLA')),
    (N'Salones no publicados',
        (SELECT COUNT(*) FROM #S WHERE Estado <> 'publicado'),
        IIF(EXISTS (SELECT 1 FROM #S WHERE Estado <> 'publicado'), 'FALLA', 'OK')),
    (N'Salones con 4 fotos', (SELECT COUNT(*) FROM (SELECT SalonId FROM #F GROUP BY SalonId HAVING COUNT(*) = 4) x), 'OK'),
    (N'Salones con 5 fotos', (SELECT COUNT(*) FROM (SELECT SalonId FROM #F GROUP BY SalonId HAVING COUNT(*) = 5) x), 'OK'),
    (N'Salones con otra cantidad de fotos',
        (SELECT COUNT(*) FROM #S s WHERE (SELECT COUNT(*) FROM #F WHERE SalonId = s.Id) NOT IN (4, 5)),
        IIF(EXISTS (SELECT 1 FROM #S s WHERE (SELECT COUNT(*) FROM #F WHERE SalonId = s.Id) NOT IN (4, 5)), 'FALLA', 'OK')),
    (N'Salones con una foto repetida',
        (SELECT COUNT(*) FROM (SELECT SalonId FROM #F GROUP BY SalonId, Url HAVING COUNT(*) > 1) x),
        IIF(EXISTS (SELECT SalonId FROM #F GROUP BY SalonId, Url HAVING COUNT(*) > 1), 'FALLA', 'OK')),
    (N'Salones sin paquete',
        (SELECT COUNT(*) FROM #S s WHERE NOT EXISTS (SELECT 1 FROM Paquetes p WHERE p.SalonId = s.Id)),
        IIF(EXISTS (SELECT 1 FROM #S s WHERE NOT EXISTS (SELECT 1 FROM Paquetes p WHERE p.SalonId = s.Id)), 'FALLA', 'OK')),
    (N'Precios fuera de 3,000 a 45,000',
        (SELECT COUNT(*) FROM Paquetes p JOIN #S s ON s.Id = p.SalonId WHERE p.Precio NOT BETWEEN 3000 AND 45000),
        IIF(EXISTS (SELECT 1 FROM Paquetes p JOIN #S s ON s.Id = p.SalonId WHERE p.Precio NOT BETWEEN 3000 AND 45000), 'FALLA', 'OK')),
    (N'Fotos distintas usadas',  @FotosDistintas, IIF(@FotosDistintas > 0, 'OK', 'FALLA')),
    -- Variedad: la foto principal usa todas las fotos disponibles.
    (N'Fotos principales distintas (esperado = fotos distintas)',
        (SELECT COUNT(DISTINCT Principal) FROM #Sets),
        IIF((SELECT COUNT(DISTINCT Principal) FROM #Sets) = @FotosDistintas, 'OK', 'FALLA')),
    (N'Salones seguidos con la misma principal',
        (SELECT COUNT(*) FROM #Sets a JOIN #Sets b ON b.Id = a.IdAnterior WHERE a.Principal = b.Principal),
        IIF(EXISTS (SELECT 1 FROM #Sets a JOIN #Sets b ON b.Id = a.IdAnterior WHERE a.Principal = b.Principal), 'FALLA', 'OK')),
    (N'Salones seguidos con el mismo set de fotos',
        (SELECT COUNT(*) FROM #Sets a JOIN #Sets b ON b.Id = a.IdAnterior WHERE a.SetFotos = b.SetFotos),
        IIF(EXISTS (SELECT 1 FROM #Sets a JOIN #Sets b ON b.Id = a.IdAnterior WHERE a.SetFotos = b.SetFotos), 'FALLA', 'OK')),
    (N'Nombres repetidos dentro de una misma zona',
        (SELECT COUNT(*) FROM (SELECT Nombre FROM #S GROUP BY Zona, Nombre HAVING COUNT(*) > 1) x),
        IIF(EXISTS (SELECT 1 FROM #S GROUP BY Zona, Nombre HAVING COUNT(*) > 1), 'FALLA', 'OK')),
    (N'Zonas con formato o longitud incorrecta',
        (SELECT COUNT(*) FROM #S WHERE LEN(Zona) > 200
            OR Zona NOT LIKE N'_%, _%, Hgo., CP [0-9][0-9][0-9][0-9][0-9]'
            OR LEN(Zona) - LEN(REPLACE(Zona, ',', '')) <> 3),
        IIF(EXISTS (SELECT 1 FROM #S WHERE LEN(Zona) > 200
            OR Zona NOT LIKE N'_%, _%, Hgo., CP [0-9][0-9][0-9][0-9][0-9]'
            OR LEN(Zona) - LEN(REPLACE(Zona, ',', '')) <> 3), 'FALLA', 'OK')),
    (N'Longitud máxima de Zona (máx. 200)', (SELECT MAX(LEN(Zona)) FROM #S), 'OK');

SELECT * FROM @Revisiones;

-- Distribución por municipio (texto entre la primera coma y ", Hgo.").
SELECT Municipio, COUNT(*) AS Salones, COUNT(DISTINCT Zona) AS Colonias
FROM (SELECT SUBSTRING(Zona, CHARINDEX(',', Zona) + 2,
                       CHARINDEX(N', Hgo.', Zona) - CHARINDEX(',', Zona) - 2) AS Municipio, Zona
      FROM #S) x
GROUP BY Municipio
ORDER BY Salones DESC;

-- Tiempo de la búsqueda pública (18 por página).
DECLARE @Codigo INT, @Mensaje NVARCHAR(200), @Inicio DATETIME2, @SinFiltro INT, @ConTula INT;

SET @Inicio = SYSDATETIME();
EXEC sp_Salon_Buscar @Pagina = 1, @TamanoPagina = 18, @Codigo = @Codigo OUTPUT, @Mensaje = @Mensaje OUTPUT;
SET @SinFiltro = DATEDIFF(MILLISECOND, @Inicio, SYSDATETIME());

SET @Inicio = SYSDATETIME();
EXEC sp_Salon_Buscar @Zona = N'tula', @Pagina = 1, @TamanoPagina = 18, @Codigo = @Codigo OUTPUT, @Mensaje = @Mensaje OUTPUT;
SET @ConTula = DATEDIFF(MILLISECOND, @Inicio, SYSDATETIME());

SELECT @SinFiltro AS MsBuscarSinFiltros, @ConTula AS MsBuscarZonaTula,
       (SELECT COUNT(*) FROM #S WHERE Zona COLLATE Latin1_General_CI_AI LIKE N'%tula%') AS DemoConTula,
       (SELECT COUNT(*) FROM #S WHERE Zona COLLATE Latin1_General_CI_AI LIKE N'%tepeji%') AS DemoConTepeji;
GO
