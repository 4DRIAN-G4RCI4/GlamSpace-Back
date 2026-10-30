# Sprint 1 — Registro e inicio de sesión

**Fechas:** hasta 2026-10-02 · **Estado:** ✅ en producción (después reestructurado en el Sprint 2.5)

## Historias

| HU | Qué | Quién | Capa |
|---|---|---|---|
| HU-02 | Registro de cliente | Alexis (back) | Backend / Frontend |
| HU-03 | Registro de administrador de salón (con nombre del salón) | Alexis (back) | Backend / Frontend |
| HU-04 | Inicio de sesión (regresa el usuario con su `id`) | Alexis (back) | Backend / Frontend |

## Lo que se hizo (backend)

- API en ASP.NET Core con Entity Framework: `POST /api/usuarios/registro`, `POST /api/usuarios/login`,
  `GET /api/usuarios/{id}` (rutas originales; las vigentes están en el Sprint 2.5).
- Actualización a **.NET 9**, conexión a **Azure SQL** (`glamspaces-db`) y **Swagger** en todos los entornos.
- Tabla `Usuarios` creada por EF (`EnsureCreated`): sin `DEFAULT` en `FechaRegistro` e índice único
  `IX_Usuarios_Correo`. Esta forma se respeta en la migración 001.
- Despliegue con GitHub Actions a Azure App Service.

## PRs

| PR | Ramas | Qué |
|---|---|---|
| #1 | Sprint1_Alexis → develop | API de registro y login |
| #2, #3 | develop → release → main | Primera versión estable (1.0) |
| #5–#7 | Sprint1_Alexis → … → main | Conexión a Azure SQL (1.1) |
| #8–#10 | Sprint1_Alexis → … → main | Swagger en producción (1.2) |

## Base de datos

- Tabla `Usuarios`. Sin scripts en `Database/` todavía (la creó EF).

## Pendientes que dejó

- Contraseñas: se reforzó con PBKDF2 en el Sprint 2.5.
- Sin JWT (sigue pendiente).
