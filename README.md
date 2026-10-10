# GlamSpaces-Back

API de GlamSpaces en ASP.NET Core (.NET 9) sobre **Azure SQL** (`glamspaces-db`), con
arquitectura limpia por capas y acceso a datos 100% por **stored procedures** (Dapper).
Plataforma para encontrar y reservar salones de eventos.

- Swagger: `/swagger` (abierto en todos los entornos)
- Healthcheck: `/health` → `Healthy` si la API y la BD responden
- Despliegue: GitHub Actions publica en Azure App Service (`glamspaces-api`) con cada push a `main`

## Arquitectura

```
GlamSpaces.sln
├── GlamSpaces.Domain/          Núcleo: no depende de nadie
│   ├── Entities/               Usuario, Salon, Paquete, FotoSalon (= tablas)
│   ├── Dtos/                   Requests y responses del API
│   ├── Interfaces/             Contratos de los repositorios
│   └── Comun/                  RespuestaApi, RespuestaPaginada, CodigosError
├── GlamSpaces.Infrastructure/  Acceso a datos → depende de Domain
│   ├── Repositorios/           Implementan las interfaces llamando stored procedures
│   ├── Datos/                  Helper para ejecutar SP con Dapper
│   └── Seguridad/              PasswordHasher (PBKDF2)
├── GlamSpaces.Api/             Entrada HTTP → depende de Domain e Infrastructure
│   ├── Controllers/            Solo reciben la petición y regresan la respuesta
│   └── Program.cs              Inyección de dependencias, conexión, healthcheck
└── Database/
    ├── 01_Tablas.sql
    └── 02_StoredProcedures.sql Reglas de negocio (validaciones, dueño, publicar, etc.)
```

Decisiones:
- **Usings globales**: cada proyecto tiene un `GlobalUsings.cs`; las clases no llevan `using`.
- **Stored procedures**: toda la lógica de datos y reglas de negocio vive en la BD.
  Cada SP regresa `@Codigo` y `@Mensaje` como OUTPUT.
- **Una conexión por petición**: se abre una sola vez por request y la comparten todos los SP
  de esa petición; el pool de ADO.NET reutiliza las conexiones físicas.
- **Solo POST**: los parámetros viajan en el body (no quedan en la URL ni en logs). Todo sobre HTTPS.
- La contraseña se hashea en la API (PBKDF2, 100,000 iteraciones, salt por usuario); el SP nunca la ve en texto plano.

## Cómo correrla

1. Correr en la BD, en orden: `Database/01_Tablas.sql` y `Database/02_StoredProcedures.sql`
   (ambos se pueden volver a correr sin romper nada).
2. Guardar la cadena de conexión (no se sube a Git):
   ```bash
   cd GlamSpaces.Api
   dotnet user-secrets set "ConnectionStrings:GlamSpacesDb" "Server=tcp:...;Password=...;"
   ```
   En Azure va en App Service → Configuración → Cadenas de conexión → `GlamSpacesDb`.
3. `dotnet run --project GlamSpaces.Api` y abrir `/swagger`.

Tu IP debe estar permitida en el firewall de Azure SQL (Portal → servidor SQL → Redes).

## Formato de respuesta

Todas las respuestas (éxito y error) tienen la misma forma:

```json
{ "codigo": 0, "mensaje": "Consulta exitosa.", "datos": { ... }, "exito": true }
```

Los listados agregan el paginado:

```json
{
  "pagina": 1, "tamanoPagina": 10, "totalRegistros": 2, "totalPaginas": 1,
  "codigo": 0, "mensaje": "Consulta exitosa.", "exito": true,
  "datos": [
    { "id": 2, "nombre": "Terraza Monarca", "zona": "Tepeji del Río, Hgo.", "capacidad": 80, "estado": "no_publicado", "totalPaquetes": 0 },
    { "id": 1, "nombre": "Salón Jardín Encanto", "zona": "Tula de Allende, Hgo.", "capacidad": 150, "estado": "publicado", "totalPaquetes": 2 }
  ]
}
```

### Códigos de error

| Código | HTTP | Significado |
|---|---|---|
| 0 | 200 | Éxito |
| 1001 | 400 | Datos inválidos (campo vacío, precio/capacidad ≤ 0, JSON mal formado) |
| 1002 | 409 | Correo ya registrado |
| 1003 | 401 | Correo o contraseña incorrectos |
| 1004 | 400 | El usuario no existe o no es administrador |
| 1005 | 404 | Usuario no encontrado |
| 2001 | 404 | Salón no encontrado |
| 2002 | 403 | No es el dueño del salón |
| 2003 | 400 | Publicar un salón sin paquetes |
| 2004 | 400 | Estado inválido |
| 3001 | 404 | Paquete no encontrado |
| 3002 | 400 | Borrar el último paquete de un salón publicado |
| 5000 | 500 | Error interno |

## Endpoints (todos POST)

> No hay JWT todavía: el `adminId` (el `id` que regresa el login) se manda en el body.

### Sprint 1 — Usuarios

| Endpoint | Body |
|---|---|
| `/api/usuarios/registro` | `{ nombreCompleto, correo, password, tipoCuenta, nombreSalon? }` |
| `/api/usuarios/login` | `{ correo, password }` |
| `/api/usuarios/obtener` | `{ id }` |

### Sprint 2 — Salones y paquetes (HU-06)

| Endpoint | Body |
|---|---|
| `/api/salones/crear` | `{ adminId, nombre, zona, capacidad, descripcion? }` |
| `/api/salones/obtener` | `{ id }` → salón con `paquetes` y `fotos` |
| `/api/salones/actualizar` | `{ id, adminId, nombre, zona, capacidad, descripcion?, estado? }` |
| `/api/salones/listar` | `{ adminId?, pagina, tamanoPagina }` (máx. 100 por página) |
| `/api/paquetes/crear` | `{ salonId, adminId, nombrePaquete, descripcion?, precio }` |
| `/api/paquetes/actualizar` | `{ id, adminId, nombrePaquete, descripcion?, precio }` |
| `/api/paquetes/eliminar` | `{ id, adminId }` |

Reglas de negocio (en los SP):
- El salón nace `no_publicado`; para publicarlo (`estado: "publicado"`) necesita al menos un paquete.
- Solo el administrador dueño puede editar el salón o sus paquetes.
- No se puede borrar el último paquete de un salón publicado.
- `precio > 0`, `capacidad > 0`; nombre, zona y nombre del paquete obligatorios.
- En `actualizar`, si `descripcion` no se manda se borra; si `estado` no se manda se conserva.

### Fuera de alcance
- Subida de imágenes a almacenamiento externo (`FotosSalon.Url` se asume como URL ya dada).
- Búsqueda pública de salones (HU-09).
- JWT (siguiente paso: sacar el `adminId` del token).
