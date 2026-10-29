# Versionamiento de la base de datos — GlamSpace-Back

**Actividad: Parte 2 · Sprint 4 (y HU-14 del Sprint 5) · Responsable: Adrian Adonai García Jiménez**

Herramienta: **FluentMigrator 8.0.1** · Base de datos: SQL Server (LocalDB en desarrollo, contenedor
SQL Server 2022 en GitHub Actions; producción es Azure SQL, que **no** se toca en esta práctica).

Evidencias completas: [`docs/evidencias/`](evidencias/) (salida real de cada comando, un archivo por paso).

---

## 1. Contexto y por qué no basta con scripts SQL a mano

El backend usa Dapper + stored procedures; Entity Framework se retiró en el Sprint 2.5, así que
**EF Migrations no aplica**. Hasta ahora el esquema vivía en scripts sueltos
(`Database/01_Tablas.sql` a `03_Indices.sql`) que alguien corría a mano en Azure.

Eso tiene problemas reales:

| Problema con scripts a mano | Qué resuelve una herramienta de migraciones |
|---|---|
| No queda registro de **qué** scripts ya se corrieron en cada base de datos ni **cuándo**. | Tabla de historial (`VersionInfo`) en cada BD: versión, fecha y descripción de cada cambio aplicado. |
| Hay que recordar el **orden** correcto (tablas → SP → índices) y no saltarse ninguno. | Cada migración tiene un número de versión y se aplican en orden, solo las pendientes. |
| No hay forma estándar de **revertir** un cambio. | Cada migración trae su `Down()`: el rollback es un comando. |
| Es difícil reproducir la BD desde cero en otra máquina o en CI. | Un solo comando deja una BD vacía igual que producción. |
| No se puede automatizar ni verificar en un pipeline. | La CLI corre igual en local, en GitHub Actions y en cualquier servidor. |

Ya nos pasó en el proyecto: en Azure la tabla `Usuarios` la creó EF y no tiene `DEFAULT`, mientras que
el script dice que sí. Con scripts a mano esas diferencias pasan desapercibidas.

---

## 2. Alternativas investigadas (punto 7)

Tres herramientas gratuitas u open source compatibles con SQL Server:

- **FluentMigrator**: framework de migraciones para .NET; cada migración es una clase de C# con `Up()` y `Down()`.
- **Liquibase (edición open source)**: herramienta en Java que aplica *changesets* definidos en XML, YAML, JSON o SQL con formato.
- **Flyway Community (Redgate)**: herramienta en Java que aplica scripts SQL versionados por nombre (`V1__descripcion.sql`).

---

## 3. Tabla comparativa (punto 8)

| | **FluentMigrator** | **Liquibase (open source)** | **Flyway Community** |
|---|---|---|---|
| **Compatibilidad con SQL Server** | Sí (procesadores `SqlServer2005` a `SqlServer2016`; funciona con Azure SQL y LocalDB). | Sí (vía driver JDBC de SQL Server). | Sí (vía driver JDBC). |
| **Forma de escribir migraciones** | Clases de C# con API fluida (`Create.Table`, `Alter.Table`, `Create.Index`) o SQL con `Execute.Sql`. | *Changesets* en XML, YAML, JSON o SQL con formato (`--changeset autor:id`). | Archivos SQL con convención de nombre (`V2__crear_tabla.sql`); también Java. |
| **Historial** | Tabla `VersionInfo` (versión, fecha de aplicación, descripción). | Tablas `DATABASECHANGELOG` (historial con *checksum*) y `DATABASECHANGELOGLOCK` (bloqueo). | Tabla `flyway_schema_history` (historial con *checksum*). |
| **Reversión (rollback)** | **Nativa y gratis**: método `Down()` en cada migración; `rollback by N`, `rollback to <versión>`, `rollback all`. | **Gratis** en todas las ediciones: `rollback-count`, `rollback` (a un tag), `rollback-to-date`. Algunos cambios se revierten solos; otros requieren un bloque `rollback`. `rollback-one-changeset` es de pago (Pro). | **No en Community**: el comando `undo` requiere Flyway **Teams** (de pago). En Community solo se puede corregir hacia adelante o borrar todo con `clean`. |
| **Ver aplicadas y pendientes** | `dotnet-fm list migrations` marca cada una como aplicada, `(current)` o `(not applied)`. | `liquibase status` / `history`. | `flyway info`. |
| **Integración con pipelines** | CLI `dotnet-fm` (herramienta de .NET) o runner dentro del código; corre en cualquier CI con .NET. | CLI, imagen Docker, plugins de Maven y Gradle, acción de GitHub. | CLI, imagen Docker, plugins de Maven y Gradle. |
| **Ventajas** | Mismo lenguaje y ecosistema que el backend (.NET), sin Java. Rollback incluido. Cada migración corre en su propia transacción. | Multi-base de datos. *Checksums* detectan cambios en migraciones ya aplicadas. Rollback gratuito. | Muy simple: solo archivos SQL. *Checksums* y comando `validate`. Muy popular. |
| **Desventajas** | El `Down()` se escribe a mano. No acepta el separador `GO` dentro de un mismo `Execute.Sql`. | Requiere Java en el ambiente. XML/YAML verboso. Funciones avanzadas en la edición Pro. | Requiere Java. El rollback (`undo`) es de pago. |
| **Limitaciones** | Solo para proyectos .NET. No guarda *checksum* de las migraciones aplicadas. | Para *changesets* en SQL el rollback se escribe a mano. | Sin rollback en Community; otras funciones (vista previa, *dry run*) también son de las ediciones de pago. |

Fuentes: [FluentMigrator — dotnet-fm](https://github.com/fluentmigrator/fluentmigrator/blob/main/docs-website/runners/dotnet-fm.md),
[FluentMigrator — FAQ (GO)](https://github.com/fluentmigrator/fluentmigrator/blob/main/docs-website/intro/faq.md),
[Liquibase — comandos de rollback](https://github.com/liquibase/liquibase-docs/blob/master/Content/commands/rollback/home.htm),
[Flyway — Undo (Teams)](https://documentation.red-gate.com/flyway/reference/commands/undo),
[Flyway — Comandos y ediciones](https://documentation.red-gate.com/flyway/reference/commands).
Consultadas el 23/10/2026.

---

## 4. Justificación de la elección e instalación (punto 9)

### Por qué FluentMigrator

1. **Nativo de .NET**: el backend es .NET 9; no hay que instalar ni mantener Java (Liquibase y Flyway lo necesitan).
2. **Rollback gratis**: cada migración trae su `Down()` y se revierte con `rollback by 1`. En Flyway el `undo` es de pago.
3. **Aplicadas y pendientes con un comando**: `list migrations`.
4. **Historial en la propia BD**: tabla `VersionInfo`.
5. **Corre igual en todos lados**: LocalDB en desarrollo, SQL Server en contenedor en GitHub Actions y, cuando el equipo lo decida, Azure SQL.
6. **Encaja con lo que ya existe**: la línea base carga los scripts de `Database/` tal cual.

### Instalación (comandos exactos que se ejecutaron)

```bash
# Proyecto de migraciones dentro de la solución
dotnet new classlib -f net9.0 -n GlamSpaces.Migrations -o GlamSpaces.Migrations
dotnet sln GlamSpaces.sln add GlamSpaces.Migrations
dotnet add GlamSpaces.Migrations package FluentMigrator                  # 8.0.1
dotnet add GlamSpaces.Migrations package FluentMigrator.Runner.SqlServer # 8.0.1

# Herramienta local (CLI dotnet-fm), versionada en el repo
dotnet new tool-manifest
dotnet tool install FluentMigrator.DotNet.Cli                            # 8.0.1
```

- El manifiesto quedó en **`.config/dotnet-tools.json`** (el SDK .NET 10 lo crea en la raíz; se movió a `.config/`, que también es una ubicación estándar).
- Como herramienta **local**, se invoca con `dotnet tool run dotnet-fm -- ...` (o `dotnet dotnet-fm`). `dotnet fm` a secas solo funciona si se instala como herramienta global.
- Quien clone el repo solo necesita `dotnet tool restore`.

Estructura:

```
GlamSpaces.Migrations/
├── GlamSpaces.Migrations.csproj   FluentMigrator + Runner.SqlServer; incrusta Database/01, 02 y 03
├── GlobalUsings.cs                (sin "using" en las clases)
├── Scripts/ScriptsSql.cs          lee los .sql incrustados y los separa por "GO"
└── Migraciones/
    ├── 001_LineaBase.cs
    ├── 002_CrearTablaBusquedasFavoritas.cs
    ├── 003_AgregarColumnaTelefonoSalones.cs
    └── 004_IndiceUnicoBusquedasFavoritas.cs
scripts/migraciones.ps1            listar, subir, rollback, reaplicar, respaldar, reset, consultar
.github/workflows/migraciones.yml  pipeline en GitHub Actions
```

---

## 5. Migraciones creadas (punto 10)

| # | Versión | Nombre | Qué hace | Up | Down |
|---|---|---|---|---|---|
| 001 | `202610230900` | `M001_LineaBase` | Deja una BD vacía igual que producción sin tocar lo que ya existe. | Crea `Usuarios` como la creó EF (sin `DEFAULT`, índice `IX_Usuarios_Correo`) y ejecuta **tal cual** `Database/01_Tablas.sql`, `02_StoredProcedures.sql` y `03_Indices.sql` (incrustados en el ensamblado, sin copias a mano). Todo es idempotente: `IF OBJECT_ID / IF NOT EXISTS` para tablas e índices y `CREATE OR ALTER` para los 12 SP. | **Vacío a propósito**: revertir la línea base borraría el esquema de producción y sus datos. Para empezar de cero en desarrollo se usa el *reset limpio*. |
| 002 | `202610230910` | `M002_CrearTablaBusquedasFavoritas` | **Creación de tabla**: salones favoritos de un usuario. | `Create.Table("BusquedasFavoritas")`: `Id` (PK identity), `UsuarioId` (FK → `Usuarios`), `SalonId` (FK → `Salones`), `FechaCreacion DATETIME2 DEFAULT GETDATE()`. | `Delete.Table("BusquedasFavoritas")`. |
| 003 | `202610230920` | `M003_AgregarColumnaTelefonoSalones` | **Adición de columna**. | `Alter.Table("Salones").AddColumn("Telefono").AsString(20).Nullable()` → `NVARCHAR(20) NULL`. | `Delete.Column("Telefono").FromTable("Salones")`. |
| 004 | `202610230930` | `M004_IndiceUnicoBusquedasFavoritas` | **Índice / restricción**: un usuario no puede guardar el mismo salón dos veces. | `Create.Index("UX_BusquedasFavoritas_Usuario_Salon")` único sobre `(UsuarioId, SalonId)`. | `Delete.Index("UX_BusquedasFavoritas_Usuario_Salon")`. |
| 005 | `202610291000` | `M005_CrearSolicitudes` | **HU-14 (Sprint 5)**: tabla `Solicitudes` de solicitudes de reservación. Ya existía en producción (se creó a mano), por eso es idempotente. Ver [sección 12](#12-sprint-5--hu-14-tabla-solicitudes). | 4 lotes `Execute.Sql`, cada uno con `IF OBJECT_ID … IS NULL` / `IF NOT EXISTS`: tabla con PK, 3 FK y 2 `DEFAULT`; `CK_Solicitudes_Invitados`; `CK_Solicitudes_Estado`; índice único filtrado `UX_Solicitudes_Cliente_Salon_Fecha_Pendiente`. | `Delete.Table("Solicitudes")` (se van con ella sus restricciones e índice). |

**Detalle técnico de la línea base:** FluentMigrator no acepta el separador `GO` dentro de un mismo
`Execute.Sql`, y los scripts de `Database/` lo usan entre cada SP. `ScriptsSql.Lotes()` divide cada
script en lotes por `GO` y la migración los ejecuta uno por uno, como haría `sqlcmd`.

**En producción**, la 001 no borra ni recrea nada: las tablas e índices ya existen y se saltan, los SP
se recrean iguales con `CREATE OR ALTER`, y la BD queda registrada en `VersionInfo` lista para recibir
las migraciones siguientes.

---

## 6. Cómo identificar migraciones aplicadas y pendientes (punto 11)

```powershell
.\scripts\migraciones.ps1 listar
# equivale a:
dotnet tool run dotnet-fm -- list migrations -p SqlServer2016 -c "<conexión>" -a GlamSpaces.Migrations/bin/Release/net9.0/GlamSpaces.Migrations.dll
```

Cómo leer la salida:
- `(not applied)` → **pendiente**.
- Sin marca → **aplicada**.
- `(current)` → la última aplicada (versión actual de la BD).

La primera vez que se ejecuta en una BD, FluentMigrator crea su tabla de historial `VersionInfo`
(se ve como `VersionMigration`, `VersionUniqueMigration` y `VersionDescriptionMigration`).

### Antes de aplicar: todas pendientes — [`01_list_antes.txt`](evidencias/01_list_antes.txt)

```text
# Evidencia 01 — Migraciones antes de aplicar (BD vacía: todas pendientes)
# Fecha: 2026-10-23 22:04:00 (hora local)
# Servidor / BD: (localdb)\MSSQLLocalDB / GlamSpacesMigraciones
# Comando: .\scripts\migraciones.ps1 listar   (dotnet tool run dotnet-fm -- list migrations)
#
-------------------------------------------------------------------------------
VersionMigration migrating
-------------------------------------------------------------------------------
Beginning Transaction
CreateTable VersionInfo
Committing Transaction
VersionMigration migrated
-------------------------------------------------------------------------------
VersionUniqueMigration migrating
-------------------------------------------------------------------------------
Beginning Transaction
CreateIndex VersionInfo (Version)
AlterTable VersionInfo
CreateColumn VersionInfo AppliedOn DateTime
Committing Transaction
VersionUniqueMigration migrated
-------------------------------------------------------------------------------
VersionDescriptionMigration migrating
-------------------------------------------------------------------------------
Beginning Transaction
AlterTable VersionInfo
CreateColumn VersionInfo Description String
Committing Transaction
VersionDescriptionMigration migrated
-------------------------------------------------------------------------------
Migrations
-------------------------------------------------------------------------------
202610230900: M001_LineaBase (not applied)
202610230910: M002_CrearTablaBusquedasFavoritas (not applied)
202610230920: M003_AgregarColumnaTelefonoSalones (not applied)
202610230930: M004_IndiceUnicoBusquedasFavoritas (not applied)
Task completed.
# Código de salida: 0
```

### Aplicar todas las pendientes — [`02_migrate_up.txt`](evidencias/02_migrate_up.txt)

`.\scripts\migraciones.ps1 subir` → `dotnet-fm migrate ... up`

<details>
<summary>Salida completa (incluye cada sentencia SQL de la línea base; clic para expandir)</summary>

```text
# Evidencia 02 — Aplicar todas las migraciones pendientes
# Fecha: 2026-10-23 22:05:02 (hora local)
# Servidor / BD: (localdb)\MSSQLLocalDB / GlamSpacesMigraciones
# Comando: .\scripts\migraciones.ps1 subir   (dotnet tool run dotnet-fm -- migrate -p SqlServer2016 -c <conexión> -a <dll> up)
#
Beginning Transaction
Rolling back transaction
-------------------------------------------------------------------------------
202610230900: M001_LineaBase migrating
-------------------------------------------------------------------------------
Beginning Transaction
ExecuteSqlStatement 
IF OBJECT_ID(N'dbo.Usuarios', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Usuarios (
        Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Usuarios PRIMARY KEY,
        NombreCompleto NVARCHAR(150) NOT NULL,
        Correo         NVARCHAR(150) NOT NULL,
        PasswordHash   NVARCHAR(MAX) NOT NULL,
        TipoCuenta     NVARCHAR(20)  NOT NULL,
        NombreSalon    NVARCHAR(150) NULL,
        FechaRegistro  DATETIME2     NOT NULL
    );
    CREATE UNIQUE INDEX IX_Usuarios_Correo ON dbo.Usuarios (Correo);
END
ExecuteSqlStatement /* =========================================================================
   GlamSpaces — Tablas (glamspaces-db, Azure SQL)
   Idempotente: se puede volver a correr sin romper nada si ya existen.
   Correr ANTES de 02_StoredProcedures.sql.
   ========================================================================= */

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Usuarios')
BEGIN
    CREATE TABLE Usuarios (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        NombreCompleto  NVARCHAR(150) NOT NULL,
        Correo          NVARCHAR(150) NOT NULL UNIQUE,
        PasswordHash    NVARCHAR(MAX) NOT NULL,
        TipoCuenta      NVARCHAR(20) NOT NULL,   -- 'cliente' o 'administrador'
        NombreSalon     NVARCHAR(150) NULL,      -- solo si TipoCuenta = 'administrador'
        FechaRegistro   DATETIME2 NOT NULL DEFAULT GETDATE()
    );
END
ExecuteSqlStatement IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Salones')
BEGIN
    CREATE TABLE Salones (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        AdminId       INT NOT NULL,
        Nombre        NVARCHAR(150) NOT NULL,
        Zona          NVARCHAR(200) NOT NULL,
        Capacidad     INT NOT NULL,
        Descripcion   NVARCHAR(1000) NULL,
        Estado        NVARCHAR(20) NOT NULL DEFAULT 'no_publicado',
        FechaCreacion DATETIME2 NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_Salones_Usuarios FOREIGN KEY (AdminId) REFERENCES Usuarios(Id),
        CONSTRAINT CK_Salones_Capacidad CHECK (Capacidad > 0),
        CONSTRAINT CK_Salones_Estado CHECK (Estado IN ('publicado', 'no_publicado'))
    );
END
ExecuteSqlStatement IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Paquetes')
BEGIN
    CREATE TABLE Paquetes (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        SalonId         INT NOT NULL,
        NombrePaquete   NVARCHAR(150) NOT NULL,
        Descripcion     NVARCHAR(500) NULL,
        Precio          DECIMAL(10,2) NOT NULL,
        CONSTRAINT FK_Paquetes_Salones FOREIGN KEY (SalonId) REFERENCES Salones(Id),
        CONSTRAINT CK_Paquetes_Precio CHECK (Precio > 0)
    );
END
ExecuteSqlStatement IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'FotosSalon')
BEGIN
    CREATE TABLE FotosSalon (
        Id      INT IDENTITY(1,1) PRIMARY KEY,
        SalonId INT NOT NULL,
        Url     NVARCHAR(500) NOT NULL,
        CONSTRAINT FK_FotosSalon_Salones FOREIGN KEY (SalonId) REFERENCES Salones(Id)
    );
END
ExecuteSqlStatement -- Sprint 4 — HU-11. Solo las reservaciones 'confirmada' ocupan la fecha del salón (HU-12).
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Reservaciones')
BEGIN
    CREATE TABLE Reservaciones (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        SalonId       INT NOT NULL,
        ClienteId     INT NOT NULL,
        PaqueteId     INT NULL,
        FechaEvento   DATE NOT NULL,
        Estado        NVARCHAR(20) NOT NULL DEFAULT 'pendiente',
        FechaCreacion DATETIME2 NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_Reservaciones_Salones FOREIGN KEY (SalonId) REFERENCES Salones(Id),
        CONSTRAINT FK_Reservaciones_Usuarios FOREIGN KEY (ClienteId) REFERENCES Usuarios(Id),
        CONSTRAINT FK_Reservaciones_Paquetes FOREIGN KEY (PaqueteId) REFERENCES Paquetes(Id),
        CONSTRAINT CK_Reservaciones_Estado CHECK (Estado IN ('pendiente', 'confirmada', 'cancelada'))
    );
END
ExecuteSqlStatement /* =========================================================================
   GlamSpaces — Stored procedures (glamspaces-db, Azure SQL)
   Correr DESPUÉS de 01_Tablas.sql. Idempotente (CREATE OR ALTER).

   Convención de todos los SP:
     - Reciben @Codigo INT OUTPUT y @Mensaje NVARCHAR(200) OUTPUT.
     - @Codigo = 0 es éxito; los demás números son los de
       GlamSpaces.Domain/Comun/CodigosError.cs.
     - Si hay error, hacen RETURN sin regresar result sets.
   ========================================================================= */

-- Los SP guardan estas opciones al crearse. Deben estar en ON porque Salones tiene
-- índices filtrados (03_Indices.sql); con OFF, los INSERT/UPDATE de los SP fallarían.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
ExecuteSqlStatement -- =========================================================================
-- USUARIOS
-- =========================================================================

-- La API manda la contraseña ya hasheada (PBKDF2); el SP nunca ve el texto plano.
CREATE OR ALTER PROCEDURE sp_Usuario_Registrar
    @NombreCompleto NVARCHAR(150),
    @Correo         NVARCHAR(150),
    @PasswordHash   NVARCHAR(MAX),
    @TipoCuenta     NVARCHAR(20),
    @NombreSalon    NVARCHAR(150) = NULL,
    @Codigo         INT OUTPUT,
    @Mensaje        NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @NombreCompleto = TRIM(@NombreCompleto);
    SET @Correo = LOWER(TRIM(@Correo));
    SET @TipoCuenta = LOWER(TRIM(@TipoCuenta));
    SET @NombreSalon = NULLIF(TRIM(@NombreSalon), '');

    IF ISNULL(@NombreCompleto, '') = ''
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'Escribe tu nombre completo.'; RETURN; END

    IF ISNULL(@Correo, '') NOT LIKE '_%@_%._%'
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'El formato del correo no es válido.'; RETURN; END

    IF ISNULL(@TipoCuenta, '') NOT IN ('cliente', 'administrador')
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'El tipo de cuenta debe ser "cliente" o "administrador".'; RETURN; END

    IF @TipoCuenta = 'administrador' AND @NombreSalon IS NULL
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'Escribe el nombre de tu salón.'; RETURN; END

    IF EXISTS (SELECT 1 FROM Usuarios WHERE Correo = @Correo)
    BEGIN SELECT @Codigo = 1002, @Mensaje = N'Ya existe una cuenta con este correo.'; RETURN; END

    INSERT INTO Usuarios (NombreCompleto, Correo, PasswordHash, TipoCuenta, NombreSalon, FechaRegistro)
    VALUES (@NombreCompleto, @Correo, @PasswordHash, @TipoCuenta,
            CASE WHEN @TipoCuenta = 'administrador' THEN @NombreSalon END, GETDATE());

    SELECT @Codigo = 0, @Mensaje = N'Cuenta creada correctamente.';

    SELECT Id, NombreCompleto, Correo, TipoCuenta, NombreSalon, FechaRegistro
    FROM Usuarios WHERE Id = SCOPE_IDENTITY();
END
ExecuteSqlStatement -- Para el login: regresa el hash y la API lo verifica (el SP no puede verificar PBKDF2).
CREATE OR ALTER PROCEDURE sp_Usuario_ObtenerPorCorreo
    @Correo  NVARCHAR(150),
    @Codigo  INT OUTPUT,
    @Mensaje NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT @Codigo = 0, @Mensaje = N'Consulta exitosa.';

    SELECT Id, NombreCompleto, Correo, PasswordHash, TipoCuenta, NombreSalon, FechaRegistro
    FROM Usuarios WHERE Correo = LOWER(TRIM(@Correo));
END
ExecuteSqlStatement CREATE OR ALTER PROCEDURE sp_Usuario_Obtener
    @Id      INT,
    @Codigo  INT OUTPUT,
    @Mensaje NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Id = @Id)
    BEGIN SELECT @Codigo = 1005, @Mensaje = N'El usuario no existe.'; RETURN; END

    SELECT @Codigo = 0, @Mensaje = N'Consulta exitosa.';

    SELECT Id, NombreCompleto, Correo, TipoCuenta, NombreSalon, FechaRegistro
    FROM Usuarios WHERE Id = @Id;
END
ExecuteSqlStatement -- =========================================================================
-- SALONES
-- =========================================================================

CREATE OR ALTER PROCEDURE sp_Salon_Crear
    @AdminId     INT,
    @Nombre      NVARCHAR(150),
    @Zona        NVARCHAR(200),
    @Capacidad   INT,
    @Descripcion NVARCHAR(1000) = NULL,
    @Codigo      INT OUTPUT,
    @Mensaje     NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @Nombre = TRIM(@Nombre);
    SET @Zona = TRIM(@Zona);
    SET @Descripcion = NULLIF(TRIM(@Descripcion), '');

    IF ISNULL(@Nombre, '') = ''
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'El nombre del salón es obligatorio.'; RETURN; END

    IF ISNULL(@Zona, '') = ''
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'La zona es obligatoria.'; RETURN; END

    IF ISNULL(@Capacidad, 0) <= 0
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'La capacidad debe ser mayor a 0.'; RETURN; END

    IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Id = @AdminId AND TipoCuenta = 'administrador')
    BEGIN SELECT @Codigo = 1004, @Mensaje = N'El usuario no existe o no es administrador.'; RETURN; END

    -- Siempre nace "no_publicado": todavía no tiene paquetes.
    INSERT INTO Salones (AdminId, Nombre, Zona, Capacidad, Descripcion, Estado, FechaCreacion)
    VALUES (@AdminId, @Nombre, @Zona, @Capacidad, @Descripcion, 'no_publicado', GETDATE());

    SELECT @Codigo = 0, @Mensaje = N'Salón creado correctamente.';

    SELECT Id, AdminId, Nombre, Zona, Capacidad, Descripcion, Estado, FechaCreacion
    FROM Salones WHERE Id = SCOPE_IDENTITY();
END
ExecuteSqlStatement -- Regresa SIEMPRE 3 result sets (salón, paquetes, fotos), vacíos si el salón no existe,
-- para que la API los pueda leer en orden.
CREATE OR ALTER PROCEDURE sp_Salon_Obtener
    @Id      INT,
    @Codigo  INT OUTPUT,
    @Mensaje NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM Salones WHERE Id = @Id)
        SELECT @Codigo = 0, @Mensaje = N'Consulta exitosa.';
    ELSE
        SELECT @Codigo = 2001, @Mensaje = N'El salón no existe.';

    SELECT Id, AdminId, Nombre, Zona, Capacidad, Descripcion, Estado, FechaCreacion
    FROM Salones WHERE Id = @Id;

    SELECT Id, SalonId, NombrePaquete, Descripcion, Precio
    FROM Paquetes WHERE SalonId = @Id ORDER BY Id;

    SELECT Id, SalonId, Url
    FROM FotosSalon WHERE SalonId = @Id ORDER BY Id;
END
ExecuteSqlStatement -- Si @Estado viene NULL se conserva el actual. Descripcion NULL la borra.
CREATE OR ALTER PROCEDURE sp_Salon_Actualizar
    @Id          INT,
    @AdminId     INT,
    @Nombre      NVARCHAR(150),
    @Zona        NVARCHAR(200),
    @Capacidad   INT,
    @Descripcion NVARCHAR(1000) = NULL,
    @Estado      NVARCHAR(20) = NULL,
    @Codigo      INT OUTPUT,
    @Mensaje     NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @DuenoId INT, @EstadoActual NVARCHAR(20);
    SELECT @DuenoId = AdminId, @EstadoActual = Estado FROM Salones WHERE Id = @Id;

    IF @DuenoId IS NULL
    BEGIN SELECT @Codigo = 2001, @Mensaje = N'El salón no existe.'; RETURN; END

    IF @DuenoId <> ISNULL(@AdminId, 0)
    BEGIN SELECT @Codigo = 2002, @Mensaje = N'Solo el administrador dueño del salón puede modificarlo.'; RETURN; END

    SET @Nombre = TRIM(@Nombre);
    SET @Zona = TRIM(@Zona);
    SET @Descripcion = NULLIF(TRIM(@Descripcion), '');
    SET @Estado = ISNULL(NULLIF(LOWER(TRIM(@Estado)), ''), @EstadoActual);

    IF ISNULL(@Nombre, '') = ''
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'El nombre del salón es obligatorio.'; RETURN; END

    IF ISNULL(@Zona, '') = ''
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'La zona es obligatoria.'; RETURN; END

    IF ISNULL(@Capacidad, 0) <= 0
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'La capacidad debe ser mayor a 0.'; RETURN; END

    IF @Estado NOT IN ('publicado', 'no_publicado')
    BEGIN SELECT @Codigo = 2004, @Mensaje = N'El estado debe ser "publicado" o "no_publicado".'; RETURN; END

    IF @Estado = 'publicado' AND NOT EXISTS (SELECT 1 FROM Paquetes WHERE SalonId = @Id)
    BEGIN SELECT @Codigo = 2003, @Mensaje = N'Para publicar el salón se requiere al menos un paquete.'; RETURN; END

    UPDATE Salones
    SET Nombre = @Nombre, Zona = @Zona, Capacidad = @Capacidad,
        Descripcion = @Descripcion, Estado = @Estado
    WHERE Id = @Id;

    SELECT @Codigo = 0, @Mensaje = N'Salón actualizado correctamente.';
END
ExecuteSqlStatement -- Paginado: primer result set = datos del paginado, segundo = registros de la página.
-- @AdminId NULL lista todos. @Pagina < 1 se toma como 1; @TamanoPagina va de 1 a 100.
CREATE OR ALTER PROCEDURE sp_Salon_Listar
    @AdminId      INT = NULL,
    @Pagina       INT = 1,
    @TamanoPagina INT = 10,
    @Codigo       INT OUTPUT,
    @Mensaje      NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @Pagina = CASE WHEN ISNULL(@Pagina, 0) < 1 THEN 1 ELSE @Pagina END;
    SET @TamanoPagina = CASE
        WHEN ISNULL(@TamanoPagina, 0) < 1 THEN 10
        WHEN @TamanoPagina > 100 THEN 100
        ELSE @TamanoPagina END;

    DECLARE @Total INT = (SELECT COUNT(*) FROM Salones WHERE @AdminId IS NULL OR AdminId = @AdminId);

    SELECT @Codigo = 0, @Mensaje = N'Consulta exitosa.';

    SELECT @Total AS TotalRegistros,
           (@Total + @TamanoPagina - 1) / @TamanoPagina AS TotalPaginas,
           @Pagina AS Pagina,
           @TamanoPagina AS TamanoPagina;

    SELECT s.Id, s.Nombre, s.Zona, s.Capacidad, s.Estado,
           (SELECT COUNT(*) FROM Paquetes p WHERE p.SalonId = s.Id) AS TotalPaquetes
    FROM Salones s
    WHERE @AdminId IS NULL OR s.AdminId = @AdminId
    ORDER BY s.Id DESC
    OFFSET (@Pagina - 1) * @TamanoPagina ROWS
    FETCH NEXT @TamanoPagina ROWS ONLY;
END
ExecuteSqlStatement -- =========================================================================
-- PAQUETES
-- =========================================================================

CREATE OR ALTER PROCEDURE sp_Paquete_Crear
    @SalonId       INT,
    @AdminId       INT,
    @NombrePaquete NVARCHAR(150),
    @Descripcion   NVARCHAR(500) = NULL,
    @Precio        DECIMAL(10,2),
    @Codigo        INT OUTPUT,
    @Mensaje       NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @DuenoId INT = (SELECT AdminId FROM Salones WHERE Id = @SalonId);

    IF @DuenoId IS NULL
    BEGIN SELECT @Codigo = 2001, @Mensaje = N'El salón no existe.'; RETURN; END

    IF @DuenoId <> ISNULL(@AdminId, 0)
    BEGIN SELECT @Codigo = 2002, @Mensaje = N'Solo el administrador dueño del salón puede modificarlo.'; RETURN; END

    SET @NombrePaquete = TRIM(@NombrePaquete);
    SET @Descripcion = NULLIF(TRIM(@Descripcion), '');

    IF ISNULL(@NombrePaquete, '') = ''
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'El nombre del paquete es obligatorio.'; RETURN; END

    IF ISNULL(@Precio, 0) <= 0
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'El precio debe ser mayor a 0.'; RETURN; END

    INSERT INTO Paquetes (SalonId, NombrePaquete, Descripcion, Precio)
    VALUES (@SalonId, @NombrePaquete, @Descripcion, @Precio);

    SELECT @Codigo = 0, @Mensaje = N'Paquete agregado correctamente.';

    SELECT Id, SalonId, NombrePaquete, Descripcion, Precio
    FROM Paquetes WHERE Id = SCOPE_IDENTITY();
END
ExecuteSqlStatement -- Descripcion NULL la borra.
CREATE OR ALTER PROCEDURE sp_Paquete_Actualizar
    @Id            INT,
    @AdminId       INT,
    @NombrePaquete NVARCHAR(150),
    @Descripcion   NVARCHAR(500) = NULL,
    @Precio        DECIMAL(10,2),
    @Codigo        INT OUTPUT,
    @Mensaje       NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @DuenoId INT = (
        SELECT s.AdminId FROM Paquetes p JOIN Salones s ON s.Id = p.SalonId WHERE p.Id = @Id);

    IF @DuenoId IS NULL
    BEGIN SELECT @Codigo = 3001, @Mensaje = N'El paquete no existe.'; RETURN; END

    IF @DuenoId <> ISNULL(@AdminId, 0)
    BEGIN SELECT @Codigo = 2002, @Mensaje = N'Solo el administrador dueño del salón puede modificarlo.'; RETURN; END

    SET @NombrePaquete = TRIM(@NombrePaquete);
    SET @Descripcion = NULLIF(TRIM(@Descripcion), '');

    IF ISNULL(@NombrePaquete, '') = ''
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'El nombre del paquete es obligatorio.'; RETURN; END

    IF ISNULL(@Precio, 0) <= 0
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'El precio debe ser mayor a 0.'; RETURN; END

    UPDATE Paquetes
    SET NombrePaquete = @NombrePaquete, Descripcion = @Descripcion, Precio = @Precio
    WHERE Id = @Id;

    SELECT @Codigo = 0, @Mensaje = N'Paquete actualizado correctamente.';

    SELECT Id, SalonId, NombrePaquete, Descripcion, Precio
    FROM Paquetes WHERE Id = @Id;
END
ExecuteSqlStatement -- No deja un salón publicado sin paquetes.
CREATE OR ALTER PROCEDURE sp_Paquete_Eliminar
    @Id      INT,
    @AdminId INT,
    @Codigo  INT OUTPUT,
    @Mensaje NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @SalonId INT, @DuenoId INT, @Estado NVARCHAR(20);
    SELECT @SalonId = s.Id, @DuenoId = s.AdminId, @Estado = s.Estado
    FROM Paquetes p JOIN Salones s ON s.Id = p.SalonId
    WHERE p.Id = @Id;

    IF @SalonId IS NULL
    BEGIN SELECT @Codigo = 3001, @Mensaje = N'El paquete no existe.'; RETURN; END

    IF @DuenoId <> ISNULL(@AdminId, 0)
    BEGIN SELECT @Codigo = 2002, @Mensaje = N'Solo el administrador dueño del salón puede modificarlo.'; RETURN; END

    IF @Estado = 'publicado' AND (SELECT COUNT(*) FROM Paquetes WHERE SalonId = @SalonId) <= 1
    BEGIN SELECT @Codigo = 3002, @Mensaje = N'Un salón publicado debe tener al menos un paquete. Despublícalo antes de borrar el último.'; RETURN; END

    DELETE FROM Paquetes WHERE Id = @Id;

    SELECT @Codigo = 0, @Mensaje = N'Paquete eliminado correctamente.';
END
ExecuteSqlStatement -- =========================================================================
-- BÚSQUEDA (Sprint 3 — HU-09)
-- =========================================================================

-- Búsqueda pública: solo salones publicados. Todos los filtros son opcionales y combinables;
-- NULL, vacío o <= 0 significa "sin filtro". Nunca regresa error: sin resultados es éxito con lista vacía.
-- Zona: coincidencia parcial sin importar mayúsculas ni acentos ("rio" encuentra "Tepeji del Río").
-- PrecioMaximo: el salón entra si su paquete más barato (PrecioDesde) cuesta eso o menos.
-- Paginado igual que sp_Salon_Listar: primer result set = datos del paginado, segundo = la página.
CREATE OR ALTER PROCEDURE sp_Salon_Buscar
    @Zona            NVARCHAR(200) = NULL,
    @CapacidadMinima INT = NULL,
    @PrecioMaximo    DECIMAL(10,2) = NULL,
    @Pagina          INT = 1,
    @TamanoPagina    INT = 10,
    @Codigo          INT OUTPUT,
    @Mensaje         NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @Pagina = CASE WHEN ISNULL(@Pagina, 0) < 1 THEN 1 ELSE @Pagina END;
    SET @TamanoPagina = CASE
        WHEN ISNULL(@TamanoPagina, 0) < 1 THEN 10
        WHEN @TamanoPagina > 100 THEN 100
        ELSE @TamanoPagina END;
    SET @Zona = NULLIF(TRIM(@Zona), '');
    IF @CapacidadMinima <= 0 SET @CapacidadMinima = NULL;
    IF @PrecioMaximo <= 0 SET @PrecioMaximo = NULL;

    SELECT s.Id, s.Nombre, s.Zona, s.Capacidad, p.PrecioDesde,
           (SELECT TOP 1 f.Url FROM FotosSalon f WHERE f.SalonId = s.Id ORDER BY f.Id) AS FotoPrincipal
    INTO #Resultados
    FROM Salones s
    CROSS APPLY (SELECT MIN(Precio) AS PrecioDesde FROM Paquetes WHERE SalonId = s.Id) p
    WHERE s.Estado = 'publicado'
      AND p.PrecioDesde IS NOT NULL
      AND (@Zona IS NULL
           OR s.Zona COLLATE Latin1_General_CI_AI LIKE N'%' + @Zona + N'%' COLLATE Latin1_General_CI_AI)
      AND (@CapacidadMinima IS NULL OR s.Capacidad >= @CapacidadMinima)
      AND (@PrecioMaximo IS NULL OR p.PrecioDesde <= @PrecioMaximo)
    -- Con filtros opcionales, RECOMPILE arma el plan con los valores reales de cada búsqueda
    -- para que SQL Server sí use los índices de 03_Indices.sql.
    OPTION (RECOMPILE);

    DECLARE @Total INT = (SELECT COUNT(*) FROM #Resultados);

    SELECT @Codigo = 0,
           @Mensaje = CASE WHEN @Total = 0 THEN N'No se encontraron salones con esos filtros.'
                           ELSE N'Consulta exitosa.' END;

    SELECT @Total AS TotalRegistros,
           (@Total + @TamanoPagina - 1) / @TamanoPagina AS TotalPaginas,
           @Pagina AS Pagina,
           @TamanoPagina AS TamanoPagina;

    SELECT Id, Nombre, Zona, Capacidad, PrecioDesde, FotoPrincipal
    FROM #Resultados
    ORDER BY PrecioDesde, Id
    OFFSET (@Pagina - 1) * @TamanoPagina ROWS
    FETCH NEXT @TamanoPagina ROWS ONLY;
END
ExecuteSqlStatement -- =========================================================================
-- DISPONIBILIDAD (Sprint 4 — HU-12)
-- =========================================================================

-- Fechas ocupadas de un salón en un mes. Solo cuentan las reservaciones 'confirmada';
-- las pendientes y canceladas no ocupan la fecha.
-- Un mes sin fechas ocupadas NO es error: éxito con result set vacío.
-- Regresa un solo result set con las fechas (FechaEvento) ordenadas.
CREATE OR ALTER PROCEDURE sp_Salon_Disponibilidad
    @SalonId INT,
    @Anio    INT,
    @Mes     INT,
    @Codigo  INT OUTPUT,
    @Mensaje NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF ISNULL(@Mes, 0) NOT BETWEEN 1 AND 12 OR ISNULL(@Anio, 0) NOT BETWEEN 2000 AND 2100
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'Mes o año inválido.'; RETURN; END

    IF NOT EXISTS (SELECT 1 FROM Salones WHERE Id = @SalonId)
    BEGIN SELECT @Codigo = 2001, @Mensaje = N'El salón no existe.'; RETURN; END

    DECLARE @Inicio DATE = DATEFROMPARTS(@Anio, @Mes, 1);
    DECLARE @Fin DATE = DATEADD(MONTH, 1, @Inicio);

    SELECT DISTINCT FechaEvento
    INTO #Fechas
    FROM Reservaciones
    WHERE SalonId = @SalonId
      AND Estado = 'confirmada'
      AND FechaEvento >= @Inicio
      AND FechaEvento < @Fin;

    SELECT @Codigo = 0,
           @Mensaje = CASE WHEN NOT EXISTS (SELECT 1 FROM #Fechas)
                           THEN N'El salón no tiene fechas ocupadas en ese mes.'
                           ELSE N'Consulta exitosa.' END;

    SELECT FechaEvento FROM #Fechas ORDER BY FechaEvento;
END
ExecuteSqlStatement /* =========================================================================
   GlamSpaces — Índices para la búsqueda de salones (Sprint 3 — HU-08)
   Correr DESPUÉS de 01_Tablas.sql y 02_StoredProcedures.sql.
   Idempotente: solo crea los índices que no existan.

   Los usa sp_Salon_Buscar (HU-09):
     - filtrado por Estado = 'publicado', zona y capacidad;
     - "precio desde" = paquete más barato de cada salón;
     - foto principal = primera foto de cada salón.
   ========================================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
ExecuteSqlStatement -- Salones publicados por capacidad, incluyendo zona y nombre.
--  - Filtrado (WHERE Estado = 'publicado'): solo guarda los salones que participan en la
--    búsqueda, así es más chico y rápido que la tabla completa.
--  - Capacidad es la llave: "capacidad mínima" hace una búsqueda directa (seek) en el índice.
--  - Zona va en INCLUDE y no como llave, porque se busca por coincidencia parcial
--    (LIKE '%tula%'), que no puede usar un índice para saltar directo. Al estar incluida,
--    el filtro de zona se resuelve dentro del índice sin leer la tabla.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Salones_Publicados_Capacidad_Zona')
    CREATE NONCLUSTERED INDEX IX_Salones_Publicados_Capacidad_Zona
        ON Salones (Capacidad)
        INCLUDE (Zona, Nombre)
        WHERE Estado = 'publicado';
ExecuteSqlStatement -- Paquetes por salón y precio: el precio más bajo de cada salón (MIN) es la primera fila
-- del índice para ese SalonId. Además acelera el FK Paquetes → Salones.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Paquetes_SalonId_Precio')
    CREATE NONCLUSTERED INDEX IX_Paquetes_SalonId_Precio
        ON Paquetes (SalonId, Precio);
ExecuteSqlStatement -- Fotos por salón: la primera foto (menor Id) sale directo del índice.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FotosSalon_SalonId')
    CREATE NONCLUSTERED INDEX IX_FotosSalon_SalonId
        ON FotosSalon (SalonId)
        INCLUDE (Url);
ExecuteSqlStatement -- =========================================================================
-- Sprint 4 — Reservaciones (HU-11 / HU-12)
-- =========================================================================

-- Disponibilidad del salón por mes (sp_Salon_Disponibilidad): busca por salón y rango
-- de fechas; Estado va incluido para filtrar las confirmadas sin leer la tabla.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Reservaciones_Salon_Fecha')
    CREATE NONCLUSTERED INDEX IX_Reservaciones_Salon_Fecha
        ON Reservaciones (SalonId, FechaEvento)
        INCLUDE (Estado);
ExecuteSqlStatement -- Regla de negocio: un salón no puede tener 2 reservaciones confirmadas el mismo día.
-- Único y filtrado: las pendientes y canceladas sí pueden repetir fecha.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Reservaciones_Confirmada')
    CREATE UNIQUE NONCLUSTERED INDEX UX_Reservaciones_Confirmada
        ON Reservaciones (SalonId, FechaEvento)
        WHERE Estado = 'confirmada';
Committing Transaction
202610230900: M001_LineaBase migrated
-------------------------------------------------------------------------------
202610230910: M002_CrearTablaBusquedasFavoritas migrating
-------------------------------------------------------------------------------
Beginning Transaction
CreateTable BusquedasFavoritas
CreateForeignKey FK_BusquedasFavoritas_Usuarios BusquedasFavoritas(UsuarioId) Usuarios(Id)
CreateForeignKey FK_BusquedasFavoritas_Salones BusquedasFavoritas(SalonId) Salones(Id)
Committing Transaction
202610230910: M002_CrearTablaBusquedasFavoritas migrated
-------------------------------------------------------------------------------
202610230920: M003_AgregarColumnaTelefonoSalones migrating
-------------------------------------------------------------------------------
Beginning Transaction
AlterTable Salones
CreateColumn Salones Telefono String
Committing Transaction
202610230920: M003_AgregarColumnaTelefonoSalones migrated
-------------------------------------------------------------------------------
202610230930: M004_IndiceUnicoBusquedasFavoritas migrating
-------------------------------------------------------------------------------
Beginning Transaction
CreateIndex BusquedasFavoritas (UsuarioId, SalonId)
Committing Transaction
202610230930: M004_IndiceUnicoBusquedasFavoritas migrated
Task completed.
# Código de salida: 0
```

</details>

### Después de aplicar: todas aplicadas — [`03_list_despues.txt`](evidencias/03_list_despues.txt)

```text
# Evidencia 03 — Migraciones después de aplicar (todas aplicadas)
# Fecha: 2026-10-23 22:05:23 (hora local)
# Servidor / BD: (localdb)\MSSQLLocalDB / GlamSpacesMigraciones
# Comando: .\scripts\migraciones.ps1 listar   (dotnet tool run dotnet-fm -- list migrations)
#
-------------------------------------------------------------------------------
Migrations
-------------------------------------------------------------------------------
202610230900: M001_LineaBase
202610230910: M002_CrearTablaBusquedasFavoritas
202610230920: M003_AgregarColumnaTelefonoSalones
⌘ 202610230930: M004_IndiceUnicoBusquedasFavoritas (current)
Task completed.
# Código de salida: 0
```

> 📸 **CAPTURA AQUÍ:** terminal con `.\scripts\migraciones.ps1 listar` antes y después de aplicar.

---

## 7. Reversión controlada (punto 12)

Reglas: **solo en desarrollo/pruebas** y **siempre con respaldo previo**.

### Paso 1: respaldo — [`04_respaldo.txt`](evidencias/04_respaldo.txt)

`.\scripts\migraciones.ps1 respaldar` hace `BACKUP DATABASE ... WITH INIT, COPY_ONLY, CHECKSUM` a
`respaldos/<bd>_<fecha>.bak` y lo comprueba con `RESTORE VERIFYONLY`. Los `.bak` están en `.gitignore`.

```text
# Evidencia 04 — Respaldo (BACKUP DATABASE) de la BD de pruebas antes del rollback
# Fecha: 2026-10-23 22:07:40 (hora local)
# Servidor / BD: (localdb)\MSSQLLocalDB / GlamSpacesMigraciones
# Comando: .\scripts\migraciones.ps1 respaldar
#
Respaldando [GlamSpacesMigraciones] en C:\Trabajos ADRIAN\trabajos escolares\TrabajoAplicacionesWeb\Backend\GlamSpace-Back\respaldos\GlamSpacesMigraciones_20261023_220741.bak
Processed 448 pages for database 'GlamSpacesMigraciones', file 'GlamSpacesMigraciones' on file 1.
Processed 1 pages for database 'GlamSpacesMigraciones', file 'GlamSpacesMigraciones_log' on file 1.
BACKUP DATABASE successfully processed 449 pages in 0.244 seconds (14.350 MB/sec).
The backup set on file 1 is valid.
Archivo: C:\Trabajos ADRIAN\trabajos escolares\TrabajoAplicacionesWeb\Backend\GlamSpace-Back\respaldos\GlamSpacesMigraciones_20261023_220741.bak (3,673 KB)
# Código de salida: 0
```

Si algo saliera mal, se restaura con:

```sql
RESTORE DATABASE [GlamSpacesMigraciones]
FROM DISK = N'<ruta>\respaldos\GlamSpacesMigraciones_<fecha>.bak'
WITH REPLACE;
```

### Paso 2: rollback de la última migración y verificación — [`05_rollback.txt`](evidencias/05_rollback.txt)

`.\scripts\migraciones.ps1 rollback` → `dotnet-fm rollback ... by 1`. Ejecuta el `Down()` de la 004
(borra el índice único) y quita su fila de `VersionInfo`. La consulta posterior confirma que el índice
**ya no existe** (en la tabla solo queda la llave primaria).

```text
# Evidencia 05 — Reversión controlada: rollback de la última migración (004, índice único)
# Fecha: 2026-10-23 22:09:38 (hora local)
# Servidor / BD: (localdb)\MSSQLLocalDB / GlamSpacesMigraciones
# Comando: .\scripts\migraciones.ps1 rollback   (dotnet tool run dotnet-fm -- rollback -p SqlServer2016 -c <conexión> -a <dll> by 1)
#
-------------------------------------------------------------------------------
202610230930: M004_IndiceUnicoBusquedasFavoritas reverting
-------------------------------------------------------------------------------
Beginning Transaction
DeleteIndex BusquedasFavoritas ()
Committing Transaction
202610230930: M004_IndiceUnicoBusquedasFavoritas reverted
Task completed.

# Verificación: índices de BusquedasFavoritas después del rollback
# Comando: .\scripts\migraciones.ps1 consultar -Sql "SELECT i.name AS indice, i.is_unique AS es_unico FROM sys.indexes i WHERE i.object_id = OBJECT_ID('dbo.BusquedasFavoritas') AND i.name IS NOT NULL; SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_BusquedasFavoritas_Usuario_Salon') THEN 'EXISTE' ELSE 'NO EXISTE' END AS UX_BusquedasFavoritas_Usuario_Salon"

indice                es_unico
------                --------
PK_BusquedasFavoritas     True
(1 filas)

UX_BusquedasFavoritas_Usuario_Salon
-----------------------------------
NO EXISTE
(1 filas)
# Código de salida: 0
```

### Paso 3: la migración revertida vuelve a aparecer como pendiente — [`06_list_tras_rollback.txt`](evidencias/06_list_tras_rollback.txt)

```text
# Evidencia 06 — Migraciones después del rollback (la 004 vuelve a estar pendiente)
# Fecha: 2026-10-23 22:09:55 (hora local)
# Servidor / BD: (localdb)\MSSQLLocalDB / GlamSpacesMigraciones
# Comando: .\scripts\migraciones.ps1 listar   (dotnet tool run dotnet-fm -- list migrations)
#
-------------------------------------------------------------------------------
Migrations
-------------------------------------------------------------------------------
202610230900: M001_LineaBase
202610230910: M002_CrearTablaBusquedasFavoritas
⌘ 202610230920: M003_AgregarColumnaTelefonoSalones (current)
202610230930: M004_IndiceUnicoBusquedasFavoritas (not applied)
Task completed.
# Código de salida: 0
```

> 📸 **CAPTURA AQUÍ:** rollback + consulta mostrando `NO EXISTE`, y la lista con la 004 en `(not applied)`.

---

## 8. Reaplicación (punto 13)

`.\scripts\migraciones.ps1 reaplicar` → `migrate up` (aplica solo lo pendiente: la 004) y luego lista.
La consulta confirma que el índice **volvió**, sobre `UsuarioId, SalonId`.
Las líneas `Beginning Transaction` / `Rolling back transaction` del inicio son de la revisión interna
que hace FluentMigrator antes de migrar; no revierten nada.

### [`07_reaplicar.txt`](evidencias/07_reaplicar.txt)

```text
# Evidencia 07 — Reaplicar la migración revertida (004) y verificar que el índice volvió
# Fecha: 2026-10-23 22:09:58 (hora local)
# Servidor / BD: (localdb)\MSSQLLocalDB / GlamSpacesMigraciones
# Comando: .\scripts\migraciones.ps1 reaplicar   (migrate up + list migrations)
#
Beginning Transaction
Rolling back transaction
-------------------------------------------------------------------------------
202610230930: M004_IndiceUnicoBusquedasFavoritas migrating
-------------------------------------------------------------------------------
Beginning Transaction
CreateIndex BusquedasFavoritas (UsuarioId, SalonId)
Committing Transaction
202610230930: M004_IndiceUnicoBusquedasFavoritas migrated
Task completed.
-------------------------------------------------------------------------------
Migrations
-------------------------------------------------------------------------------
202610230900: M001_LineaBase
202610230910: M002_CrearTablaBusquedasFavoritas
202610230920: M003_AgregarColumnaTelefonoSalones
⌘ 202610230930: M004_IndiceUnicoBusquedasFavoritas (current)
Task completed.

# Verificación: el índice único volvió
# Comando: .\scripts\migraciones.ps1 consultar -Sql "SELECT i.name AS indice, i.is_unique AS es_unico, STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS columnas FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id WHERE i.object_id = OBJECT_ID('dbo.BusquedasFavoritas') GROUP BY i.name, i.is_unique; SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_BusquedasFavoritas_Usuario_Salon') THEN 'EXISTE' ELSE 'NO EXISTE' END AS UX_BusquedasFavoritas_Usuario_Salon"

indice                              es_unico columnas          
------                              -------- --------          
PK_BusquedasFavoritas                   True Id                
UX_BusquedasFavoritas_Usuario_Salon     True UsuarioId, SalonId
(2 filas)

UX_BusquedasFavoritas_Usuario_Salon
-----------------------------------
EXISTE
(1 filas)
# Código de salida: 0
```

> 📸 **CAPTURA AQUÍ:** reaplicación + consulta mostrando `EXISTE`.

---

## 9. Cómo ejecutar todo desde una base limpia (punto 13)

### Requisitos

- Windows con **.NET SDK 9** o superior y **SQL Server Express LocalDB** (viene con Visual Studio).
- Windows PowerShell 5.1 (incluido en Windows) para `scripts/migraciones.ps1`.

### Pasos

```powershell
# 1. Clonar y entrar al repo
git clone https://github.com/4DRIAN-G4RCI4/GlamSpace-Back.git
cd GlamSpace-Back
git checkout develop

# 2. Restaurar la herramienta dotnet-fm (lee .config/dotnet-tools.json)
dotnet tool restore

# 3. Reset limpio: borra y crea la BD GlamSpacesMigraciones en LocalDB y corre TODAS las migraciones
.\scripts\migraciones.ps1 reset

# 4. Confirmar que todas quedaron aplicadas
.\scripts\migraciones.ps1 listar

# 5. (Opcional) ver el historial
.\scripts\migraciones.ps1 consultar -Sql "SELECT Version, AppliedOn, Description FROM dbo.VersionInfo ORDER BY Version"
```

Si la ejecución de scripts está bloqueada en tu equipo:
`powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\migraciones.ps1 reset`.

Para otra BD de pruebas (por ejemplo, un SQL Server en Docker):
`$env:GLAMSPACES_CONN = "Server=localhost,1433;Database=GlamSpacesMigraciones;User Id=sa;Password=<tu contraseña>;TrustServerCertificate=True"`.
El `reset` solo se permite en LocalDB; con otra BD hay que crearla vacía y usar `subir`.

### Reset limpio desde cero — [`08_base_limpia.txt`](evidencias/08_base_limpia.txt)

La verificación al final confirma que la BD nueva quedó igual que producción: 5 tablas del proyecto
(más `BusquedasFavoritas` de la 002), `Usuarios` con **0 `DEFAULT`** como la creó EF, los **12 stored
procedures**, los 5 índices de producción (con sus filtros), `IX_Usuarios_Correo`, el índice único nuevo
y la columna `Telefono NVARCHAR(20) NULL`.

<details>
<summary>Salida completa (clic para expandir)</summary>

```text
# Evidencia 08 — Reset limpio: BD nueva y corrida completa de todas las migraciones desde cero
# Fecha: 2026-10-23 22:10:54 (hora local)
# Servidor / BD: (localdb)\MSSQLLocalDB / GlamSpacesMigraciones
# Comando: .\scripts\migraciones.ps1 reset   (DROP/CREATE DATABASE en LocalDB + migrate up)
#
Borrando y recreando [GlamSpacesMigraciones] en (localdb)\MSSQLLocalDB
Base de datos [GlamSpacesMigraciones] creada vacía.
Beginning Transaction
Rolling back transaction
-------------------------------------------------------------------------------
VersionMigration migrating
-------------------------------------------------------------------------------
Beginning Transaction
CreateTable VersionInfo
Committing Transaction
VersionMigration migrated
-------------------------------------------------------------------------------
VersionUniqueMigration migrating
-------------------------------------------------------------------------------
Beginning Transaction
CreateIndex VersionInfo (Version)
AlterTable VersionInfo
CreateColumn VersionInfo AppliedOn DateTime
Committing Transaction
VersionUniqueMigration migrated
-------------------------------------------------------------------------------
VersionDescriptionMigration migrating
-------------------------------------------------------------------------------
Beginning Transaction
AlterTable VersionInfo
CreateColumn VersionInfo Description String
Committing Transaction
VersionDescriptionMigration migrated
-------------------------------------------------------------------------------
202610230900: M001_LineaBase migrating
-------------------------------------------------------------------------------
Beginning Transaction
ExecuteSqlStatement 
IF OBJECT_ID(N'dbo.Usuarios', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Usuarios (
        Id             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Usuarios PRIMARY KEY,
        NombreCompleto NVARCHAR(150) NOT NULL,
        Correo         NVARCHAR(150) NOT NULL,
        PasswordHash   NVARCHAR(MAX) NOT NULL,
        TipoCuenta     NVARCHAR(20)  NOT NULL,
        NombreSalon    NVARCHAR(150) NULL,
        FechaRegistro  DATETIME2     NOT NULL
    );
    CREATE UNIQUE INDEX IX_Usuarios_Correo ON dbo.Usuarios (Correo);
END
ExecuteSqlStatement /* =========================================================================
   GlamSpaces — Tablas (glamspaces-db, Azure SQL)
   Idempotente: se puede volver a correr sin romper nada si ya existen.
   Correr ANTES de 02_StoredProcedures.sql.
   ========================================================================= */

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Usuarios')
BEGIN
    CREATE TABLE Usuarios (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        NombreCompleto  NVARCHAR(150) NOT NULL,
        Correo          NVARCHAR(150) NOT NULL UNIQUE,
        PasswordHash    NVARCHAR(MAX) NOT NULL,
        TipoCuenta      NVARCHAR(20) NOT NULL,   -- 'cliente' o 'administrador'
        NombreSalon     NVARCHAR(150) NULL,      -- solo si TipoCuenta = 'administrador'
        FechaRegistro   DATETIME2 NOT NULL DEFAULT GETDATE()
    );
END
ExecuteSqlStatement IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Salones')
BEGIN
    CREATE TABLE Salones (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        AdminId       INT NOT NULL,
        Nombre        NVARCHAR(150) NOT NULL,
        Zona          NVARCHAR(200) NOT NULL,
        Capacidad     INT NOT NULL,
        Descripcion   NVARCHAR(1000) NULL,
        Estado        NVARCHAR(20) NOT NULL DEFAULT 'no_publicado',
        FechaCreacion DATETIME2 NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_Salones_Usuarios FOREIGN KEY (AdminId) REFERENCES Usuarios(Id),
        CONSTRAINT CK_Salones_Capacidad CHECK (Capacidad > 0),
        CONSTRAINT CK_Salones_Estado CHECK (Estado IN ('publicado', 'no_publicado'))
    );
END
ExecuteSqlStatement IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Paquetes')
BEGIN
    CREATE TABLE Paquetes (
        Id              INT IDENTITY(1,1) PRIMARY KEY,
        SalonId         INT NOT NULL,
        NombrePaquete   NVARCHAR(150) NOT NULL,
        Descripcion     NVARCHAR(500) NULL,
        Precio          DECIMAL(10,2) NOT NULL,
        CONSTRAINT FK_Paquetes_Salones FOREIGN KEY (SalonId) REFERENCES Salones(Id),
        CONSTRAINT CK_Paquetes_Precio CHECK (Precio > 0)
    );
END
ExecuteSqlStatement IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'FotosSalon')
BEGIN
    CREATE TABLE FotosSalon (
        Id      INT IDENTITY(1,1) PRIMARY KEY,
        SalonId INT NOT NULL,
        Url     NVARCHAR(500) NOT NULL,
        CONSTRAINT FK_FotosSalon_Salones FOREIGN KEY (SalonId) REFERENCES Salones(Id)
    );
END
ExecuteSqlStatement -- Sprint 4 — HU-11. Solo las reservaciones 'confirmada' ocupan la fecha del salón (HU-12).
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Reservaciones')
BEGIN
    CREATE TABLE Reservaciones (
        Id            INT IDENTITY(1,1) PRIMARY KEY,
        SalonId       INT NOT NULL,
        ClienteId     INT NOT NULL,
        PaqueteId     INT NULL,
        FechaEvento   DATE NOT NULL,
        Estado        NVARCHAR(20) NOT NULL DEFAULT 'pendiente',
        FechaCreacion DATETIME2 NOT NULL DEFAULT GETDATE(),
        CONSTRAINT FK_Reservaciones_Salones FOREIGN KEY (SalonId) REFERENCES Salones(Id),
        CONSTRAINT FK_Reservaciones_Usuarios FOREIGN KEY (ClienteId) REFERENCES Usuarios(Id),
        CONSTRAINT FK_Reservaciones_Paquetes FOREIGN KEY (PaqueteId) REFERENCES Paquetes(Id),
        CONSTRAINT CK_Reservaciones_Estado CHECK (Estado IN ('pendiente', 'confirmada', 'cancelada'))
    );
END
ExecuteSqlStatement /* =========================================================================
   GlamSpaces — Stored procedures (glamspaces-db, Azure SQL)
   Correr DESPUÉS de 01_Tablas.sql. Idempotente (CREATE OR ALTER).

   Convención de todos los SP:
     - Reciben @Codigo INT OUTPUT y @Mensaje NVARCHAR(200) OUTPUT.
     - @Codigo = 0 es éxito; los demás números son los de
       GlamSpaces.Domain/Comun/CodigosError.cs.
     - Si hay error, hacen RETURN sin regresar result sets.
   ========================================================================= */

-- Los SP guardan estas opciones al crearse. Deben estar en ON porque Salones tiene
-- índices filtrados (03_Indices.sql); con OFF, los INSERT/UPDATE de los SP fallarían.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
ExecuteSqlStatement -- =========================================================================
-- USUARIOS
-- =========================================================================

-- La API manda la contraseña ya hasheada (PBKDF2); el SP nunca ve el texto plano.
CREATE OR ALTER PROCEDURE sp_Usuario_Registrar
    @NombreCompleto NVARCHAR(150),
    @Correo         NVARCHAR(150),
    @PasswordHash   NVARCHAR(MAX),
    @TipoCuenta     NVARCHAR(20),
    @NombreSalon    NVARCHAR(150) = NULL,
    @Codigo         INT OUTPUT,
    @Mensaje        NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @NombreCompleto = TRIM(@NombreCompleto);
    SET @Correo = LOWER(TRIM(@Correo));
    SET @TipoCuenta = LOWER(TRIM(@TipoCuenta));
    SET @NombreSalon = NULLIF(TRIM(@NombreSalon), '');

    IF ISNULL(@NombreCompleto, '') = ''
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'Escribe tu nombre completo.'; RETURN; END

    IF ISNULL(@Correo, '') NOT LIKE '_%@_%._%'
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'El formato del correo no es válido.'; RETURN; END

    IF ISNULL(@TipoCuenta, '') NOT IN ('cliente', 'administrador')
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'El tipo de cuenta debe ser "cliente" o "administrador".'; RETURN; END

    IF @TipoCuenta = 'administrador' AND @NombreSalon IS NULL
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'Escribe el nombre de tu salón.'; RETURN; END

    IF EXISTS (SELECT 1 FROM Usuarios WHERE Correo = @Correo)
    BEGIN SELECT @Codigo = 1002, @Mensaje = N'Ya existe una cuenta con este correo.'; RETURN; END

    INSERT INTO Usuarios (NombreCompleto, Correo, PasswordHash, TipoCuenta, NombreSalon, FechaRegistro)
    VALUES (@NombreCompleto, @Correo, @PasswordHash, @TipoCuenta,
            CASE WHEN @TipoCuenta = 'administrador' THEN @NombreSalon END, GETDATE());

    SELECT @Codigo = 0, @Mensaje = N'Cuenta creada correctamente.';

    SELECT Id, NombreCompleto, Correo, TipoCuenta, NombreSalon, FechaRegistro
    FROM Usuarios WHERE Id = SCOPE_IDENTITY();
END
ExecuteSqlStatement -- Para el login: regresa el hash y la API lo verifica (el SP no puede verificar PBKDF2).
CREATE OR ALTER PROCEDURE sp_Usuario_ObtenerPorCorreo
    @Correo  NVARCHAR(150),
    @Codigo  INT OUTPUT,
    @Mensaje NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT @Codigo = 0, @Mensaje = N'Consulta exitosa.';

    SELECT Id, NombreCompleto, Correo, PasswordHash, TipoCuenta, NombreSalon, FechaRegistro
    FROM Usuarios WHERE Correo = LOWER(TRIM(@Correo));
END
ExecuteSqlStatement CREATE OR ALTER PROCEDURE sp_Usuario_Obtener
    @Id      INT,
    @Codigo  INT OUTPUT,
    @Mensaje NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Id = @Id)
    BEGIN SELECT @Codigo = 1005, @Mensaje = N'El usuario no existe.'; RETURN; END

    SELECT @Codigo = 0, @Mensaje = N'Consulta exitosa.';

    SELECT Id, NombreCompleto, Correo, TipoCuenta, NombreSalon, FechaRegistro
    FROM Usuarios WHERE Id = @Id;
END
ExecuteSqlStatement -- =========================================================================
-- SALONES
-- =========================================================================

CREATE OR ALTER PROCEDURE sp_Salon_Crear
    @AdminId     INT,
    @Nombre      NVARCHAR(150),
    @Zona        NVARCHAR(200),
    @Capacidad   INT,
    @Descripcion NVARCHAR(1000) = NULL,
    @Codigo      INT OUTPUT,
    @Mensaje     NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @Nombre = TRIM(@Nombre);
    SET @Zona = TRIM(@Zona);
    SET @Descripcion = NULLIF(TRIM(@Descripcion), '');

    IF ISNULL(@Nombre, '') = ''
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'El nombre del salón es obligatorio.'; RETURN; END

    IF ISNULL(@Zona, '') = ''
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'La zona es obligatoria.'; RETURN; END

    IF ISNULL(@Capacidad, 0) <= 0
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'La capacidad debe ser mayor a 0.'; RETURN; END

    IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Id = @AdminId AND TipoCuenta = 'administrador')
    BEGIN SELECT @Codigo = 1004, @Mensaje = N'El usuario no existe o no es administrador.'; RETURN; END

    -- Siempre nace "no_publicado": todavía no tiene paquetes.
    INSERT INTO Salones (AdminId, Nombre, Zona, Capacidad, Descripcion, Estado, FechaCreacion)
    VALUES (@AdminId, @Nombre, @Zona, @Capacidad, @Descripcion, 'no_publicado', GETDATE());

    SELECT @Codigo = 0, @Mensaje = N'Salón creado correctamente.';

    SELECT Id, AdminId, Nombre, Zona, Capacidad, Descripcion, Estado, FechaCreacion
    FROM Salones WHERE Id = SCOPE_IDENTITY();
END
ExecuteSqlStatement -- Regresa SIEMPRE 3 result sets (salón, paquetes, fotos), vacíos si el salón no existe,
-- para que la API los pueda leer en orden.
CREATE OR ALTER PROCEDURE sp_Salon_Obtener
    @Id      INT,
    @Codigo  INT OUTPUT,
    @Mensaje NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM Salones WHERE Id = @Id)
        SELECT @Codigo = 0, @Mensaje = N'Consulta exitosa.';
    ELSE
        SELECT @Codigo = 2001, @Mensaje = N'El salón no existe.';

    SELECT Id, AdminId, Nombre, Zona, Capacidad, Descripcion, Estado, FechaCreacion
    FROM Salones WHERE Id = @Id;

    SELECT Id, SalonId, NombrePaquete, Descripcion, Precio
    FROM Paquetes WHERE SalonId = @Id ORDER BY Id;

    SELECT Id, SalonId, Url
    FROM FotosSalon WHERE SalonId = @Id ORDER BY Id;
END
ExecuteSqlStatement -- Si @Estado viene NULL se conserva el actual. Descripcion NULL la borra.
CREATE OR ALTER PROCEDURE sp_Salon_Actualizar
    @Id          INT,
    @AdminId     INT,
    @Nombre      NVARCHAR(150),
    @Zona        NVARCHAR(200),
    @Capacidad   INT,
    @Descripcion NVARCHAR(1000) = NULL,
    @Estado      NVARCHAR(20) = NULL,
    @Codigo      INT OUTPUT,
    @Mensaje     NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @DuenoId INT, @EstadoActual NVARCHAR(20);
    SELECT @DuenoId = AdminId, @EstadoActual = Estado FROM Salones WHERE Id = @Id;

    IF @DuenoId IS NULL
    BEGIN SELECT @Codigo = 2001, @Mensaje = N'El salón no existe.'; RETURN; END

    IF @DuenoId <> ISNULL(@AdminId, 0)
    BEGIN SELECT @Codigo = 2002, @Mensaje = N'Solo el administrador dueño del salón puede modificarlo.'; RETURN; END

    SET @Nombre = TRIM(@Nombre);
    SET @Zona = TRIM(@Zona);
    SET @Descripcion = NULLIF(TRIM(@Descripcion), '');
    SET @Estado = ISNULL(NULLIF(LOWER(TRIM(@Estado)), ''), @EstadoActual);

    IF ISNULL(@Nombre, '') = ''
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'El nombre del salón es obligatorio.'; RETURN; END

    IF ISNULL(@Zona, '') = ''
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'La zona es obligatoria.'; RETURN; END

    IF ISNULL(@Capacidad, 0) <= 0
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'La capacidad debe ser mayor a 0.'; RETURN; END

    IF @Estado NOT IN ('publicado', 'no_publicado')
    BEGIN SELECT @Codigo = 2004, @Mensaje = N'El estado debe ser "publicado" o "no_publicado".'; RETURN; END

    IF @Estado = 'publicado' AND NOT EXISTS (SELECT 1 FROM Paquetes WHERE SalonId = @Id)
    BEGIN SELECT @Codigo = 2003, @Mensaje = N'Para publicar el salón se requiere al menos un paquete.'; RETURN; END

    UPDATE Salones
    SET Nombre = @Nombre, Zona = @Zona, Capacidad = @Capacidad,
        Descripcion = @Descripcion, Estado = @Estado
    WHERE Id = @Id;

    SELECT @Codigo = 0, @Mensaje = N'Salón actualizado correctamente.';
END
ExecuteSqlStatement -- Paginado: primer result set = datos del paginado, segundo = registros de la página.
-- @AdminId NULL lista todos. @Pagina < 1 se toma como 1; @TamanoPagina va de 1 a 100.
CREATE OR ALTER PROCEDURE sp_Salon_Listar
    @AdminId      INT = NULL,
    @Pagina       INT = 1,
    @TamanoPagina INT = 10,
    @Codigo       INT OUTPUT,
    @Mensaje      NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @Pagina = CASE WHEN ISNULL(@Pagina, 0) < 1 THEN 1 ELSE @Pagina END;
    SET @TamanoPagina = CASE
        WHEN ISNULL(@TamanoPagina, 0) < 1 THEN 10
        WHEN @TamanoPagina > 100 THEN 100
        ELSE @TamanoPagina END;

    DECLARE @Total INT = (SELECT COUNT(*) FROM Salones WHERE @AdminId IS NULL OR AdminId = @AdminId);

    SELECT @Codigo = 0, @Mensaje = N'Consulta exitosa.';

    SELECT @Total AS TotalRegistros,
           (@Total + @TamanoPagina - 1) / @TamanoPagina AS TotalPaginas,
           @Pagina AS Pagina,
           @TamanoPagina AS TamanoPagina;

    SELECT s.Id, s.Nombre, s.Zona, s.Capacidad, s.Estado,
           (SELECT COUNT(*) FROM Paquetes p WHERE p.SalonId = s.Id) AS TotalPaquetes
    FROM Salones s
    WHERE @AdminId IS NULL OR s.AdminId = @AdminId
    ORDER BY s.Id DESC
    OFFSET (@Pagina - 1) * @TamanoPagina ROWS
    FETCH NEXT @TamanoPagina ROWS ONLY;
END
ExecuteSqlStatement -- =========================================================================
-- PAQUETES
-- =========================================================================

CREATE OR ALTER PROCEDURE sp_Paquete_Crear
    @SalonId       INT,
    @AdminId       INT,
    @NombrePaquete NVARCHAR(150),
    @Descripcion   NVARCHAR(500) = NULL,
    @Precio        DECIMAL(10,2),
    @Codigo        INT OUTPUT,
    @Mensaje       NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @DuenoId INT = (SELECT AdminId FROM Salones WHERE Id = @SalonId);

    IF @DuenoId IS NULL
    BEGIN SELECT @Codigo = 2001, @Mensaje = N'El salón no existe.'; RETURN; END

    IF @DuenoId <> ISNULL(@AdminId, 0)
    BEGIN SELECT @Codigo = 2002, @Mensaje = N'Solo el administrador dueño del salón puede modificarlo.'; RETURN; END

    SET @NombrePaquete = TRIM(@NombrePaquete);
    SET @Descripcion = NULLIF(TRIM(@Descripcion), '');

    IF ISNULL(@NombrePaquete, '') = ''
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'El nombre del paquete es obligatorio.'; RETURN; END

    IF ISNULL(@Precio, 0) <= 0
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'El precio debe ser mayor a 0.'; RETURN; END

    INSERT INTO Paquetes (SalonId, NombrePaquete, Descripcion, Precio)
    VALUES (@SalonId, @NombrePaquete, @Descripcion, @Precio);

    SELECT @Codigo = 0, @Mensaje = N'Paquete agregado correctamente.';

    SELECT Id, SalonId, NombrePaquete, Descripcion, Precio
    FROM Paquetes WHERE Id = SCOPE_IDENTITY();
END
ExecuteSqlStatement -- Descripcion NULL la borra.
CREATE OR ALTER PROCEDURE sp_Paquete_Actualizar
    @Id            INT,
    @AdminId       INT,
    @NombrePaquete NVARCHAR(150),
    @Descripcion   NVARCHAR(500) = NULL,
    @Precio        DECIMAL(10,2),
    @Codigo        INT OUTPUT,
    @Mensaje       NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @DuenoId INT = (
        SELECT s.AdminId FROM Paquetes p JOIN Salones s ON s.Id = p.SalonId WHERE p.Id = @Id);

    IF @DuenoId IS NULL
    BEGIN SELECT @Codigo = 3001, @Mensaje = N'El paquete no existe.'; RETURN; END

    IF @DuenoId <> ISNULL(@AdminId, 0)
    BEGIN SELECT @Codigo = 2002, @Mensaje = N'Solo el administrador dueño del salón puede modificarlo.'; RETURN; END

    SET @NombrePaquete = TRIM(@NombrePaquete);
    SET @Descripcion = NULLIF(TRIM(@Descripcion), '');

    IF ISNULL(@NombrePaquete, '') = ''
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'El nombre del paquete es obligatorio.'; RETURN; END

    IF ISNULL(@Precio, 0) <= 0
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'El precio debe ser mayor a 0.'; RETURN; END

    UPDATE Paquetes
    SET NombrePaquete = @NombrePaquete, Descripcion = @Descripcion, Precio = @Precio
    WHERE Id = @Id;

    SELECT @Codigo = 0, @Mensaje = N'Paquete actualizado correctamente.';

    SELECT Id, SalonId, NombrePaquete, Descripcion, Precio
    FROM Paquetes WHERE Id = @Id;
END
ExecuteSqlStatement -- No deja un salón publicado sin paquetes.
CREATE OR ALTER PROCEDURE sp_Paquete_Eliminar
    @Id      INT,
    @AdminId INT,
    @Codigo  INT OUTPUT,
    @Mensaje NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @SalonId INT, @DuenoId INT, @Estado NVARCHAR(20);
    SELECT @SalonId = s.Id, @DuenoId = s.AdminId, @Estado = s.Estado
    FROM Paquetes p JOIN Salones s ON s.Id = p.SalonId
    WHERE p.Id = @Id;

    IF @SalonId IS NULL
    BEGIN SELECT @Codigo = 3001, @Mensaje = N'El paquete no existe.'; RETURN; END

    IF @DuenoId <> ISNULL(@AdminId, 0)
    BEGIN SELECT @Codigo = 2002, @Mensaje = N'Solo el administrador dueño del salón puede modificarlo.'; RETURN; END

    IF @Estado = 'publicado' AND (SELECT COUNT(*) FROM Paquetes WHERE SalonId = @SalonId) <= 1
    BEGIN SELECT @Codigo = 3002, @Mensaje = N'Un salón publicado debe tener al menos un paquete. Despublícalo antes de borrar el último.'; RETURN; END

    DELETE FROM Paquetes WHERE Id = @Id;

    SELECT @Codigo = 0, @Mensaje = N'Paquete eliminado correctamente.';
END
ExecuteSqlStatement -- =========================================================================
-- BÚSQUEDA (Sprint 3 — HU-09)
-- =========================================================================

-- Búsqueda pública: solo salones publicados. Todos los filtros son opcionales y combinables;
-- NULL, vacío o <= 0 significa "sin filtro". Nunca regresa error: sin resultados es éxito con lista vacía.
-- Zona: coincidencia parcial sin importar mayúsculas ni acentos ("rio" encuentra "Tepeji del Río").
-- PrecioMaximo: el salón entra si su paquete más barato (PrecioDesde) cuesta eso o menos.
-- Paginado igual que sp_Salon_Listar: primer result set = datos del paginado, segundo = la página.
CREATE OR ALTER PROCEDURE sp_Salon_Buscar
    @Zona            NVARCHAR(200) = NULL,
    @CapacidadMinima INT = NULL,
    @PrecioMaximo    DECIMAL(10,2) = NULL,
    @Pagina          INT = 1,
    @TamanoPagina    INT = 10,
    @Codigo          INT OUTPUT,
    @Mensaje         NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @Pagina = CASE WHEN ISNULL(@Pagina, 0) < 1 THEN 1 ELSE @Pagina END;
    SET @TamanoPagina = CASE
        WHEN ISNULL(@TamanoPagina, 0) < 1 THEN 10
        WHEN @TamanoPagina > 100 THEN 100
        ELSE @TamanoPagina END;
    SET @Zona = NULLIF(TRIM(@Zona), '');
    IF @CapacidadMinima <= 0 SET @CapacidadMinima = NULL;
    IF @PrecioMaximo <= 0 SET @PrecioMaximo = NULL;

    SELECT s.Id, s.Nombre, s.Zona, s.Capacidad, p.PrecioDesde,
           (SELECT TOP 1 f.Url FROM FotosSalon f WHERE f.SalonId = s.Id ORDER BY f.Id) AS FotoPrincipal
    INTO #Resultados
    FROM Salones s
    CROSS APPLY (SELECT MIN(Precio) AS PrecioDesde FROM Paquetes WHERE SalonId = s.Id) p
    WHERE s.Estado = 'publicado'
      AND p.PrecioDesde IS NOT NULL
      AND (@Zona IS NULL
           OR s.Zona COLLATE Latin1_General_CI_AI LIKE N'%' + @Zona + N'%' COLLATE Latin1_General_CI_AI)
      AND (@CapacidadMinima IS NULL OR s.Capacidad >= @CapacidadMinima)
      AND (@PrecioMaximo IS NULL OR p.PrecioDesde <= @PrecioMaximo)
    -- Con filtros opcionales, RECOMPILE arma el plan con los valores reales de cada búsqueda
    -- para que SQL Server sí use los índices de 03_Indices.sql.
    OPTION (RECOMPILE);

    DECLARE @Total INT = (SELECT COUNT(*) FROM #Resultados);

    SELECT @Codigo = 0,
           @Mensaje = CASE WHEN @Total = 0 THEN N'No se encontraron salones con esos filtros.'
                           ELSE N'Consulta exitosa.' END;

    SELECT @Total AS TotalRegistros,
           (@Total + @TamanoPagina - 1) / @TamanoPagina AS TotalPaginas,
           @Pagina AS Pagina,
           @TamanoPagina AS TamanoPagina;

    SELECT Id, Nombre, Zona, Capacidad, PrecioDesde, FotoPrincipal
    FROM #Resultados
    ORDER BY PrecioDesde, Id
    OFFSET (@Pagina - 1) * @TamanoPagina ROWS
    FETCH NEXT @TamanoPagina ROWS ONLY;
END
ExecuteSqlStatement -- =========================================================================
-- DISPONIBILIDAD (Sprint 4 — HU-12)
-- =========================================================================

-- Fechas ocupadas de un salón en un mes. Solo cuentan las reservaciones 'confirmada';
-- las pendientes y canceladas no ocupan la fecha.
-- Un mes sin fechas ocupadas NO es error: éxito con result set vacío.
-- Regresa un solo result set con las fechas (FechaEvento) ordenadas.
CREATE OR ALTER PROCEDURE sp_Salon_Disponibilidad
    @SalonId INT,
    @Anio    INT,
    @Mes     INT,
    @Codigo  INT OUTPUT,
    @Mensaje NVARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF ISNULL(@Mes, 0) NOT BETWEEN 1 AND 12 OR ISNULL(@Anio, 0) NOT BETWEEN 2000 AND 2100
    BEGIN SELECT @Codigo = 1001, @Mensaje = N'Mes o año inválido.'; RETURN; END

    IF NOT EXISTS (SELECT 1 FROM Salones WHERE Id = @SalonId)
    BEGIN SELECT @Codigo = 2001, @Mensaje = N'El salón no existe.'; RETURN; END

    DECLARE @Inicio DATE = DATEFROMPARTS(@Anio, @Mes, 1);
    DECLARE @Fin DATE = DATEADD(MONTH, 1, @Inicio);

    SELECT DISTINCT FechaEvento
    INTO #Fechas
    FROM Reservaciones
    WHERE SalonId = @SalonId
      AND Estado = 'confirmada'
      AND FechaEvento >= @Inicio
      AND FechaEvento < @Fin;

    SELECT @Codigo = 0,
           @Mensaje = CASE WHEN NOT EXISTS (SELECT 1 FROM #Fechas)
                           THEN N'El salón no tiene fechas ocupadas en ese mes.'
                           ELSE N'Consulta exitosa.' END;

    SELECT FechaEvento FROM #Fechas ORDER BY FechaEvento;
END
ExecuteSqlStatement /* =========================================================================
   GlamSpaces — Índices para la búsqueda de salones (Sprint 3 — HU-08)
   Correr DESPUÉS de 01_Tablas.sql y 02_StoredProcedures.sql.
   Idempotente: solo crea los índices que no existan.

   Los usa sp_Salon_Buscar (HU-09):
     - filtrado por Estado = 'publicado', zona y capacidad;
     - "precio desde" = paquete más barato de cada salón;
     - foto principal = primera foto de cada salón.
   ========================================================================= */

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
ExecuteSqlStatement -- Salones publicados por capacidad, incluyendo zona y nombre.
--  - Filtrado (WHERE Estado = 'publicado'): solo guarda los salones que participan en la
--    búsqueda, así es más chico y rápido que la tabla completa.
--  - Capacidad es la llave: "capacidad mínima" hace una búsqueda directa (seek) en el índice.
--  - Zona va en INCLUDE y no como llave, porque se busca por coincidencia parcial
--    (LIKE '%tula%'), que no puede usar un índice para saltar directo. Al estar incluida,
--    el filtro de zona se resuelve dentro del índice sin leer la tabla.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Salones_Publicados_Capacidad_Zona')
    CREATE NONCLUSTERED INDEX IX_Salones_Publicados_Capacidad_Zona
        ON Salones (Capacidad)
        INCLUDE (Zona, Nombre)
        WHERE Estado = 'publicado';
ExecuteSqlStatement -- Paquetes por salón y precio: el precio más bajo de cada salón (MIN) es la primera fila
-- del índice para ese SalonId. Además acelera el FK Paquetes → Salones.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Paquetes_SalonId_Precio')
    CREATE NONCLUSTERED INDEX IX_Paquetes_SalonId_Precio
        ON Paquetes (SalonId, Precio);
ExecuteSqlStatement -- Fotos por salón: la primera foto (menor Id) sale directo del índice.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_FotosSalon_SalonId')
    CREATE NONCLUSTERED INDEX IX_FotosSalon_SalonId
        ON FotosSalon (SalonId)
        INCLUDE (Url);
ExecuteSqlStatement -- =========================================================================
-- Sprint 4 — Reservaciones (HU-11 / HU-12)
-- =========================================================================

-- Disponibilidad del salón por mes (sp_Salon_Disponibilidad): busca por salón y rango
-- de fechas; Estado va incluido para filtrar las confirmadas sin leer la tabla.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Reservaciones_Salon_Fecha')
    CREATE NONCLUSTERED INDEX IX_Reservaciones_Salon_Fecha
        ON Reservaciones (SalonId, FechaEvento)
        INCLUDE (Estado);
ExecuteSqlStatement -- Regla de negocio: un salón no puede tener 2 reservaciones confirmadas el mismo día.
-- Único y filtrado: las pendientes y canceladas sí pueden repetir fecha.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Reservaciones_Confirmada')
    CREATE UNIQUE NONCLUSTERED INDEX UX_Reservaciones_Confirmada
        ON Reservaciones (SalonId, FechaEvento)
        WHERE Estado = 'confirmada';
Committing Transaction
202610230900: M001_LineaBase migrated
-------------------------------------------------------------------------------
202610230910: M002_CrearTablaBusquedasFavoritas migrating
-------------------------------------------------------------------------------
Beginning Transaction
CreateTable BusquedasFavoritas
CreateForeignKey FK_BusquedasFavoritas_Usuarios BusquedasFavoritas(UsuarioId) Usuarios(Id)
CreateForeignKey FK_BusquedasFavoritas_Salones BusquedasFavoritas(SalonId) Salones(Id)
Committing Transaction
202610230910: M002_CrearTablaBusquedasFavoritas migrated
-------------------------------------------------------------------------------
202610230920: M003_AgregarColumnaTelefonoSalones migrating
-------------------------------------------------------------------------------
Beginning Transaction
AlterTable Salones
CreateColumn Salones Telefono String
Committing Transaction
202610230920: M003_AgregarColumnaTelefonoSalones migrated
-------------------------------------------------------------------------------
202610230930: M004_IndiceUnicoBusquedasFavoritas migrating
-------------------------------------------------------------------------------
Beginning Transaction
CreateIndex BusquedasFavoritas (UsuarioId, SalonId)
Committing Transaction
202610230930: M004_IndiceUnicoBusquedasFavoritas migrated
Task completed.

# Verificación del esquema resultante (tablas, defaults, stored procedures, índices del proyecto y columna Telefono)
# Comando: .\scripts\migraciones.ps1 consultar -Sql "SELECT t.name AS tabla, (SELECT COUNT(*) FROM sys.columns c WHERE c.object_id = t.object_id) AS columnas, (SELECT COUNT(*) FROM sys.default_constraints d WHERE d.parent_object_id = t.object_id) AS defaults FROM sys.tables t WHERE t.name <> 'VersionInfo' ORDER BY t.name; SELECT COUNT(*) AS stored_procedures FROM sys.procedures; SELECT OBJECT_NAME(i.object_id) AS tabla, i.name AS indice, i.is_unique AS es_unico, i.filter_definition AS filtro FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id WHERE t.is_ms_shipped = 0 AND t.name <> 'VersionInfo' AND i.is_primary_key = 0 AND i.name IS NOT NULL ORDER BY tabla, indice; SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH AS largo, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Salones' AND COLUMN_NAME = 'Telefono'"

tabla              columnas defaults
-----              -------- --------
BusquedasFavoritas        4        1
FotosSalon                3        0
Paquetes                  5        0
Reservaciones             7        2
Salones                   9        2
Usuarios                  7        0
(6 filas)

stored_procedures
-----------------
               12
(1 filas)

tabla              indice                               es_unico filtro                 
-----              ------                               -------- ------                 
BusquedasFavoritas UX_BusquedasFavoritas_Usuario_Salon      True                        
FotosSalon         IX_FotosSalon_SalonId                   False                        
Paquetes           IX_Paquetes_SalonId_Precio              False                        
Reservaciones      IX_Reservaciones_Salon_Fecha            False                        
Reservaciones      UX_Reservaciones_Confirmada              True ([Estado]='confirmada')
Salones            IX_Salones_Publicados_Capacidad_Zona    False ([Estado]='publicado') 
Usuarios           IX_Usuarios_Correo                       True
(7 filas)

COLUMN_NAME DATA_TYPE largo IS_NULLABLE
----------- --------- ----- -----------
Telefono    nvarchar     20 YES
(1 filas)
# Código de salida: 0
```

</details>

### Historial en `VersionInfo` — [`09_versioninfo.txt`](evidencias/09_versioninfo.txt)

`AppliedOn` se guarda en **UTC** (04:10 del 24/10 UTC = 22:10 del 23/10 en hora del centro de México).

```text
# Evidencia 09 — Historial de migraciones aplicadas (tabla VersionInfo de FluentMigrator)
# Fecha: 2026-10-23 22:11:09 (hora local)
# Servidor / BD: (localdb)\MSSQLLocalDB / GlamSpacesMigraciones
# Comando: .\scripts\migraciones.ps1 consultar -Sql "SELECT Version, AppliedOn, Description FROM dbo.VersionInfo ORDER BY Version"
#

     Version AppliedOn                 Description                                               
     ------- ---------                 -----------                                               
202610230900 24/10/2026 04:10:58 a. m. Línea base: tablas, stored procedures e índices existentes
202610230910 24/10/2026 04:10:58 a. m. Crea la tabla BusquedasFavoritas                          
202610230920 24/10/2026 04:10:58 a. m. Agrega la columna Telefono a Salones                      
202610230930 24/10/2026 04:10:58 a. m. Índice único (UsuarioId, SalonId) en BusquedasFavoritas
(4 filas)
# Código de salida: 0
```

> 📸 **CAPTURA AQUÍ:** reset limpio terminado y el `SELECT` de `VersionInfo`.

### Problema encontrado al generar las evidencias

En la primera corrida LocalDB respondió *"There is insufficient system memory in resource pool
'internal'"* (error 701): el equipo tenía solo ~0.7 GB de RAM libre y Windows le indicó a SQL Server
que la memoria física estaba baja. Se resolvió reservándole memoria mínima a la instancia local:

```sql
EXEC sp_configure 'show advanced options', 1; RECONFIGURE;
EXEC sp_configure 'min server memory (MB)', 512; RECONFIGURE;
-- (para regresarlo: EXEC sp_configure 'min server memory (MB)', 0; RECONFIGURE;)
```

No es un problema de las migraciones; es del ambiente. Las evidencias de `docs/evidencias/` son de la
corrida posterior, ya sin ese error.

---

## 10. Seguridad y límites

- **Solo LocalDB o un SQL Server de pruebas. Nunca Azure.** `scripts/migraciones.ps1` se niega a correr
  si la conexión contiene `database.windows.net`:

  ```text
  Conexión rechazada: apunta a Azure SQL (database.windows.net). Este script es solo para LocalDB o un SQL Server de pruebas.
  ```

- La conexión por defecto es **LocalDB con autenticación de Windows** (sin contraseña):
  `Server=(localdb)\MSSQLLocalDB;Database=GlamSpacesMigraciones;Trusted_Connection=True;TrustServerCertificate=True`.
  Otra BD se indica con la variable de entorno `GLAMSPACES_CONN`, nunca en un archivo del repo.
- **Respaldo antes de cualquier rollback** (`respaldar`), verificado con `RESTORE VERIFYONLY`.
- El **reset limpio** solo se permite en LocalDB, porque borra la base de datos completa.
- La línea base **no tiene `Down()`**: no se puede revertir por accidente el esquema de producción.
- **Qué NO se publica en GitHub:**
  - cadenas de conexión reales y contraseñas (la del workflow vive en el secret `MSSQL_SA_PASSWORD`);
  - respaldos (`*.bak` y la carpeta `respaldos/` están en `.gitignore`).
- Aplicar las migraciones en Azure (producción) es una decisión aparte del equipo. Antes habría que
  respaldar la BD y aplicar primero la 001, que solo registra la línea base.

---

## 11. Versionado en GitHub (punto 14)

- **Rama:** `Sprint4_Adrian` (PR hacia `develop`).
- **Commits (uno por parte lógica, formato `RE_Sprint4: ...`):**
  1. Instalación de FluentMigrator (proyecto `GlamSpaces.Migrations` y tool local `dotnet-fm`).
  2. Migraciones 001 a 004.
  3. `scripts/migraciones.ps1` y `.gitignore` (`*.bak`, `respaldos/`).
  4. Evidencias reales en `docs/evidencias/`.
  5. Workflow `.github/workflows/migraciones.yml`.
  6. Esta documentación.
  7. Sección en el README.
- **Ubicación de los archivos:**
  - Migraciones: `GlamSpaces.Migrations/Migraciones/`
  - Herramienta: `.config/dotnet-tools.json`
  - Script: `scripts/migraciones.ps1`
  - Evidencias: `docs/evidencias/`
  - Pipeline: `.github/workflows/migraciones.yml`
- **Workflow `Migraciones BD`:** se dispara en push a `develop`, `main` y `Sprint4_Adrian`, y en PR hacia
  `develop` y `main`. Levanta un contenedor `mcr.microsoft.com/mssql/server:2022-latest` (contraseña desde
  el secret `MSSQL_SA_PASSWORD`), crea una BD vacía y ejecuta: listar → `migrate up` → listar →
  `04_DatosPrueba.sql` + un cliente de prueba → `06_SolicitudesHU14.sql` dos veces (verifica 3 solicitudes,
  una por estado) → `rollback by 1` → verificación de que `Solicitudes` se quitó → listar → `migrate up` →
  verificación de que `Solicitudes` volvió con sus 2 CHECK y su índice filtrado + `VersionInfo`.
  Si una verificación falla, el job falla. (Hasta el Sprint 4 la última migración era la 004 y el
  rollback verificaba su índice; desde la HU-14 la última es la 005.)

> 📸 **CAPTURA AQUÍ:** el workflow "Migraciones BD" en verde en la pestaña *Actions* de GitHub.

---

## 12. Sprint 5 — HU-14: tabla Solicitudes

**Rama:** `Sprint5_Adrian` (PR hacia `develop`) · **Migración:** `M005_CrearSolicitudes` (`202610291000`)
· **Script:** `Database/06_SolicitudesHU14.sql`

### Por qué hubo que versionarla

La tabla `Solicitudes` se creó **a mano en Azure** (`glamspaces-db`) para que el equipo pudiera
avanzar con la HU-14. Si no queda en el repo, cualquier BD nueva (la local de un compañero, el
contenedor de GitHub Actions) se queda sin ella, y la HU-15 depende de esta tabla. Ahora queda en
dos lugares que se complementan:

| Archivo | Qué contiene | Quién lo usa |
|---|---|---|
| `GlamSpaces.Migrations/Migraciones/005_CrearSolicitudes.cs` | Solo el **esquema**: tabla, restricciones e índice. Up idempotente, Down real. | `dotnet-fm` / `scripts/migraciones.ps1` / workflow `Migraciones BD`. |
| `Database/06_SolicitudesHU14.sql` | El mismo esquema **más la semilla** de 3 solicitudes. Es el script que se aplicó en Azure. | Se corre a mano (SSMS, `sqlcmd` o `migraciones.ps1 script`) y lo ejecuta el workflow. |

La semilla **no** va dentro de la migración por dos razones: en una BD recién creada no hay salones
ni clientes (la semilla haría `THROW` y la migración fallaría), y en producción los datos ya se
cargaron a mano.

### Modelo

| Columna | Tipo | Nulos | Restricción |
|---|---|---|---|
| `Id` | `INT IDENTITY(1,1)` | NOT NULL | `PK_Solicitudes` |
| `SalonId` | `INT` | NOT NULL | `FK_Solicitudes_Salones` → `Salones(Id)` |
| `ClienteId` | `INT` | NOT NULL | `FK_Solicitudes_Usuarios` → `Usuarios(Id)` |
| `PaqueteId` | `INT` | NULL | `FK_Solicitudes_Paquetes` → `Paquetes(Id)` |
| `FechaEvento` | `DATE` | NOT NULL | |
| `TipoEvento` | `NVARCHAR(100)` | NOT NULL | |
| `NumeroInvitados` | `INT` | NOT NULL | `CK_Solicitudes_Invitados`: `NumeroInvitados > 0` |
| `Comentarios` | `NVARCHAR(500)` | NULL | |
| `Estado` | `NVARCHAR(20)` | NOT NULL | `DF_Solicitudes_Estado` = `'pendiente'`; `CK_Solicitudes_Estado`: `pendiente`, `confirmada` o `rechazada` |
| `FechaCreacion` | `DATETIME2(7)` | NOT NULL | `DF_Solicitudes_FechaCreacion` = `SYSUTCDATETIME()` |

**Índice único filtrado** `UX_Solicitudes_Cliente_Salon_Fecha_Pendiente` sobre
`(ClienteId, SalonId, FechaEvento) WHERE Estado = 'pendiente'`: un cliente no puede tener **dos
solicitudes pendientes** para el mismo salón y la misma fecha. Las confirmadas o rechazadas no
cuentan, así que puede volver a pedir esa fecha si antes se la rechazaron. Como todo índice
filtrado, exige `QUOTED_IDENTIFIER ON`.

### Up idempotente y Down

- **Up** se divide en 4 lotes `Execute.Sql`: tabla, `CK_Solicitudes_Invitados`, `CK_Solicitudes_Estado`
  e índice. Cada uno revisa antes si ya existe (`IF OBJECT_ID(...) IS NULL`, `IF NOT EXISTS` en
  `sys.check_constraints` y `sys.indexes`). Van en lotes separados para que cada verificación vea la tabla
  ya creada. No se usó la API fluida (`Create.Table`) porque no puede preguntar "si no existe" y
  fallaría en Azure, donde la tabla ya está.
- **Down** borra la tabla (`Delete.Table("Solicitudes")` → `DROP TABLE [dbo].[Solicitudes]`); sus
  restricciones e índice se van con ella.
- **En Azure**, cuando el equipo decida aplicar las migraciones, la 005 encontrará todo creado, no
  cambiará nada y solo quedará registrada en `VersionInfo` (se simuló en la evidencia 16).

### Semilla de `06_SolicitudesHU14.sql`

Toma el **primer salón** (`MIN(Id)`), **su primer paquete** y el **primer usuario con
`TipoCuenta = 'cliente'`**. Si falta alguno, se detiene con `THROW` (50001 salón, 50002 paquete,
50003 cliente) en lugar de insertar datos incompletos. Inserta una solicitud por estado (`pendiente`,
`confirmada`, `rechazada`) y no duplica nada si se vuelve a correr.

### Pruebas unitarias (sin base de datos)

En `GlamSpaces.Tests/Migrations/MigracionesPruebas.cs` (82 pruebas en total, todas en verde):

- La 005 se agregó a la prueba de versión y descripción (`202610291000`).
- `M005_Up_CreaSolicitudesEnCuatroLotesIdempotentes`: 4 lotes, cada uno con su `IF … IS NULL / IF NOT EXISTS`, sin `GO`.
- `M005_Up_DefineLasColumnasYLlavesDeLaHu14`: las 10 columnas con su tipo, nulos, PK, FK y DEFAULT.
- `M005_Up_AgregaLosChecksYElIndiceUnicoFiltradoDePendientes`: los 2 CHECK y el índice filtrado con `QUOTED_IDENTIFIER ON`.
- `M005_Down_BorraLaTablaSolicitudes`: el SQL generado es `DROP TABLE [dbo].[Solicitudes]`.

Cobertura local de `GlamSpaces.Migrations`: **100 %** (103/103 líneas, 4/4 ramas).

### Prueba local con `scripts/migraciones.ps1` (LocalDB, nunca Azure)

Para correr el script 06 se agregó la acción `script` al script de migraciones. Parte el `.sql` en
lotes por `GO`, igual que `sqlcmd`:

```powershell
.\scripts\migraciones.ps1 script -Archivo Database\06_SolicitudesHU14.sql
```

Orden: listar → subir → verificar → script 06 → rollback 1 → reaplicar → BD que ya tiene la tabla
(como Azure) → reset y todo desde cero.

#### Listar: la 005 aparece pendiente — [`10_hu14_list_antes.txt`](evidencias/10_hu14_list_antes.txt)

```text
# Evidencia 10 — HU-14: listar antes de aplicar (la 005 aparece pendiente)
# Fecha: 2026-10-29 13:27:03 (hora local)
# Servidor / BD: (localdb)\MSSQLLocalDB / GlamSpacesMigraciones
# Comando: .\scripts\migraciones.ps1 listar
#
-------------------------------------------------------------------------------
Migrations
-------------------------------------------------------------------------------
202610230900: M001_LineaBase
202610230910: M002_CrearTablaBusquedasFavoritas
202610230920: M003_AgregarColumnaTelefonoSalones
⌘ 202610230930: M004_IndiceUnicoBusquedasFavoritas (current)
202610291000: M005_CrearSolicitudes (not applied)
Task completed.
# Código de salida: 0
```

#### Subir — [`11_hu14_migrate_up.txt`](evidencias/11_hu14_migrate_up.txt)

```text
# Evidencia 11 — HU-14: aplicar la migración pendiente (005, tabla Solicitudes)
# Fecha: 2026-10-29 13:27:07 (hora local)
# Servidor / BD: (localdb)\MSSQLLocalDB / GlamSpacesMigraciones
# Comando: .\scripts\migraciones.ps1 subir
#
Beginning Transaction
Rolling back transaction
-------------------------------------------------------------------------------
202610291000: M005_CrearSolicitudes migrating
-------------------------------------------------------------------------------
Beginning Transaction
ExecuteSqlStatement 
IF OBJECT_ID(N'dbo.Solicitudes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Solicitudes (
        Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Solicitudes PRIMARY KEY,
        SalonId         INT           NOT NULL CONSTRAINT FK_Solicitudes_Salones  REFERENCES dbo.Salones(Id),
        ClienteId       INT           NOT NULL CONSTRAINT FK_Solicitudes_Usuarios REFERENCES dbo.Usuarios(Id),
        PaqueteId       INT           NULL     CONSTRAINT FK_Solicitudes_Paquetes REFERENCES dbo.Paquetes(Id),
        FechaEvento     DATE          NOT NULL,
        TipoEvento      NVARCHAR(100) NOT NULL,
        NumeroInvitados INT           NOT NULL,
        Comentarios     NVARCHAR(500) NULL,
        Estado          NVARCHAR(20)  NOT NULL CONSTRAINT DF_Solicitudes_Estado DEFAULT 'pendiente',
        FechaCreacion   DATETIME2(7)  NOT NULL CONSTRAINT DF_Solicitudes_FechaCreacion DEFAULT SYSUTCDATETIME()
    );
END
ExecuteSqlStatement 
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Solicitudes_Invitados')
    ALTER TABLE dbo.Solicitudes ADD CONSTRAINT CK_Solicitudes_Invitados CHECK (NumeroInvitados > 0);
ExecuteSqlStatement 
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Solicitudes_Estado')
    ALTER TABLE dbo.Solicitudes ADD CONSTRAINT CK_Solicitudes_Estado CHECK (Estado IN ('pendiente', 'confirmada', 'rechazada'));
ExecuteSqlStatement 
SET QUOTED_IDENTIFIER ON;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Solicitudes_Cliente_Salon_Fecha_Pendiente'
               AND object_id = OBJECT_ID(N'dbo.Solicitudes'))
    CREATE UNIQUE NONCLUSTERED INDEX UX_Solicitudes_Cliente_Salon_Fecha_Pendiente
        ON dbo.Solicitudes (ClienteId, SalonId, FechaEvento)
        WHERE Estado = 'pendiente';
Committing Transaction
202610291000: M005_CrearSolicitudes migrated
Task completed.
# Código de salida: 0
```

#### Verificar la estructura — [`12_hu14_verificar.txt`](evidencias/12_hu14_verificar.txt)

```text
# Evidencia 12 — HU-14: verificar la estructura de Solicitudes (columnas, llaves, CHECK, índice) y VersionInfo
# Fecha: 2026-10-29 13:27:33 (hora local)
# Servidor / BD: (localdb)\MSSQLLocalDB / GlamSpacesMigraciones
# Comando: .\scripts\migraciones.ps1 consultar -Sql "<consultas a sys.columns, sys.foreign_keys, sys.check_constraints, sys.indexes y VersionInfo>"
#

 N Columna         Tipo          Nulos    Identidad PorDefecto        
 - -------         ----          -----    --------- ----------        
 1 Id              int           NOT NULL      True                   
 2 SalonId         int           NOT NULL     False                   
 3 ClienteId       int           NOT NULL     False                   
 4 PaqueteId       int           NULL         False                   
 5 FechaEvento     date          NOT NULL     False                   
 6 TipoEvento      nvarchar(100) NOT NULL     False                   
 7 NumeroInvitados int           NOT NULL     False                   
 8 Comentarios     nvarchar(500) NULL         False                   
 9 Estado          nvarchar(20)  NOT NULL     False ('pendiente')     
10 FechaCreacion   datetime2(7)  NOT NULL     False (sysutcdatetime())
(10 filas)

Restriccion                  Tipo                  
-----------                  ----                  
CK_Solicitudes_Estado        CHECK_CONSTRAINT      
CK_Solicitudes_Invitados     CHECK_CONSTRAINT      
DF_Solicitudes_Estado        DEFAULT_CONSTRAINT    
DF_Solicitudes_FechaCreacion DEFAULT_CONSTRAINT    
FK_Solicitudes_Paquetes      FOREIGN_KEY_CONSTRAINT
FK_Solicitudes_Salones       FOREIGN_KEY_CONSTRAINT
FK_Solicitudes_Usuarios      FOREIGN_KEY_CONSTRAINT
PK_Solicitudes               PRIMARY_KEY_CONSTRAINT
(8 filas)

LlaveForanea            Columna   Referencia
------------            -------   ----------
FK_Solicitudes_Paquetes PaqueteId Paquetes  
FK_Solicitudes_Salones  SalonId   Salones   
FK_Solicitudes_Usuarios ClienteId Usuarios
(3 filas)

CheckRestriccion         Definicion                                                             
----------------         ----------                                                             
CK_Solicitudes_Estado    ([Estado]='rechazada' OR [Estado]='confirmada' OR [Estado]='pendiente')
CK_Solicitudes_Invitados ([NumeroInvitados]>(0))
(2 filas)

Indice                                       Unico Filtrado Filtro                 Columnas                       
------                                       ----- -------- ------                 --------                       
PK_Solicitudes                                True    False                        Id                             
UX_Solicitudes_Cliente_Salon_Fecha_Pendiente  True     True ([Estado]='pendiente') ClienteId, SalonId, FechaEvento
(2 filas)

     Version AppliedOn                 Description                                                            
     ------- ---------                 -----------                                                            
202610230900 24/10/2026 04:10:58 a. m. Línea base: tablas, stored procedures e índices existentes             
202610230910 24/10/2026 04:10:58 a. m. Crea la tabla BusquedasFavoritas                                       
202610230920 24/10/2026 04:10:58 a. m. Agrega la columna Telefono a Salones                                   
202610230930 24/10/2026 04:10:58 a. m. Índice único (UsuarioId, SalonId) en BusquedasFavoritas                
202610291000 29/10/2026 07:27:10 p. m. HU-14: tabla Solicitudes con restricciones e índice único de pendientes
(5 filas)
# Código de salida: 0
```

> 📸 **CAPTURA AQUÍ:** la tabla `Solicitudes` (columnas, llaves y el índice filtrado) en SSMS o en el Explorador de objetos.

#### Script 06: THROW sin datos, semilla, idempotencia y restricciones — [`13_hu14_script06_semilla.txt`](evidencias/13_hu14_script06_semilla.txt)

El paso A muestra el `THROW` cuando no hay salones. Los pasos B y C cargan los datos previos, solo
en LocalDB. D y E corren el script dos veces y quedan las mismas 3 filas. F comprueba que la BD
rechaza 0 invitados, el estado `cancelada`, una segunda solicitud pendiente igual y un salón que no
existe. También muestra que `Estado` y `FechaCreacion` toman su valor `DEFAULT`.

```text
# Evidencia 13 — HU-14: script 06_SolicitudesHU14.sql (THROW sin datos, semilla, idempotencia y restricciones)
# Fecha: 2026-10-29 13:27:59 (hora local)
# Servidor / BD: (localdb)\MSSQLLocalDB / GlamSpacesMigraciones
# Comando: ver cada paso abajo
#
>>> Paso A: 06 en la BD recién migrada (sin salones ni clientes): la semilla debe hacer THROW
>>> .\scripts\migraciones.ps1 script -Archivo Database\06_SolicitudesHU14.sql
Ejecutando Database\06_SolicitudesHU14.sql en [GlamSpacesMigraciones]
La tabla Solicitudes ya existe.
C:\Trabajos ADRIAN\trabajos escolares\TrabajoAplicacionesWeb\Backend\GlamSpace-Back\scripts\migraciones.ps1 : 
Excepción al llamar a "ExecuteReader" con los argumentos "0": "Semilla HU-14: no hay ningún salón en dbo.Salones."
    + CategoryInfo          : NotSpecified: (:) [migraciones.ps1], MethodInvocationException
    + FullyQualifiedErrorId : SqlException,migraciones.ps1
 
# (código de salida del paso A: 1)

>>> Paso B: datos previos — 04_DatosPrueba.sql (salones y paquetes de prueba)
>>> .\scripts\migraciones.ps1 script -Archivo Database\04_DatosPrueba.sql
Ejecutando Database\04_DatosPrueba.sql en [GlamSpacesMigraciones]

Id Nombre                       Zona                  Capacidad Estado       PrecioDesde
-- ------                       ----                  --------- ------       -----------
 1 [Prueba] Jardín Encanto      Tula de Allende, Hgo.       150 publicado        8500.00
 2 [Prueba] Terraza Monarca     Tepeji del Río, Hgo.         80 publicado        5000.00
 3 [Prueba] Hacienda El Rosario Tula de Allende, Hgo.       400 publicado       30000.00
 4 [Prueba] Salón Fiesta Kids   Tlahuelilpan, Hgo.          100 publicado        3500.00
 5 [Prueba] Quinta Las Palmas   Mixquiahuala, Hgo.          250 publicado       12000.00
 6 [Prueba] Jardín Oculto       Tula de Allende, Hgo.       500 no_publicado     1000.00
(6 filas)

Id SalonId FechaEvento               Estado    
-- ------- -----------               ------    
 1       1 14/11/2026 12:00:00 a. m. confirmada
 2       1 21/11/2026 12:00:00 a. m. confirmada
 3       1 28/11/2026 12:00:00 a. m. pendiente
(3 filas)
# (código de salida del paso B: 0)

>>> Paso C: un usuario con TipoCuenta = 'cliente' (solo para la BD local)
>>> .\scripts\migraciones.ps1 consultar -Sql "IF NOT EXISTS (...) INSERT INTO Usuarios (...) VALUES ('Cliente de Prueba', ..., 'cliente', ...)"

Id NombreCompleto    TipoCuenta
-- --------------    ----------
 2 Cliente de Prueba cliente
(1 filas)
# (código de salida del paso C: 0)

>>> Paso D: 06 por primera vez con datos: crea las 3 solicitudes
Ejecutando Database\06_SolicitudesHU14.sql en [GlamSpacesMigraciones]
La tabla Solicitudes ya existe.

Id SalonId ClienteId PaqueteId FechaEvento               TipoEvento NumeroInvitados Estado    
-- ------- --------- --------- -----------               ---------- --------------- ------    
 1       1         2         1 05/12/2026 12:00:00 a. m. Boda                   120 pendiente 
 2       1         2         1 12/12/2026 12:00:00 a. m. XV años                 90 confirmada
 3       1         2         1 19/12/2026 12:00:00 a. m. Cumpleaños              40 rechazada
(3 filas)
# (código de salida del paso D: 0)

>>> Paso E: 06 por segunda vez: idempotente, no duplica nada
Ejecutando Database\06_SolicitudesHU14.sql en [GlamSpacesMigraciones]
La tabla Solicitudes ya existe.

Id SalonId ClienteId PaqueteId FechaEvento               TipoEvento NumeroInvitados Estado    
-- ------- --------- --------- -----------               ---------- --------------- ------    
 1       1         2         1 05/12/2026 12:00:00 a. m. Boda                   120 pendiente 
 2       1         2         1 12/12/2026 12:00:00 a. m. XV años                 90 confirmada
 3       1         2         1 19/12/2026 12:00:00 a. m. Cumpleaños              40 rechazada
(3 filas)
# (código de salida del paso E: 0)

>>> Paso F: las restricciones rechazan datos inválidos (cada INSERT va en TRY/CATCH)
>>> .\scripts\migraciones.ps1 consultar -Sql "<INSERT inválidos en TRY/CATCH>"
OK invitados = 0 rechazado -> 547: The INSERT statement conflicted with the CHECK constraint "CK_Solicitudes_Invitados". The conflict occurred in database "GlamSpacesMigraciones", table "dbo.Solicitudes", column 'NumeroInvitados'.
OK estado cancelada rechazado -> 547: The INSERT statement conflicted with the CHECK constraint "CK_Solicitudes_Estado". The conflict occurred in database "GlamSpacesMigraciones", table "dbo.Solicitudes", column 'Estado'.
OK segunda pendiente mismo cliente/salón/fecha rechazada -> 2601: Cannot insert duplicate key row in object 'dbo.Solicitudes' with unique index 'UX_Solicitudes_Cliente_Salon_Fecha_Pendiente'. The duplicate key value is (2, 1, 2026-12-05).
OK salón inexistente rechazado -> 547: The INSERT statement conflicted with the FOREIGN KEY constraint "FK_Solicitudes_Salones". The conflict occurred in database "GlamSpacesMigraciones", table "dbo.Salones", column 'Id'.
OK DEFAULT: Estado y FechaCreacion se llenan solos (se revierte con ROLLBACK)

Estado    FechaCreacion            
------    -------------            
pendiente 29/10/2026 07:28:03 p. m.
(1 filas)

Estado     Total
------     -----
confirmada     1
pendiente      1
rechazada      1
(3 filas)
# Código de salida: 0
```

#### Rollback de la 005 — [`14_hu14_rollback.txt`](evidencias/14_hu14_rollback.txt)

Antes de cualquier rollback fuera de una BD desechable va el respaldo (`respaldar`, sección 7).
Aquí la BD es local y se recrea en el último paso.

```text
# Evidencia 14 — HU-14: rollback de la última migración (005) y verificación
# Fecha: 2026-10-29 13:28:16 (hora local)
# Servidor / BD: (localdb)\MSSQLLocalDB / GlamSpacesMigraciones
# Comando: .\scripts\migraciones.ps1 rollback   (dotnet-fm rollback ... by 1)
#
>>> .\scripts\migraciones.ps1 rollback
-------------------------------------------------------------------------------
202610291000: M005_CrearSolicitudes reverting
-------------------------------------------------------------------------------
Beginning Transaction
DeleteTable Solicitudes
Committing Transaction
202610291000: M005_CrearSolicitudes reverted
Task completed.

>>> Verificación: .\scripts\migraciones.ps1 consultar -Sql "<¿existe Solicitudes?, CHECK, índice, VersionInfo>"

TablaSolicitudes ChecksSolicitudes IndicePendientes TablasAnteriores
---------------- ----------------- ---------------- ----------------
NO existe                        0                0                5
(1 filas)

     Version Description                                               
     ------- -----------                                               
202610230900 Línea base: tablas, stored procedures e índices existentes
202610230910 Crea la tabla BusquedasFavoritas                          
202610230920 Agrega la columna Telefono a Salones                      
202610230930 Índice único (UsuarioId, SalonId) en BusquedasFavoritas
(4 filas)

>>> .\scripts\migraciones.ps1 listar
-------------------------------------------------------------------------------
Migrations
-------------------------------------------------------------------------------
202610230900: M001_LineaBase
202610230910: M002_CrearTablaBusquedasFavoritas
202610230920: M003_AgregarColumnaTelefonoSalones
⌘ 202610230930: M004_IndiceUnicoBusquedasFavoritas (current)
202610291000: M005_CrearSolicitudes (not applied)
Task completed.
# Código de salida: 0
```

#### Reaplicar — [`15_hu14_reaplicar.txt`](evidencias/15_hu14_reaplicar.txt)

```text
# Evidencia 15 — HU-14: reaplicar la 005 tras el rollback
# Fecha: 2026-10-29 13:28:23 (hora local)
# Servidor / BD: (localdb)\MSSQLLocalDB / GlamSpacesMigraciones
# Comando: .\scripts\migraciones.ps1 reaplicar
#
>>> .\scripts\migraciones.ps1 reaplicar   (subir + listar)
Beginning Transaction
Rolling back transaction
-------------------------------------------------------------------------------
202610291000: M005_CrearSolicitudes migrating
-------------------------------------------------------------------------------
Beginning Transaction
ExecuteSqlStatement 
IF OBJECT_ID(N'dbo.Solicitudes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Solicitudes (
        Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Solicitudes PRIMARY KEY,
        SalonId         INT           NOT NULL CONSTRAINT FK_Solicitudes_Salones  REFERENCES dbo.Salones(Id),
        ClienteId       INT           NOT NULL CONSTRAINT FK_Solicitudes_Usuarios REFERENCES dbo.Usuarios(Id),
        PaqueteId       INT           NULL     CONSTRAINT FK_Solicitudes_Paquetes REFERENCES dbo.Paquetes(Id),
        FechaEvento     DATE          NOT NULL,
        TipoEvento      NVARCHAR(100) NOT NULL,
        NumeroInvitados INT           NOT NULL,
        Comentarios     NVARCHAR(500) NULL,
        Estado          NVARCHAR(20)  NOT NULL CONSTRAINT DF_Solicitudes_Estado DEFAULT 'pendiente',
        FechaCreacion   DATETIME2(7)  NOT NULL CONSTRAINT DF_Solicitudes_FechaCreacion DEFAULT SYSUTCDATETIME()
    );
END
ExecuteSqlStatement 
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Solicitudes_Invitados')
    ALTER TABLE dbo.Solicitudes ADD CONSTRAINT CK_Solicitudes_Invitados CHECK (NumeroInvitados > 0);
ExecuteSqlStatement 
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Solicitudes_Estado')
    ALTER TABLE dbo.Solicitudes ADD CONSTRAINT CK_Solicitudes_Estado CHECK (Estado IN ('pendiente', 'confirmada', 'rechazada'));
ExecuteSqlStatement 
SET QUOTED_IDENTIFIER ON;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Solicitudes_Cliente_Salon_Fecha_Pendiente'
               AND object_id = OBJECT_ID(N'dbo.Solicitudes'))
    CREATE UNIQUE NONCLUSTERED INDEX UX_Solicitudes_Cliente_Salon_Fecha_Pendiente
        ON dbo.Solicitudes (ClienteId, SalonId, FechaEvento)
        WHERE Estado = 'pendiente';
Committing Transaction
202610291000: M005_CrearSolicitudes migrated
Task completed.
-------------------------------------------------------------------------------
Migrations
-------------------------------------------------------------------------------
202610230900: M001_LineaBase
202610230910: M002_CrearTablaBusquedasFavoritas
202610230920: M003_AgregarColumnaTelefonoSalones
202610230930: M004_IndiceUnicoBusquedasFavoritas
⌘ 202610291000: M005_CrearSolicitudes (current)
Task completed.

>>> Verificación: .\scripts\migraciones.ps1 consultar -Sql "<¿existe Solicitudes?, CHECK, índice, VersionInfo>"

TablaSolicitudes ChecksSolicitudes IndicePendientes TablasAnteriores
---------------- ----------------- ---------------- ----------------
existe                           2                1                5
(1 filas)

     Version Description                                                            
     ------- -----------                                                            
202610230900 Línea base: tablas, stored procedures e índices existentes             
202610230910 Crea la tabla BusquedasFavoritas                                       
202610230920 Agrega la columna Telefono a Salones                                   
202610230930 Índice único (UsuarioId, SalonId) en BusquedasFavoritas                
202610291000 HU-14: tabla Solicitudes con restricciones e índice único de pendientes
(5 filas)
# Código de salida: 0
```

#### La 005 sobre una BD que ya tiene la tabla (como `glamspaces-db`) — [`16_hu14_idempotencia_azure.txt`](evidencias/16_hu14_idempotencia_azure.txt)

```text
# Evidencia 16 — HU-14: la 005 sobre una BD que YA tiene Solicitudes (simula glamspaces-db)
# Fecha: 2026-10-29 13:28:47 (hora local)
# Servidor / BD: (localdb)\MSSQLLocalDB / GlamSpacesMigraciones
# Comando: ver cada paso abajo
#
>>> Paso A: se crea la tabla a mano con el script 06 (como se hizo en glamspaces-db) y se quita la 005 de VersionInfo
>>> .\scripts\migraciones.ps1 consultar -Sql "DELETE FROM VersionInfo WHERE Version = 202610291000"
La 005 ya no figura en VersionInfo; la tabla Solicitudes sigue existiendo.
>>> .\scripts\migraciones.ps1 script -Archivo Database\06_SolicitudesHU14.sql
Ejecutando Database\06_SolicitudesHU14.sql en [GlamSpacesMigraciones]
La tabla Solicitudes ya existe.

Id SalonId ClienteId PaqueteId FechaEvento               TipoEvento NumeroInvitados Estado    
-- ------- --------- --------- -----------               ---------- --------------- ------    
 1       1         2         1 05/12/2026 12:00:00 a. m. Boda                   120 pendiente 
 2       1         2         1 12/12/2026 12:00:00 a. m. XV años                 90 confirmada
 3       1         2         1 19/12/2026 12:00:00 a. m. Cumpleaños              40 rechazada
(3 filas)

>>> .\scripts\migraciones.ps1 listar
-------------------------------------------------------------------------------
Migrations
-------------------------------------------------------------------------------
202610230900: M001_LineaBase
202610230910: M002_CrearTablaBusquedasFavoritas
202610230920: M003_AgregarColumnaTelefonoSalones
⌘ 202610230930: M004_IndiceUnicoBusquedasFavoritas (current)
202610291000: M005_CrearSolicitudes (not applied)
Task completed.

>>> Paso B: subir — la 005 encuentra todo creado, se salta cada parte y solo queda registrada
>>> .\scripts\migraciones.ps1 subir
Beginning Transaction
Rolling back transaction
-------------------------------------------------------------------------------
202610291000: M005_CrearSolicitudes migrating
-------------------------------------------------------------------------------
Beginning Transaction
ExecuteSqlStatement 
IF OBJECT_ID(N'dbo.Solicitudes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Solicitudes (
        Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Solicitudes PRIMARY KEY,
        SalonId         INT           NOT NULL CONSTRAINT FK_Solicitudes_Salones  REFERENCES dbo.Salones(Id),
        ClienteId       INT           NOT NULL CONSTRAINT FK_Solicitudes_Usuarios REFERENCES dbo.Usuarios(Id),
        PaqueteId       INT           NULL     CONSTRAINT FK_Solicitudes_Paquetes REFERENCES dbo.Paquetes(Id),
        FechaEvento     DATE          NOT NULL,
        TipoEvento      NVARCHAR(100) NOT NULL,
        NumeroInvitados INT           NOT NULL,
        Comentarios     NVARCHAR(500) NULL,
        Estado          NVARCHAR(20)  NOT NULL CONSTRAINT DF_Solicitudes_Estado DEFAULT 'pendiente',
        FechaCreacion   DATETIME2(7)  NOT NULL CONSTRAINT DF_Solicitudes_FechaCreacion DEFAULT SYSUTCDATETIME()
    );
END
ExecuteSqlStatement 
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Solicitudes_Invitados')
    ALTER TABLE dbo.Solicitudes ADD CONSTRAINT CK_Solicitudes_Invitados CHECK (NumeroInvitados > 0);
ExecuteSqlStatement 
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Solicitudes_Estado')
    ALTER TABLE dbo.Solicitudes ADD CONSTRAINT CK_Solicitudes_Estado CHECK (Estado IN ('pendiente', 'confirmada', 'rechazada'));
ExecuteSqlStatement 
SET QUOTED_IDENTIFIER ON;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Solicitudes_Cliente_Salon_Fecha_Pendiente'
               AND object_id = OBJECT_ID(N'dbo.Solicitudes'))
    CREATE UNIQUE NONCLUSTERED INDEX UX_Solicitudes_Cliente_Salon_Fecha_Pendiente
        ON dbo.Solicitudes (ClienteId, SalonId, FechaEvento)
        WHERE Estado = 'pendiente';
Committing Transaction
202610291000: M005_CrearSolicitudes migrated
Task completed.

>>> Verificación (las 3 solicitudes siguen ahí, sin duplicados ni errores)

TablaSolicitudes ChecksSolicitudes IndicePendientes TablasAnteriores FilasSolicitudes
---------------- ----------------- ---------------- ---------------- ----------------
existe                           2                1                5                3
(1 filas)

     Version Description                                                            
     ------- -----------                                                            
202610230900 Línea base: tablas, stored procedures e índices existentes             
202610230910 Crea la tabla BusquedasFavoritas                                       
202610230920 Agrega la columna Telefono a Salones                                   
202610230930 Índice único (UsuarioId, SalonId) en BusquedasFavoritas                
202610291000 HU-14: tabla Solicitudes con restricciones e índice único de pendientes
(5 filas)
# Código de salida: 0
```

#### Reset limpio: 001 a 005 desde cero — [`17_hu14_reset_desde_cero.txt`](evidencias/17_hu14_reset_desde_cero.txt)

La salida completa (más de 700 líneas, con los 12 SP de la línea base) está en el archivo. Aquí va
el inicio y el final:

```text
# Evidencia 17 — HU-14: reset limpio y todas las migraciones (001–005) desde cero
# Fecha: 2026-10-29 13:28:56 (hora local)
# Servidor / BD: (localdb)\MSSQLLocalDB / GlamSpacesMigraciones
# Comando: .\scripts\migraciones.ps1 reset
#
>>> .\scripts\migraciones.ps1 reset   (borra y recrea la BD en LocalDB y aplica todo)
Borrando y recreando [GlamSpacesMigraciones] en (localdb)\MSSQLLocalDB
Base de datos [GlamSpacesMigraciones] creada vacía.
Beginning Transaction
Rolling back transaction
-------------------------------------------------------------------------------
VersionMigration migrating
...
        WHERE Estado = 'pendiente';
Committing Transaction
202610291000: M005_CrearSolicitudes migrated
Task completed.

>>> .\scripts\migraciones.ps1 listar
-------------------------------------------------------------------------------
Migrations
-------------------------------------------------------------------------------
202610230900: M001_LineaBase
202610230910: M002_CrearTablaBusquedasFavoritas
202610230920: M003_AgregarColumnaTelefonoSalones
202610230930: M004_IndiceUnicoBusquedasFavoritas
⌘ 202610291000: M005_CrearSolicitudes (current)
Task completed.

>>> Verificación

TablaSolicitudes ChecksSolicitudes IndicePendientes TablasAnteriores FilasSolicitudes
---------------- ----------------- ---------------- ---------------- ----------------
existe                           2                1                5                0
(1 filas)

     Version Description                                                            
     ------- -----------                                                            
202610230900 Línea base: tablas, stored procedures e índices existentes             
202610230910 Crea la tabla BusquedasFavoritas                                       
202610230920 Agrega la columna Telefono a Salones                                   
202610230930 Índice único (UsuarioId, SalonId) en BusquedasFavoritas                
202610291000 HU-14: tabla Solicitudes con restricciones e índice único de pendientes
(5 filas)
# Código de salida: 0
```

> 📸 **CAPTURA AQUÍ:** el reset con la 005 en `(current)` y el `SELECT` de `VersionInfo` con las 5 migraciones.

### En el workflow `Migraciones BD`

Ver sección 11: después de migrar, el workflow corre `04_DatosPrueba.sql`, crea un cliente de prueba
y ejecuta `06_SolicitudesHU14.sql` dos veces. Después revierte la 005, comprueba que `Solicitudes`
desapareció, la reaplica y comprueba que volvió completa.

---

## 13. Capturas de pantalla

Lugares marcados con **CAPTURA AQUÍ** en este documento:

1. Sección 6: `listar` antes y después de aplicar.
2. Sección 7: rollback + consulta `NO EXISTE` y la 004 en `(not applied)`.
3. Sección 8: reaplicación + consulta `EXISTE`.
4. Sección 9: reset limpio y `SELECT` de `VersionInfo`.
5. Sección 11: workflow "Migraciones BD" en verde en GitHub Actions.
6. Sección 12 (HU-14): la tabla `Solicitudes` en SSMS y el reset con la 005 en `(current)`.

Opcionales:

> 📸 **CAPTURA AQUÍ:** el proyecto `GlamSpaces.Migrations` en el Explorador de soluciones de Visual Studio.

> 📸 **CAPTURA AQUÍ:** la tabla `VersionInfo` abierta en SSMS o en el Explorador de objetos de SQL Server.
