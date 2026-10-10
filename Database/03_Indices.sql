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
