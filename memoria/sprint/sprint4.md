# Sprint 4 — Disponibilidad del salón, calidad y versionamiento de la BD

**Fechas:** 2026-10-22 a 2026-10-24 · **Estado:** ✅ en producción

## Historias

| HU | Qué | Quién | Capa |
|---|---|---|---|
| HU-11 | Modelo de calendario de disponibilidad (tabla `Reservaciones`) | Alexis (modelo) · Adrian (verificación y documentación) | BD |
| HU-12 | Endpoint de detalle y disponibilidad del salón | Alexis | Backend |
| HU-13 | Pantalla de detalle y calendario | Erick | Frontend |
| Actividad | Calidad con SonarQube Cloud (Parte 1) y versionamiento de BD (Parte 2) | Adrian | Calidad / BD |

## Lo que se hizo

### HU-11 y HU-12
- Tabla **`Reservaciones`** (`01_Tablas.sql`): `Estado` `pendiente` / `confirmada` / `cancelada`.
  Índices `IX_Reservaciones_Salon_Fecha` y **`UX_Reservaciones_Confirmada`** (único filtrado: un solo
  día confirmado por salón).
- `sp_Salon_Disponibilidad` y `POST /api/salones/disponibilidad` `{ salonId, anio, mes }` →
  fechas confirmadas del mes (`yyyy-MM-dd`).
- `Database/05_PruebasHU11.sql`: criterios de la HU-11 en transacción con `ROLLBACK` (6 de 6).

### HU-13 (front)
- Detalle con galería, paquetes y calendario mensual (ocupado / pasado / elegido); botón
  **Solicitar reservación** (el envío llegó en el Sprint 5).

### SonarQube Cloud (Parte 1)
- Workflow `.github/workflows/sonar.yml` (Java 21, `dotnet-sonarscanner`), proyecto
  `4DRIAN-G4RCI4_GlamSpace-Back`. Hallazgos corregidos y documentados en `docs/CALIDAD.md` y
  `docs/CALIDAD_SONAR.md`.

### Versionamiento de BD con FluentMigrator (Parte 2)
- Proyecto `GlamSpaces.Migrations` y tool local `dotnet-fm` (`.config/dotnet-tools.json`).
- Migraciones 001 (línea base) a 004 (002–004 de práctica: `BusquedasFavoritas`, `Salones.Telefono` e índice).
- `scripts/migraciones.ps1` (solo LocalDB), workflow `migraciones.yml` con SQL Server 2022 en contenedor.
- Evidencias reales `docs/evidencias/01` a `09` y `docs/VERSIONAMIENTO_BD.md`.

### Pruebas unitarias
- `GlamSpaces.Tests` (xUnit, NSubstitute): 77 pruebas sin BD y cobertura OpenCover para el Quality Gate.

### Front
- Corrección del botón Atrás del navegador (history `pushState` / `popstate`).

## PRs

| PR | Ramas | Qué |
|---|---|---|
| #20–#22 | Sprint4_Alexis → … → main | HU-11 Reservaciones y HU-12 disponibilidad (Release 1.3) |
| #23, #27, #28 | Sprint4_Adrian → … → main | SonarQube Cloud |
| #24–#26 | Sprint4_Adrian_HU11 → … → main | HU-11 verificada y documentada |
| #29–#31 | Sprint4_Adrian → … → main | Sonar + FluentMigrator |
| #32–#34 | Sprint4_Pruebas → … → main | Pruebas unitarias con cobertura |

## Pendientes que dejó

- Rama principal de Sonar: hoy `Sprint4_Adrian`; pasar a `main` y revisar el Quality Gate.
- Pruebas de integración contra el contenedor SQL (pospuestas).
