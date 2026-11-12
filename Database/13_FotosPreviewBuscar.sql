/* =========================================================================
   GlamSpaces — Fotos de vista previa en la búsqueda (HU-07 (fotos), Sprint 7)
   Correr DESPUÉS de 12_FotosSalonHU07.sql. Idempotente (CREATE OR ALTER).

   La tarjeta de salón del front muestra un carrusel al pasar el mouse. Para eso sp_Salon_Buscar
   regresa un TERCER result set con hasta 5 fotos por cada salón de la página (en el mismo orden
   que `fotos` del detalle: por Id de foto). Los dos primeros result sets (paginado y página) y
   FotoPrincipal quedan EXACTAMENTE igual que antes, así que nada se rompe:
     - API vieja + este script: el tercer result set se ignora.
     - API nueva + SP viejo: no hay tercer result set y fotosPreview va vacío.
   Una sola consulta por página (sin N+1); usa IX_FotosSalon_SalonId (03_Indices.sql).
   La tabla FotosSalon no cambia.
   ========================================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- Búsqueda pública: solo salones publicados. Todos los filtros son opcionales y combinables;
-- NULL, vacío o <= 0 significa "sin filtro". Nunca regresa error: sin resultados es éxito con lista vacía.
-- Zona: coincidencia parcial sin importar mayúsculas ni acentos ("rio" encuentra "Tepeji del Río").
-- PrecioMaximo: el salón entra si su paquete más barato (PrecioDesde) cuesta eso o menos.
-- Result sets: 1) datos del paginado, 2) salones de la página, 3) hasta 5 fotos por salón de la página
-- (SalonId, Url) en orden de Id de foto.
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

    -- La página se guarda una vez para usarla en el result set 2 y en el 3 con el mismo orden.
    SELECT Id, Nombre, Zona, Capacidad, PrecioDesde, FotoPrincipal
    INTO #Pagina
    FROM #Resultados
    ORDER BY PrecioDesde, Id
    OFFSET (@Pagina - 1) * @TamanoPagina ROWS
    FETCH NEXT @TamanoPagina ROWS ONLY;

    SELECT @Codigo = 0,
           @Mensaje = CASE WHEN @Total = 0 THEN N'No se encontraron salones con esos filtros.'
                           ELSE N'Consulta exitosa.' END;

    SELECT @Total AS TotalRegistros,
           (@Total + @TamanoPagina - 1) / @TamanoPagina AS TotalPaginas,
           @Pagina AS Pagina,
           @TamanoPagina AS TamanoPagina;

    SELECT Id, Nombre, Zona, Capacidad, PrecioDesde, FotoPrincipal
    FROM #Pagina
    ORDER BY PrecioDesde, Id;

    SELECT x.SalonId, x.Url
    FROM (SELECT f.SalonId, f.Url,
                 ROW_NUMBER() OVER (PARTITION BY f.SalonId ORDER BY f.Id) AS Orden
          FROM FotosSalon f
          INNER JOIN #Pagina pg ON pg.Id = f.SalonId) x
    WHERE x.Orden <= 5
    ORDER BY x.SalonId, x.Orden;
END
GO
