# Sprint 5 — Solicitudes de reservación

**Fechas:** desde 2026-10-29 · **Estado:** 🚧 en curso

## Historias

| HU | Qué | Quién | Capa | Estado |
|---|---|---|---|---|
| HU-14 | Modelo de solicitudes (tabla `Solicitudes`) | Adrian | BD | ✅ en main |
| HU-15 | Endpoints para crear y consultar solicitudes | Alexis | Backend | ✅ en develop y release (#45) |
| HU-16 | Formulario de solicitud (nombre y teléfono obligatorios) | Erick (front) · Adrian (columna de teléfono) | Frontend / BD | 🚧 BD en main; front en curso |

## HU-14 — Tabla `Solicitudes` (Adrian)

- Se creó a mano en Azure y se versionó en el repo.
- **Migración `M005_CrearSolicitudes`** (`202610291000`): `Up()` idempotente en 4 lotes (tabla, 2 CHECK,
  índice) y `Down()` que borra la tabla.
- **`Database/06_SolicitudesHU14.sql`**: esquema + semilla de 3 solicitudes (pendiente, confirmada,
  rechazada) con el primer salón, su primer paquete y el primer cliente; `THROW` 50001–50003 si faltan.
- Columnas: `Id`, `SalonId` (FK), `ClienteId` (FK Usuarios), `PaqueteId` NULL (FK), `FechaEvento` DATE,
  `TipoEvento` NVARCHAR(100), `NumeroInvitados` (> 0), `Comentarios` NVARCHAR(500) NULL,
  `Estado` (`pendiente` por defecto; `confirmada`, `rechazada`), `FechaCreacion` DATETIME2(7) (`SYSUTCDATETIME()`).
- Índice único filtrado `UX_Solicitudes_Cliente_Salon_Fecha_Pendiente` `(ClienteId, SalonId, FechaEvento) WHERE Estado = 'pendiente'`.
- `scripts/migraciones.ps1` ganó la acción `script -Archivo`. Evidencias `docs/evidencias/10` a `17`.
- PRs #35 → #36 → #37 (main).

## HU-16 — Teléfono de contacto (columna, Adrian)

- La HU-16 pide nombre y teléfono obligatorios; el nombre sale de la sesión (`NombreCompleto`).
- **`Database/07_TelefonoSolicitudesHU16.sql`** (corrido a mano en Azure) y **migración `M006`**
  (`202610291100`): `Solicitudes.TelefonoContacto NVARCHAR(20) NULL`. Es NULL en la BD por las
  solicitudes que ya existían; el API lo exige al crear.
- Evidencias `docs/evidencias/18` a `22`. PRs #38 → #39 → #40 (main).

## HU-15 — Endpoints de solicitudes (Alexis)

- `POST /api/solicitudes/crear`, `/obtener`, `/listar` (paginado, `FechaCreacion DESC`, máximo 100 por página).
- SP `sp_Solicitud_Crear`, `sp_Solicitud_Obtener`, `sp_Solicitud_ListarPorCliente` en la migración
  **`M007_ProcedimientosSolicitudes`** (`202610291200`) y en `Database/08_SolicitudesHU15.sql`.
- Códigos nuevos: `4001` no existe, `4002` fecha no disponible, `4003` no es cliente, `4004` ya hay una
  pendiente, `4005` es de otro cliente.
- `crear` valida cliente, salón publicado, paquete del salón, datos (incluye teléfono), capacidad y
  fecha; usa `UPDLOCK, HOLDLOCK` contra confirmaciones simultáneas.
- PRs #41 → #42 → #43 (main); ajuste del workflow al rollback de la M007: #44 → #45 (release).

## Memoria del proyecto (Adrian)

- `memoria/` con `MEMORIA.md`, `REGLAS.md`, este registro por sprint y `bd/GlamSpaces_BD_Completa.sql`
  generado con `scripts/generar-bd-completa.ps1`.
- El workflow **Migraciones BD** falla si el script completo no está al día y lo ejecuta dos veces sobre
  una BD vacía.
- Hook `.githooks/post-merge`: avisa después de cada `git pull` si cambió la BD.

## Pendientes

- Llevar #45 (ajuste del workflow de la M007) de release a main.
- HU-16 front (Erick): formulario con teléfono obligatorio usando `POST /api/solicitudes/crear`.
- Sonar: rama principal a `main`.
