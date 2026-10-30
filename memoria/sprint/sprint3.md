# Sprint 3 — Búsqueda y filtrado de salones

**Fechas:** hasta 2026-10-10 · **Estado:** ✅ en producción

## Historias

| HU | Qué | Quién | Capa |
|---|---|---|---|
| HU-08 | Índices y consultas para la búsqueda | Adrian | BD |
| HU-09 | Endpoint de búsqueda y filtrado | Adrian | Backend |
| HU-10 | Pantalla de búsqueda | Adrian (por confirmar con Erick) | Frontend |

## Lo que se hizo

- **HU-09:** `POST /api/salones/buscar` `{ zona?, capacidadMinima?, precioMaximo?, pagina, tamanoPagina }` →
  `{ id, nombre, zona, capacidad, precioDesde, fotoPrincipal }`. `sp_Salon_Buscar`: solo publicados,
  "precio desde" = paquete más barato, foto principal = primera foto, filtros combinables.
- **HU-08:** `Database/03_Indices.sql`: `IX_Salones_Publicados_Capacidad_Zona` (filtrado por publicados),
  `IX_Paquetes_SalonId_Precio`, `IX_FotosSalon_SalonId`. Prueba de carga con 50 000 salones: búsquedas
  filtradas de 5 a 9 veces más rápidas.
- `Database/04_DatosPrueba.sql`: 6 salones `[Prueba]` (solo local). Las fechas van explícitas porque en
  Azure `Usuarios` no tiene `DEFAULT`.
- Los índices filtrados exigen `QUOTED_IDENTIFIER ON`: los SP se recrearon con esa opción.
- **HU-10 (front):** barra por zona, chips de Zona/Capacidad/Precio, cuadrícula responsiva, esqueletos de
  carga, estado vacío y "Cargar más"; detalle del salón; accesibilidad AA revisada con axe-core.

## PRs

| PR | Ramas | Qué |
|---|---|---|
| #17 | Sprint3_Adrian → develop | HU-08 y HU-09 |
| #18, #19 | develop → release → main | Sprint 3 en producción |
| Front #11–#16 | rama Sprint3 | Adaptación a la API nueva, HU-10 y rediseño |

## Base de datos

- `02_StoredProcedures.sql` (+ `sp_Salon_Buscar`), `03_Indices.sql`, `04_DatosPrueba.sql`.
