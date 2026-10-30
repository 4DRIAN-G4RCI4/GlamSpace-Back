# Sprint 2.5 — Cambio completo de la infraestructura del backend

**Fechas:** hasta 2026-10-10 · **Estado:** ✅ en producción · **Quién:** Adrian

## Por qué

El profesor pidió para todos los equipos: DTOs, usings globales, arquitectura por capas, paginación,
números de error, stored procedures y solo `POST`.

## Lo que se hizo

| Antes | Después |
|---|---|
| Entity Framework | **Dapper + stored procedures** (`Database/02_StoredProcedures.sql`, 12 SP) |
| Un proyecto | Capas `Api` → `Domain` ← `Infrastructure` |
| `using` en cada clase | `GlobalUsings.cs` por proyecto |
| GET / PUT / DELETE con parámetros en URL | **Todo `POST`** con body JSON (`/api/<recurso>/<acción>`) |
| Respuestas sueltas | `RespuestaApi` / `RespuestaPaginada` con `codigo`, `mensaje`, `exito`, `datos` |
| Errores por excepción | **Catálogo de códigos** (`CodigosError.cs`): 0, 1001–1005, 2001–2004, 3001–3002, 5000 |
| Contraseñas | PBKDF2 (`PasswordHasher`) |

- Cada SP recibe `@Codigo INT OUTPUT, @Mensaje NVARCHAR(200) OUTPUT`; `ApiControllerBase.Responder`
  traduce el código a HTTP.
- Listados paginados (`pagina`, `tamanoPagina`).
- Los SP se ejecutaron en Azure a mano.

## PRs

| PR | Ramas | Qué |
|---|---|---|
| #14 | Sprint2_Adrian → develop | Cambio completo de la infraestructura |
| #15, #16 | develop → release → main | Infraestructura en producción |

## Impacto en el frontend

Todas las llamadas pasaron a `POST /api/<recurso>/<acción>` y a leer `datos` de la respuesta estándar.
