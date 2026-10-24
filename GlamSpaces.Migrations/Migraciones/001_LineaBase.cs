namespace GlamSpaces.Migrations.Migraciones;

// 001 · Línea base: deja una BD vacía igual que producción (glamspaces-db) sin tocar lo que ya existe.
//  - En una BD nueva crea todo.
//  - En una BD que ya tiene el esquema (producción), las tablas e índices se saltan porque ya
//    existen y los stored procedures se recrean iguales (CREATE OR ALTER). Así producción queda
//    registrada en VersionInfo sin perder datos.
[Migration(202610230900, "Línea base: tablas, stored procedures e índices existentes")]
public class M001_LineaBase : Migration
{
    public override void Up()
    {
        // Usuarios la creó Entity Framework en el Sprint 1 (EnsureCreated), no el script SQL:
        // no tiene DEFAULT en FechaRegistro y el correo único es un índice (IX_Usuarios_Correo).
        // Se crea primero aquí para respetar esa forma; 01_Tablas.sql luego la salta.
        Execute.Sql(@"
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
END");

        // Resto del esquema, cargado de los scripts originales de Database/ (sin copias a mano).
        // Todos son idempotentes: IF NOT EXISTS para tablas e índices, CREATE OR ALTER para los SP.
        EjecutarScript("Database.01_Tablas.sql");          // Salones, Paquetes, FotosSalon, Reservaciones
        EjecutarScript("Database.02_StoredProcedures.sql"); // 12 stored procedures
        EjecutarScript("Database.03_Indices.sql");          // índices de búsqueda y de reservaciones
    }

    public override void Down()
    {
        // Intencionalmente vacío: la línea base representa el esquema de producción.
        // Revertirla borraría todas las tablas y sus datos, así que no se permite por rollback.
        // Para empezar desde cero en desarrollo se usa el "reset limpio" (borra y recrea la BD
        // local) de scripts/migraciones.ps1.
    }

    private void EjecutarScript(string recurso)
    {
        foreach (var lote in ScriptsSql.Lotes(recurso))
        {
            Execute.Sql(lote);
        }
    }
}
