# GlamSpaces-Back

API de GlamSpaces en ASP.NET Core (.NET 9) sobre **Azure SQL** (`glamspaces-db`).
Plataforma para encontrar y reservar salones de eventos.

- Swagger: `/swagger` (abierto en todos los entornos)
- Healthcheck: `/health` → `Healthy` si la API y la BD responden
- Despliegue: GitHub Actions publica en Azure App Service (`glamspaces-api`) con cada push a `main`

> **Estado actual:** la arquitectura y los endpoints vigentes son los del **[Sprint 2.5](#sprint-25--cambio-completo-de-la-infraestructura-del-backend)**,
> más el **Sprint 3**: [búsqueda y filtrado de salones (HU-09)](#sprint-3--búsqueda-y-filtrado-de-salones-hu-09)
> y [índices para la búsqueda (HU-08)](#sprint-3--índices-y-consultas-para-búsqueda-de-salones-hu-08).
> Las secciones de Sprint 1 y Sprint 2 se conservan como historial.

| Sprint | Historias | Estado |
|---|---|---|
| Sprint 1 | HU-02, HU-03, HU-04: registro e inicio de sesión | ✅ En producción |
| Sprint 2 | HU-06: CRUD de salones y paquetes | ✅ En producción |
| Sprint 2.5 | Cambio completo de la infraestructura del backend | ✅ En producción |
| Sprint 3 | HU-08: índices · HU-09: búsqueda y filtrado | ✅ En producción |

## Cómo correrla

1. Correr en la BD, en orden:
   - `Database/01_Tablas.sql`
   - `Database/02_StoredProcedures.sql`
   - `Database/03_Indices.sql`

   Todos se pueden volver a correr sin romper nada.
   `Database/04_DatosPrueba.sql` es **solo para bases locales o de prueba**: carga 6 salones de ejemplo.

   > En `glamspaces-db` las tablas las creó Entity Framework en el Sprint 1, no `01_Tablas.sql`,
   > así que **no tienen valores por defecto** (`FechaRegistro`, `FechaCreacion`, `Estado`).
   > Por eso los SP y los scripts siempre mandan esos valores explícitamente.
2. Guardar la cadena de conexión (no se sube a Git porque trae contraseña):
   ```bash
   cd GlamSpaces.Api
   dotnet user-secrets set "ConnectionStrings:GlamSpacesDb" "Server=tcp:...;Password=...;"
   ```
   En Azure va en App Service → Configuración → Cadenas de conexión → `GlamSpacesDb`.
3. Abrir `GlamSpaces.sln` en Visual Studio, o correr `dotnet run --project GlamSpaces.Api`, y abrir `/swagger`.

Tu IP debe estar permitida en el firewall de Azure SQL (Portal → servidor SQL → Redes).

---

## Sprint 1 — Registro e inicio de sesión

> Historial. Los endpoints vigentes están en el Sprint 2.5.

| HU | Endpoint | Descripción |
|----|----------|-------------|
| HU-02 / HU-03 | `POST /api/usuarios/registro` | Registro de cliente o administrador de salón |
| HU-04 | `POST /api/usuarios/login` | Inicio de sesión (regresa el usuario con su `id`) |
| — | `GET /api/usuarios/{id}` | Consulta de usuario |

---

## Sprint 2 — CRUD de salones y paquetes (HU-06)

> Historial. Los endpoints vigentes están en el Sprint 2.5.

Permite al administrador crear, consultar, actualizar y eliminar su salón y sus paquetes.

| Método | Endpoint | Descripción |
|--------|----------|-------------|
| `POST` | `/api/salones` | Crea un salón (siempre nace `no_publicado`) |
| `GET` | `/api/salones/{id}` | Detalle del salón con sus paquetes y fotos |
| `PUT` | `/api/salones/{id}` | Actualiza el salón; con `estado: "publicado"` lo publica |
| `POST` | `/api/salones/{id}/paquetes` | Agrega un paquete al salón |
| `PUT` | `/api/paquetes/{id}` | Actualiza un paquete |
| `DELETE` | `/api/paquetes/{id}?adminId={adminId}` | Elimina un paquete |

### Reglas de negocio

- Solo el administrador dueño del salón puede editarlo o modificar sus paquetes → si no, `403`.
- No se puede publicar un salón sin al menos un paquete → `400`.
- No se puede borrar el último paquete de un salón publicado → `400`.
- `precio > 0` y `capacidad > 0`; nombre, zona y nombre del paquete obligatorios → `400`.
- Errores con formato `{ "mensaje": "..." }`.

### Identificación del administrador

Todavía no hay JWT. El frontend manda el `adminId` (el `id` que regresó `/api/usuarios/login`)
en el body de POST/PUT y en la URL del DELETE. Cuando se implemente JWT se sacará del token.

### Ejemplos

Crear salón:

```json
POST /api/salones
{
  "adminId": 1,
  "nombre": "Salón Jardín Encanto",
  "zona": "Tula de Allende, Hgo.",
  "capacidad": 150,
  "descripcion": "Salón con jardín exterior."
}
```

Agregar paquete:

```json
POST /api/salones/1/paquetes
{
  "adminId": 1,
  "nombrePaquete": "Paquete Básico",
  "descripcion": "Salón, mesas y sillas por 6 horas.",
  "precio": 8500
}
```

Publicar salón (mismo body que crear, más `estado`):

```json
PUT /api/salones/1
{
  "adminId": 1,
  "nombre": "Salón Jardín Encanto",
  "zona": "Tula de Allende, Hgo.",
  "capacidad": 150,
  "descripcion": "Salón con jardín exterior.",
  "estado": "publicado"
}
```

### Fuera de alcance

- Subida de imágenes a almacenamiento externo (`FotosSalon.Url` se asume como URL ya dada).
- Búsqueda pública de salones (HU-09).
- Validaciones de formato en pantalla (HU-07, frontend).

---

## Sprint 2.5 — Cambio completo de la infraestructura del backend

Se reestructuró todo el backend para cumplir los lineamientos del profesor:

- arquitectura limpia por capas;
- DTOs;
- usings globales;
- stored procedures;
- paginado;
- códigos de error;
- solo endpoints POST.

La funcionalidad de los Sprints 1 y 2 se conserva igual (registro, login, CRUD de salones y paquetes, mismas reglas de negocio). Lo que cambió es **cómo está construida**.

### Antes vs. después

| Tema | Antes (Sprint 1 y 2) | Ahora (Sprint 2.5) |
|---|---|---|
| Estructura | 1 proyecto (`GlamSpaces.Api`) con carpetas | 3 proyectos: **Domain**, **Infrastructure**, **Api** |
| Acceso a datos | Entity Framework Core (LINQ) | **Stored procedures** llamados con Dapper |
| Reglas de negocio | En los controllers (C#) | En los **stored procedures** (la carga queda en la BD) |
| `using` | En cada archivo | **`GlobalUsings.cs`** por proyecto; las clases no llevan `using` |
| Métodos HTTP | GET, POST, PUT, DELETE | **Solo POST** (los datos viajan en el body) |
| Formato de respuesta | Distinto en cada endpoint | **Uno solo**: `{ codigo, mensaje, datos, exito }` |
| Errores | Solo código HTTP + `mensaje` | **Número de error propio** (catálogo `CodigosError`) + código HTTP |
| Listados | No había | **Paginado** con total de registros y de páginas |
| Conexión a BD | La manejaba EF | **Una conexión por petición**, compartida por todos los SP de esa petición |

### Arquitectura por capas

```
GlamSpaces.sln
├── GlamSpaces.Domain/          Núcleo. No depende de ningún otro proyecto.
│   ├── Entities/               Usuario, Salon, Paquete, FotoSalon (= tablas de la BD)
│   ├── Dtos/                   Requests y responses (lo que entra y sale del API)
│   ├── Interfaces/             Contratos de los repositorios (IUsuarioRepositorio, ...)
│   └── Comun/                  RespuestaApi, RespuestaPaginada, CodigosError
│
├── GlamSpaces.Infrastructure/  Acceso a datos. Depende de Domain.
│   ├── Repositorios/           Implementan las interfaces llamando stored procedures
│   ├── Datos/                  Helper para ejecutar SP con Dapper (@Codigo / @Mensaje)
│   └── Seguridad/              PasswordHasher (PBKDF2)
│
├── GlamSpaces.Api/             Entrada HTTP. Depende de Domain e Infrastructure.
│   ├── Controllers/            Reciben la petición y regresan la respuesta (sin lógica)
│   └── Program.cs              Inyección de dependencias, conexión, errores, healthcheck
│
└── Database/
    ├── 01_Tablas.sql           Usuarios, Salones, Paquetes, FotosSalon
    └── 02_StoredProcedures.sql Los 10 stored procedures con las reglas de negocio
```

**Por qué así:** cada capa tiene una sola responsabilidad.
- **Domain** define *qué* datos existen y *qué* operaciones se pueden hacer (interfaces), sin saber nada de SQL ni de HTTP.
- **Infrastructure** decide *cómo* se guardan los datos (stored procedures en Azure SQL). Si mañana cambia la base de datos, solo se toca esta capa.
- **Api** solo expone los endpoints.

Los controllers dependen de las **interfaces** de Domain, no de las clases concretas. Las implementaciones se inyectan en `Program.cs` (inversión de dependencias).

### Flujo de una petición

Ejemplo: `POST /api/salones/crear`

1. **Api**: `SalonesController.Crear` recibe el JSON como `SalonRequest` (DTO de Domain).
2. El controller llama a `ISalonRepositorio.Crear(...)`. No sabe que detrás hay SQL.
3. **Infrastructure**: `SalonRepositorio` ejecuta `sp_Salon_Crear` con Dapper usando la conexión de esa petición.
4. **Base de datos**: el SP valida (nombre, zona, capacidad > 0, que el usuario sea administrador), inserta el registro y regresa `@Codigo`, `@Mensaje` y el salón creado.
5. El repositorio convierte la entidad `Salon` en `SalonResponse` y arma la `RespuestaApi`.
6. **Api**: `ApiControllerBase.Responder` traduce el `codigo` al status HTTP (0 → 200, 2002 → 403, etc.) y regresa el JSON.

### Stored procedures

Todos están en `Database/02_StoredProcedures.sql`. Todos siguen la misma convención:
- reciben `@Codigo INT OUTPUT` y `@Mensaje NVARCHAR(200) OUTPUT`;
- `@Codigo = 0` es éxito; si hay error, ponen el código y hacen `RETURN` sin modificar nada;
- usan `CREATE OR ALTER`, así que el script se puede volver a correr.

| SP | Endpoint | Reglas que valida |
|---|---|---|
| `sp_Usuario_Registrar` | `/api/usuarios/registro` | Nombre, formato de correo, tipo de cuenta, nombre de salón si es administrador, correo duplicado |
| `sp_Usuario_ObtenerPorCorreo` | `/api/usuarios/login` | Regresa el hash; la API verifica la contraseña |
| `sp_Usuario_Obtener` | `/api/usuarios/obtener` | Que el usuario exista |
| `sp_Salon_Crear` | `/api/salones/crear` | Campos obligatorios, capacidad > 0, que sea administrador; nace `no_publicado` |
| `sp_Salon_Obtener` | `/api/salones/obtener` | Regresa 3 resultados: salón, paquetes y fotos |
| `sp_Salon_Actualizar` | `/api/salones/actualizar` | Que exista, que sea el dueño, estado válido, **no publicar sin paquetes** |
| `sp_Salon_Listar` | `/api/salones/listar` | Paginado con `OFFSET/FETCH`, máximo 100 por página |
| `sp_Paquete_Crear` | `/api/paquetes/crear` | Que el salón exista, que sea el dueño, nombre obligatorio, precio > 0 |
| `sp_Paquete_Actualizar` | `/api/paquetes/actualizar` | Que exista, que sea el dueño, nombre obligatorio, precio > 0 |
| `sp_Paquete_Eliminar` | `/api/paquetes/eliminar` | Que exista, que sea el dueño, **no borrar el último paquete de un salón publicado** |

La contraseña es la única regla que se queda en C#: la API valida que tenga al menos 8 caracteres y la hashea con PBKDF2 (100,000 iteraciones, salt por usuario). El SP solo recibe el hash, nunca la contraseña en texto plano.

### Conexión a la base de datos

En `Program.cs` se registra **una sola conexión por petición HTTP** (`AddScoped<IDbConnection>`):
- se abre cuando un repositorio la necesita;
- la comparten todos los SP de esa petición;
- se cierra sola al terminar la petición;
- entre peticiones, el pool de ADO.NET reutiliza las conexiones físicas, así que no se abre una conexión nueva a Azure cada vez.

### Formato de respuesta

Todas las respuestas, de éxito y de error, tienen la misma forma:

```json
{ "codigo": 0, "mensaje": "Salón creado correctamente.", "datos": { ... }, "exito": true }
```

Error:

```json
{ "codigo": 2003, "mensaje": "Para publicar el salón se requiere al menos un paquete.", "datos": null, "exito": false }
```

Listado paginado:

```json
{
  "pagina": 1, "tamanoPagina": 10, "totalRegistros": 2, "totalPaginas": 1,
  "codigo": 0, "mensaje": "Consulta exitosa.", "exito": true,
  "datos": [
    { "id": 2, "nombre": "Terraza Monarca", "zona": "Tepeji del Río, Hgo.", "capacidad": 80, "estado": "no_publicado", "totalPaquetes": 1 },
    { "id": 1, "nombre": "Salón Jardín Encanto", "zona": "Tula de Allende, Hgo.", "capacidad": 150, "estado": "publicado", "totalPaquetes": 2 }
  ]
}
```

También responden con este formato el JSON mal formado (`1001`) y cualquier error inesperado del servidor (`5000`).

### Catálogo de códigos de error

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

### Endpoints vigentes (todos POST)

Ya no hay parámetros en la URL: todo va en el body.

> Todavía no hay JWT: el `adminId` (el `id` que regresa el login) se manda en el body.

| Endpoint | Body | Regresa |
|---|---|---|
| `/api/usuarios/registro` | `{ nombreCompleto, correo, password, tipoCuenta, nombreSalon? }` | Usuario |
| `/api/usuarios/login` | `{ correo, password }` | Usuario |
| `/api/usuarios/obtener` | `{ id }` | Usuario |
| `/api/salones/crear` | `{ adminId, nombre, zona, capacidad, descripcion? }` | Salón |
| `/api/salones/obtener` | `{ id }` | Salón con `paquetes` y `fotos` |
| `/api/salones/actualizar` | `{ id, adminId, nombre, zona, capacidad, descripcion?, estado? }` | Salón con `paquetes` y `fotos` |
| `/api/salones/listar` | `{ adminId?, pagina, tamanoPagina }` | Listado paginado |
| `/api/paquetes/crear` | `{ salonId, adminId, nombrePaquete, descripcion?, precio }` | Paquete |
| `/api/paquetes/actualizar` | `{ id, adminId, nombrePaquete, descripcion?, precio }` | Paquete |
| `/api/paquetes/eliminar` | `{ id, adminId }` | Id del paquete eliminado |

Notas:
- En `actualizar`, si `descripcion` no se manda, se borra; si `estado` no se manda, se conserva el actual.
- En `listar`, si `adminId` no se manda, se listan todos los salones.

Ejemplo, agregar un paquete:

```json
POST /api/paquetes/crear
{
  "salonId": 1,
  "adminId": 1,
  "nombrePaquete": "Paquete Básico",
  "descripcion": "Salón, mesas y sillas por 6 horas.",
  "precio": 8500
}
```

### Qué tiene que cambiar el frontend

| Antes | Ahora |
|---|---|
| `GET /api/usuarios/{id}` | `POST /api/usuarios/obtener` con `{ id }` |
| `POST /api/salones` | `POST /api/salones/crear` |
| `GET /api/salones/{id}` | `POST /api/salones/obtener` con `{ id }` |
| `PUT /api/salones/{id}` | `POST /api/salones/actualizar` con `id` en el body |
| `POST /api/salones/{id}/paquetes` | `POST /api/paquetes/crear` con `salonId` en el body |
| `PUT /api/paquetes/{id}` | `POST /api/paquetes/actualizar` con `id` en el body |
| `DELETE /api/paquetes/{id}?adminId=` | `POST /api/paquetes/eliminar` con `{ id, adminId }` |

Además:
- Los datos ahora vienen dentro de `datos`: `const { datos } = await resp.json();`.
- Para saber si fue éxito se puede revisar `exito` (o `codigo === 0`).
- El mensaje para mostrar al usuario viene en `mensaje`.

### Verificación

- La solución compila sin errores ni advertencias (`dotnet build GlamSpaces.sln`).
- Se probaron los 10 endpoints de punta a punta contra una base de prueba con los mismos scripts, en 35 casos:
  - éxito;
  - cada código de error;
  - paginado y sus límites;
  - JSON inválido;
  - healthcheck;
  - rechazo de GET.

### Pendiente

- **JWT**: sacar el `adminId` del token en vez de recibirlo en el body (marcado con `TODO` en los DTOs).
- Subida de imágenes a almacenamiento externo.
- ~~Búsqueda pública de salones (HU-09)~~ → hecha en el Sprint 3.

---

## Sprint 3 — Búsqueda y filtrado de salones (HU-09)

Endpoint para que el frontend muestre a los clientes solo los salones que cumplen lo que buscan.
Sigue la misma arquitectura del Sprint 2.5: un SP nuevo (`sp_Salon_Buscar`), el repositorio y el endpoint.

### `POST /api/salones/buscar`

Todos los filtros son **opcionales** y se pueden **combinar** sin generar errores:

| Campo | Tipo | Qué hace |
|---|---|---|
| `zona` | string | Coincidencia parcial, sin importar mayúsculas ni acentos (`"rio"` encuentra `"Tepeji del Río"`) |
| `capacidadMinima` | int | Salones con esa capacidad **o mayor** |
| `precioMaximo` | number | Salones cuyo paquete más barato cuesta **eso o menos** |
| `pagina`, `tamanoPagina` | int | Paginado (por defecto 1 y 10; máximo 100) |

Si un filtro se manda vacío, en `null`, en 0 o negativo, se ignora.

```json
POST /api/salones/buscar
{ "zona": "tula", "precioMaximo": 10000 }
```

Respuesta (formato paginado estándar):

```json
{
  "pagina": 1, "tamanoPagina": 10, "totalRegistros": 1, "totalPaginas": 1,
  "codigo": 0, "mensaje": "Consulta exitosa.", "exito": true,
  "datos": [
    {
      "id": 1,
      "nombre": "Salón Jardín Encanto",
      "zona": "Tula de Allende, Hgo.",
      "capacidad": 150,
      "precioDesde": 8500.00,
      "fotoPrincipal": "https://glamspacesfrontsa.z41.web.core.windows.net/img/salon-jardin-1.jpg"
    }
  ]
}
```

- `precioDesde`: precio del paquete más barato del salón.
- `fotoPrincipal`: la primera foto del salón; `null` si no tiene.

### Reglas

- Solo aparecen salones **publicados**. Como para publicar se requiere al menos un paquete, siempre hay `precioDesde`.
- Resultados ordenados del más barato al más caro. Ordenar por relevancia o calificación está fuera de alcance.
- **Sin resultados no es un error**: responde `codigo: 0`, `datos: []` y `mensaje: "No se encontraron salones con esos filtros."`.

### Criterios de aceptación (verificados)

| # | Caso | Resultado |
|---|---|---|
| 1 | Búsqueda sin filtros | Todos los salones publicados (los no publicados nunca aparecen) |
| 2 | `capacidadMinima` | Solo salones con esa capacidad o mayor |
| 3 | `zona` + `precioMaximo` | Solo los que cumplen ambas condiciones |
| 4 | Ningún salón cumple | Lista vacía con respuesta exitosa |

También se probaron los 3 filtros combinados, la zona sin acento, los filtros vacíos o negativos, el paginado, un tipo de dato incorrecto (`1001`) y la regresión completa del Sprint 2.5.

---

## Sprint 3 — Índices y consultas para búsqueda de salones (HU-08)

Optimiza la búsqueda de la HU-09 para que responda rápido aunque haya muchos salones.

### La consulta

Vive en `sp_Salon_Buscar` (`Database/02_StoredProcedures.sql`):
- **"Precio desde"**: combina cada salón con su paquete más barato (`CROSS APPLY ... MIN(Precio)`).
- **Solo publicados**: filtra internamente por `Estado = 'publicado'`. Un salón no publicado nunca participa.
- **`OPTION (RECOMPILE)`**: como todos los filtros son opcionales, SQL Server arma el plan con los valores reales de cada búsqueda. Sin esto usaría un plan genérico que no aprovecha los índices.

### Los índices (`Database/03_Indices.sql`)

| Índice | Columnas | Para qué |
|---|---|---|
| `IX_Salones_Publicados_Capacidad_Zona` | `Capacidad` + incluye `Zona`, `Nombre`, **filtrado** `WHERE Estado = 'publicado'` | Solo guarda los salones publicados, así es más chico que la tabla. "Capacidad mínima" busca directo en el índice. La zona se filtra dentro del índice sin leer la tabla. |
| `IX_Paquetes_SalonId_Precio` | `SalonId, Precio` | El paquete más barato de cada salón es la primera fila del índice. |
| `IX_FotosSalon_SalonId` | `SalonId` + incluye `Url` | La foto principal sale directo del índice. |

**¿Por qué zona no es la llave del índice?** La zona se busca por coincidencia parcial (`LIKE '%tula%'`). Ese tipo de búsqueda no puede "saltar" directo dentro de un índice, así que de llave no serviría. Como columna incluida sí sirve: el filtro se resuelve leyendo solo el índice (chico y solo con publicados) y no la tabla completa.

**Detalle técnico:** los índices filtrados requieren `QUOTED_IDENTIFIER ON` en los SP que insertan o actualizan salones. Por eso `02_StoredProcedures.sql` lo activa al inicio. Hay que correr `02` antes que `03`.

### Prueba de rendimiento (criterio 2)

Prueba en LocalDB con **50,000 salones** (40,000 publicados), 150,000 paquetes y 50,000 fotos. Cada búsqueda se ejecutó 10 veces; se reporta el promedio.

| Búsqueda | Sin índices | Con índices | Mejora |
|---|---|---|---|
| zona + precio máximo | 421.6 ms · 45,226 lecturas | **84.1 ms** · 1,355 lecturas | **5× más rápida**, 33× menos lecturas |
| zona + capacidad + precio | 620.4 ms · 78,665 lecturas | **70.8 ms** · 1,522 lecturas | **9× más rápida**, 52× menos lecturas |
| capacidad mínima | 117.8 ms | **57.3 ms** | 2× más rápida |
| sin filtros | 284.7 ms | **206.1 ms** | 1.4× más rápida |

La búsqueda sin filtros es la más pesada porque tiene que calcular el "precio desde" de **todos** los salones publicados para ordenarlos por precio. Con 40,000 salones sigue estando en ~200 ms. Si algún día crece mucho más, la mejora sería guardar el precio mínimo como columna del salón.

Los tiempos son de una laptop; en Azure varían según el plan contratado, pero la proporción de mejora se mantiene.

### Criterios de aceptación (verificados con `04_DatosPrueba.sql`)

| # | Caso | Resultado |
|---|---|---|
| 1 | Salones publicados con distintos precios | Cada uno muestra su paquete más barato (ej. Hacienda El Rosario: paquetes de $30,000 y $45,000 → `precioDesde: 30000`) |
| 2 | Índice sobre zona y capacidad | Búsquedas filtradas de 5 a 9 veces más rápidas (tabla de arriba) |
| 3 | Salón no publicado | "[Prueba] Jardín Oculto" (no publicado, el más barato y con más capacidad) nunca aparece |
| 4 | 6 salones de prueba con filtros combinados | Resultados consistentes. Ej.: zona "tula" + capacidad ≥ 200 + precio ≤ 40,000 → solo Hacienda El Rosario |

### Para desplegar (HU-08 y HU-09)

Correr en `glamspaces-db`, en este orden:
1. `Database/02_StoredProcedures.sql`: agrega `sp_Salon_Buscar` y recrea los demás con `QUOTED_IDENTIFIER ON`.
2. `Database/03_Indices.sql`: crea los índices.

Ambos son idempotentes. **No correr** `04_DatosPrueba.sql` en producción.
