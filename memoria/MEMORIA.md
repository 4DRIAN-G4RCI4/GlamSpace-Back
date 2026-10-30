# Memoria del proyecto GlamSpaces

Contexto completo y vigente del proyecto. **Léelo antes de trabajar** y actualízalo al terminar cada
HU (ver [`REGLAS.md`](REGLAS.md)). El detalle de cada sprint está en [`sprint/`](sprint/).

**Última actualización:** 2026-10-29 · Sprint 5 en curso.

---

## 1. Qué es

Plataforma para **encontrar y reservar salones de eventos**. Los **clientes** buscan salones, ven su
detalle y disponibilidad y envían solicitudes de reservación; los **administradores** publican su salón
con paquetes.

| Pieza | Tecnología | Repositorio | Producción |
|---|---|---|---|
| Backend (API) | ASP.NET Core .NET 9, Dapper, stored procedures | `4DRIAN-G4RCI4/GlamSpace-Back` (este) | `https://glamspaces-api-g8g9bzemfyh7g3en.mexicocentral-01.azurewebsites.net` (`/swagger`, `/health`) |
| Frontend | React Native / Expo (web) | `GlamSpace-Front` | `https://glamspacesfrontsa.z41.web.core.windows.net` |
| Base de datos | Azure SQL | — | `glamspaces-db` |

## 2. Equipo

| Persona | Rol | Usuario Git |
|---|---|---|
| Adrian Adonai García Jiménez | Backend, BD, calidad (Sonar), versionamiento de BD | `Adrian Garcia` |
| Ángel Alexis Blas Castillo | Backend | `Angel Alexis Blas Castillo` / `AlexisBBY` |
| Erick Trejo Reséndiz | Frontend | — |

## 3. Arquitectura del backend

```
GlamSpaces.Api            Controllers (todos POST) + ApiControllerBase.Responder (código → HTTP) + Program.cs
GlamSpaces.Domain         Entities, Dtos, Interfaces, Comun (RespuestaApi, RespuestaPaginada, CodigosError)
GlamSpaces.Infrastructure Repositorios (Dapper → stored procedures), Datos/StoredProcedure, Seguridad/PasswordHasher (PBKDF2)
GlamSpaces.Migrations     FluentMigrator 8.0.1 (migraciones 001…007)
GlamSpaces.Tests          xUnit + NSubstitute, sin BD (pruebas unitarias y de migraciones)
```

- Flujo: Controller → Repositorio → SP (`@Codigo`, `@Mensaje` OUTPUT) → `RespuestaApi` → HTTP según el código.
- Sin `using` en las clases (`GlobalUsings.cs`). Comentarios en español.
- **Sin JWT todavía:** `adminId` / `clienteId` viajan en el body (limitación conocida).
- CORS: `Cors:OrigenesPermitidos` en `appsettings.json`.

## 4. Base de datos

**Script completo y vigente:** [`bd/GlamSpaces_BD_Completa.sql`](bd/GlamSpaces_BD_Completa.sql) (generado con
`scripts/generar-bd-completa.ps1`, no se edita a mano).

| Tabla | Para qué | Sprint / HU |
|---|---|---|
| `Usuarios` | Clientes y administradores (`TipoCuenta`: `cliente` / `administrador`). En producción la creó EF: `FechaRegistro` sin `DEFAULT`, índice único `IX_Usuarios_Correo`. | 1 |
| `Salones` | Salón del admin; `Estado` `publicado` / `no_publicado`. | 2 · HU-06 |
| `Paquetes` | Paquetes del salón (`Precio > 0`). | 2 · HU-06 |
| `FotosSalon` | Fotos del salón. | 2 |
| `Reservaciones` | Fechas del salón; solo `confirmada` ocupa la fecha (`UX_Reservaciones_Confirmada`). | 4 · HU-11/12 |
| `Solicitudes` | Solicitudes de reservación del cliente: `pendiente` / `confirmada` / `rechazada`, `TelefonoContacto`. Índice único filtrado de pendientes por cliente-salón-fecha. | 5 · HU-14/16 |

**Scripts de `Database/`** (todos idempotentes):

| Script | Contenido | ¿En el script completo? |
|---|---|---|
| `01_Tablas.sql` | Usuarios, Salones, Paquetes, FotosSalon, Reservaciones | Sí |
| `02_StoredProcedures.sql` | 12 SP (usuarios, salones, paquetes, búsqueda, disponibilidad) | Sí |
| `03_Indices.sql` | Índices de búsqueda y de reservaciones | Sí |
| `04_DatosPrueba.sql` | Datos de prueba **solo local** (salones `[Prueba]`, reservaciones nov-2026) | No |
| `05_PruebasHU11.sql` | Pruebas de la HU-11 (transacción + `ROLLBACK`) | No |
| `06_SolicitudesHU14.sql` | Tabla `Solicitudes` + semilla de 3 solicitudes | Sí (sin la semilla) |
| `07_TelefonoSolicitudesHU16.sql` | Columna `TelefonoContacto` | Sí |
| `08_SolicitudesHU15.sql` | 3 SP de solicitudes | Sí |

**Migraciones (FluentMigrator, tabla de historial `VersionInfo`):**

| # | Versión | Qué hace |
|---|---|---|
| 001 | `202610230900` | Línea base: Usuarios (forma EF) + scripts 01, 02 y 03 incrustados. `Down()` vacío a propósito. |
| 002 | `202610230910` | Tabla `BusquedasFavoritas` (práctica del Sprint 4, no está en producción). |
| 003 | `202610230920` | Columna `Salones.Telefono` (práctica, no está en producción). |
| 004 | `202610230930` | Índice único de `BusquedasFavoritas` (práctica, no está en producción). |
| 005 | `202610291000` | HU-14: tabla `Solicitudes` (idempotente). |
| 006 | `202610291100` | HU-16: `Solicitudes.TelefonoContacto` (idempotente). |
| 007 | `202610291200` | HU-15: SP de solicitudes (Alexis). |

Herramientas locales: `.\scripts\migraciones.ps1 listar|subir|rollback|reaplicar|respaldar|reset|consultar|script`
(solo LocalDB; se niega a correr contra Azure). En producción las migraciones **no** se han aplicado; los
cambios se aplicaron a mano con los scripts de `Database/`.

## 5. Endpoints vigentes (todos `POST`, body JSON)

| Endpoint | Body | HU |
|---|---|---|
| `/api/usuarios/registro` | `{ nombreCompleto, correo, password, tipoCuenta, nombreSalon? }` | HU-02/03 |
| `/api/usuarios/login` | `{ correo, password }` | HU-04 |
| `/api/usuarios/obtener` | `{ id }` | — |
| `/api/salones/crear` | `{ adminId, nombre, zona, capacidad, descripcion? }` | HU-06 |
| `/api/salones/obtener` | `{ id }` | HU-06/12 |
| `/api/salones/actualizar` | `{ id, adminId, nombre, zona, capacidad, descripcion?, estado? }` | HU-06 |
| `/api/salones/listar` | `{ adminId?, pagina, tamanoPagina }` | HU-06 |
| `/api/salones/buscar` | `{ zona?, capacidadMinima?, precioMaximo?, pagina, tamanoPagina }` | HU-09 |
| `/api/salones/disponibilidad` | `{ salonId, anio, mes }` → fechas confirmadas `yyyy-MM-dd` | HU-12 |
| `/api/paquetes/crear` | `{ salonId, adminId, nombrePaquete, descripcion?, precio }` | HU-06 |
| `/api/paquetes/actualizar` | `{ id, adminId, nombrePaquete, descripcion?, precio }` | HU-06 |
| `/api/paquetes/eliminar` | `{ id, adminId }` | HU-06 |
| `/api/solicitudes/crear` | `{ clienteId, salonId, paqueteId?, fechaEvento, tipoEvento, numeroInvitados, telefonoContacto, comentarios? }` | HU-15 |
| `/api/solicitudes/obtener` | `{ id, clienteId }` | HU-15 |
| `/api/solicitudes/listar` | `{ clienteId, pagina, tamanoPagina }` | HU-15 |

Respuesta: `{ codigo, mensaje, exito, datos }`; las paginadas agregan `pagina, tamanoPagina, totalRegistros, totalPaginas`.

## 6. Catálogo de códigos de error

| Código | HTTP | Significado |
|---|---|---|
| 0 | 200 | Éxito |
| 1001 | 400 | Datos inválidos |
| 1002 | 409 | Correo ya registrado |
| 1003 | 401 | Credenciales incorrectas |
| 1004 | 400 | No existe o no es administrador |
| 1005 | 404 | Usuario no encontrado |
| 2001 | 404 | Salón no encontrado (o no publicado, en solicitudes) |
| 2002 | 403 | No es el dueño del salón |
| 2003 | 400 | Publicar sin paquetes |
| 2004 | 400 | Estado inválido |
| 3001 | 404 | Paquete no encontrado |
| 3002 | 400 | Borrar el último paquete de un salón publicado |
| 4001 | 404 | Solicitud no encontrada |
| 4002 | 409 | Fecha no disponible (el salón tiene una solicitud confirmada ese día) |
| 4003 | 400 | El usuario no es cliente |
| 4004 | 409 | Ya hay una solicitud pendiente para ese salón y fecha |
| 4005 | 403 | La solicitud es de otro cliente |
| 5000 | 500 | Error interno |

## 7. Calidad, pruebas y CI/CD

| Workflow | Cuándo | Qué hace |
|---|---|---|
| `sonar.yml` — SonarQube Cloud | push a develop/main/Sprint4_Adrian y PR | Compila, corre pruebas con cobertura OpenCover y evalúa el Quality Gate ("Sonar way": cobertura de código nuevo ≥ 80 %). Proyecto `4DRIAN-G4RCI4_GlamSpace-Back`, org `4drian-g4rci4`. |
| `migraciones.yml` — Migraciones BD | push a develop/main y PR a develop/main | Verifica que `memoria/bd` esté al día; SQL Server 2022 en contenedor; migraciones desde cero, scripts 04/06/07, rollback de la última y reaplicación; script completo dos veces. |
| `main_glamspaces-api.yml` | push a main | Despliega en Azure App Service. |

Pruebas: `dotnet test GlamSpaces.sln` (sin BD). Documentos: `docs/CALIDAD.md`, `docs/CALIDAD_SONAR.md`,
`docs/VERSIONAMIENTO_BD.md`, evidencias en `docs/evidencias/`.

## 8. Flujo de trabajo

Ramas `SprintN_Nombre` → PR a `develop` → `release` → `main` (merge commit). Commits `RE_SprintN: ...` sin
co-autoría de IA. Detalle en [`REGLAS.md`](REGLAS.md).

## 9. Pendientes conocidos

- **JWT:** tomar `adminId` / `clienteId` del token en lugar del body.
- **Sonar:** cambiar la rama principal del proyecto a `main` y revisar su Quality Gate; luego cerrar la
  sección 9 de `docs/CALIDAD_SONAR.md`.
- **Producción:** decidir si se aplican las migraciones con FluentMigrator en Azure (hoy se usa el script a mano).
- **HU-16 (Erick, front):** formulario de solicitud con nombre (de la sesión) y teléfono obligatorio.
- Revocar el token de Sonar que quedó expuesto en un chat y borrar el proyecto huérfano en minúsculas.

## 10. Bitácora

| Fecha | Quién | Qué |
|---|---|---|
| 2026-10-02 | Alexis | Sprint 1: registro y login, conexión a Azure SQL, Swagger (PR #1–#10). |
| 2026-10-09 | Adrian | Sprint 2: HU-06 CRUD de salones y paquetes (PR #11–#13). |
| 2026-10-10 | Adrian | Sprint 2.5: reestructura del backend (PR #14–#16). Sprint 3: HU-08 y HU-09 (PR #17–#19). |
| 2026-10-22 | Alexis | Sprint 4: HU-11 tabla Reservaciones y HU-12 disponibilidad (PR #20–#22). |
| 2026-10-22 | Adrian | Sprint 4: HU-11 verificada y documentada; SonarQube Cloud (PR #23–#28). |
| 2026-10-24 | Adrian | Sprint 4: FluentMigrator (PR #29–#31) y pruebas unitarias con cobertura (PR #32–#34). |
| 2026-10-29 | Adrian | Sprint 5: HU-14 tabla Solicitudes (PR #35–#37) y HU-16 teléfono de contacto (PR #38–#40). |
| 2026-10-29 | Alexis | Sprint 5: HU-15 endpoints de solicitudes y M007 (PR #41–#45). |
| 2026-10-29 | Adrian | Memoria del proyecto, reglas, script completo generado y verificación en el workflow. |
