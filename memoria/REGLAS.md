# Reglas del proyecto GlamSpaces

Aplican a **todo el equipo y a cualquier IA** (Claude Code, OpenCode, Copilot, etc.) que trabaje en este
repositorio. `CLAUDE.md` y `AGENTS.md` en la raíz apuntan aquí.

---

## Regla 0 — Antes de empezar a trabajar

1. `git pull origin develop` en tu rama.
2. Lee [`MEMORIA.md`](MEMORIA.md) (estado general) y el archivo del sprint actual en [`sprint/`](sprint/).
3. Activa el aviso automático **una sola vez por clon**:

   ```bash
   git config core.hooksPath .githooks
   ```

   Después de cada `git pull` te dirá si cambió la BD y qué tienes que correr.

## Regla 1 — Cada vez que descargues cambios (`git pull`)

Si cambió algo en `Database/` o en `GlamSpaces.Migrations/Migraciones/`:

```powershell
.\scripts\migraciones.ps1 subir           # aplica las migraciones nuevas en tu LocalDB
.\scripts\generar-bd-completa.ps1         # debe dejar memoria/bd sin cambios (git status limpio)
```

Si `generar-bd-completa.ps1` cambia `memoria/bd/GlamSpaces_BD_Completa.sql`, quien subió el cambio de BD
olvidó regenerarlo. Avísale o súbelo tú en tu rama.

## Regla 2 — Cada cambio a la base de datos

Un cambio de esquema (tabla, columna, índice, restricción, stored procedure) **siempre** lleva todo esto,
en el mismo PR:

| # | Qué | Dónde | Cómo |
|---|---|---|---|
| a | Script SQL | `Database/NN_Descripcion.sql` (siguiente número) | Idempotente: `IF NOT EXISTS`, `COL_LENGTH(...) IS NULL`, `CREATE OR ALTER`. Si tiene semilla, que haga `THROW` cuando falten datos. |
| b | Migración | `GlamSpaces.Migrations/Migraciones/NNN_Descripcion.cs` | Clase `MNNN_...`, versión `yyyyMMddHHmm` mayor que la anterior. `Up()` idempotente (producción puede tener ya el cambio) y `Down()` real. |
| c | Pruebas | `GlamSpaces.Tests/Migrations/MigracionesPruebas.cs` | Agregar la versión al `[Theory]` y pruebas de Up y Down (SQL generado, sin BD). Todas en verde. |
| d | Script completo | `scripts/generar-bd-completa.ps1` | Agregar el script nuevo a `$Scripts` (si cambia el esquema) y regenerar `memoria/bd/GlamSpaces_BD_Completa.sql`. **El workflow falla si no está al día.** |
| e | Workflow | `.github/workflows/migraciones.yml` | El paso tras `rollback by 1` verifica **la última migración**: ajustarlo a la nueva. |
| f | Evidencias | `docs/evidencias/NN_...txt` (siguiente número) | Salida real de `scripts/migraciones.ps1` en LocalDB: listar → subir → verificar → rollback → reaplicar. |
| g | Documentación | `docs/VERSIONAMIENTO_BD.md` y `README.md` | Fila en la tabla de migraciones y sección de la HU. |
| h | Memoria | `memoria/sprint/sprintN.md` y `memoria/MEMORIA.md` | Ver regla 3. |

**Producción (Azure, `glamspaces-db`):** la IA **nunca** ejecuta nada contra Azure. El script de
`Database/` lo corre Adrian a mano y lo confirma. Todo lo demás se prueba en LocalDB o en el contenedor
del workflow.

## Regla 3 — Al terminar cada HU o cambio

1. En `memoria/sprint/sprintN.md`: agrega la HU con **qué se hizo, quién, rama, PR, scripts y
   migraciones, endpoints y códigos nuevos, y pendientes**.
2. En `memoria/MEMORIA.md`: actualiza solo lo que cambió del estado general (tablas, migraciones,
   endpoints, códigos de error, pendientes) y agrega una línea en la **bitácora**.
3. Si empieza un sprint nuevo, crea `memoria/sprint/sprintN.md` con la misma estructura.

## Regla 4 — Git y GitHub

- Ramas: `SprintN_Nombre` desde `develop` → PR a `develop` → `release` → `main` (siempre *merge commit*,
  nunca squash ni rebase). No borrar ramas al mergear.
- Commits: `RE_SprintN: <mensaje en español>`, a nombre de su autor, **sin** `Co-Authored-By` ni
  menciones a una IA, ni en commits ni en PRs.
- Push y merge solo cuando el dueño de la HU lo pida.
- Antes de mergear: workflows **SonarQube Cloud** y **Migraciones BD** en verde (Quality Gate OK).

## Regla 5 — Seguridad

- Nunca credenciales ni cadenas de conexión reales en el repo; la del workflow vive en el secret
  `MSSQL_SA_PASSWORD`.
- No subir `GlamSpaces.Api/Properties/` (configuración local), respaldos `*.bak` ni `respaldos/`.
- No ocultar hallazgos de Sonar con exclusiones, `//NOSONAR` ni falsos positivos sin justificación técnica.

## Regla 6 — Código

- Capas: `Api` (controllers) → `Domain` (entidades, DTOs, interfaces, `Comun`) ← `Infrastructure`
  (repositorios Dapper que llaman stored procedures).
- Sin `using` en las clases: van en `GlobalUsings.cs` de cada proyecto.
- Todos los endpoints son `POST` con body JSON y responden `RespuestaApi` / `RespuestaPaginada`.
- Todo SP recibe `@Codigo INT OUTPUT, @Mensaje NVARCHAR(200) OUTPUT`; los códigos viven en
  `GlamSpaces.Domain/Comun/CodigosError.cs` y en el catálogo de `MEMORIA.md`.
- Comentarios y mensajes en español.
