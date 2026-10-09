# GlamSpaces-Back

API de GlamSpaces en ASP.NET Core (.NET 9) + Entity Framework Core sobre **Azure SQL** (`glamspaces-db`).
Plataforma para encontrar y reservar salones de eventos.

- Swagger: `/swagger` (abierto en todos los entornos)
- Healthcheck: `/health` → `Healthy` si la API y la BD responden
- Cómo correrla en local y configurar la cadena de conexión: ver [GlamSpaces.Api/README.md](GlamSpaces.Api/README.md)

## Estructura

```
GlamSpaces.Api/
├── Controllers/   Endpoints (UsuariosController, SalonesController, PaquetesController)
├── Data/          GlamSpacesContext (EF Core)
├── Dtos/          Requests y responses del API
├── Models/        Usuario, Salon, Paquete, FotoSalon
└── Services/      PasswordHasher
```

Las tablas ya existen en Azure SQL (script manual); EF solo las mapea, no hay migraciones.

---

## Sprint 1 — Registro e inicio de sesión

| HU | Endpoint | Descripción |
|----|----------|-------------|
| HU-02 / HU-03 | `POST /api/usuarios/registro` | Registro de cliente o administrador de salón |
| HU-04 | `POST /api/usuarios/login` | Inicio de sesión (regresa el usuario con su `id`) |
| — | `GET /api/usuarios/{id}` | Consulta de usuario |

---

## Sprint 2 — CRUD de salones y paquetes (HU-06)

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
