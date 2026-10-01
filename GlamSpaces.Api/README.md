# GlamSpaces.Api — Registro e inicio de sesión (Sprint 1)

API en ASP.NET Core (.NET 9) basada en el modelo `Usuario` que les pasaron:

```csharp
public class Usuario
{
    public int Id { get; set; }
    public string NombreCompleto { get; set; }
    public string Correo { get; set; }
    public string PasswordHash { get; set; }
    public string TipoCuenta { get; set; } // "cliente" o "administrador"
    public string? NombreSalon { get; set; } // solo si TipoCuenta = administrador
    public DateTime FechaRegistro { get; set; } = DateTime.Now;
}
```

Los únicos cambios respecto al modelo original son cosméticos (valores por
defecto `= string.Empty` para que compile con `Nullable` activado). Nada de
la forma de los datos cambió.

## Requisitos

- .NET 9 SDK instalado (`dotnet --version` debe mostrar 9.x).

## Cómo correrla

```bash
cd GlamSpaces.Api
dotnet restore
dotnet run
```

Por defecto corre en algo como `http://localhost:5062` (la terminal te dice
el puerto exacto). Abre `http://localhost:5062/swagger` para probar los
endpoints desde el navegador sin necesitar Postman.

La base de datos es un archivo SQLite (`glamspaces.db`) que se crea solo la
primera vez que corres el proyecto — no hay que instalar SQL Server ni nada
aparte. Si más adelante Adrian quiere usar otro motor (SQL Server, MySQL),
solo se cambia una línea en `Program.cs` (`UseSqlite` → `UseSqlServer`, etc.)
y la cadena de conexión en `appsettings.json`; los controllers y el modelo
no cambian.

## Endpoints

### `POST /api/usuarios/registro`

Sirve tanto para HU-02 (cliente) como para HU-03 (administrador).

```json
{
  "nombreCompleto": "Erick Trejo Reséndiz",
  "correo": "erick@glamspaces.com",
  "password": "glam2026",
  "tipoCuenta": "cliente",
  "nombreSalon": null
}
```

Para un administrador, `tipoCuenta` es `"administrador"` y `nombreSalon` es
obligatorio.

Respuestas:
- `201 Created` con los datos del usuario (sin la contraseña).
- `400` con los errores de validación por campo (igual que los mensajes de
  tu pantalla: nombre vacío, correo inválido, contraseña corta, etc.).
- `409 Conflict` si el correo ya existe.

### `POST /api/usuarios/login`

Cubre HU-04.

```json
{
  "correo": "erick@glamspaces.com",
  "password": "glam2026"
}
```

Respuestas:
- `200 OK` con los datos del usuario si el correo y la contraseña son
  correctos.
- `401 Unauthorized` con un mensaje genérico si fallan (no dice cuál de los
  dos fue, igual que pide el criterio de aceptación de HU-04).

### `GET /api/usuarios/{id}`

Regresa un usuario por su id (sin la contraseña). Útil para probar que el
registro sí quedó guardado.

## Conectar tu pantalla de React Native a este API

En `RegistroClienteScreen.js`, reemplaza la función `guardarUsuario` por
esto (ajusta la URL al puerto que te haya dado `dotnet run`):

```js
async function guardarUsuario(usuario) {
  const resp = await fetch("http://localhost:5062/api/usuarios/registro", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      nombreCompleto: usuario.nombre,
      correo: usuario.correo,
      password: usuario.password,
      tipoCuenta: usuario.tipo_cuenta,
      nombreSalon: usuario.nombre_salon ?? null,
    }),
  });

  if (resp.status === 409) {
    throw new Error("correo_duplicado");
  }
  if (!resp.ok) {
    throw new Error("error_registro");
  }
  return resp.json();
}
```

Nota: si pruebas desde el celular con Expo Go, `localhost` no funciona
(apunta al celular, no a tu laptop) — ahí hay que usar la IP de tu laptop
en la red local, por ejemplo `http://192.168.1.50:5062/...`.
