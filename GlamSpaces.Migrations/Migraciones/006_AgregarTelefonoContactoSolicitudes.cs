namespace GlamSpaces.Migrations.Migraciones;

// 006 · HU-16 (Sprint 5): teléfono de contacto obligatorio en el formulario de solicitud.
// Es NULL en la BD porque ya hay solicitudes sin teléfono; el API lo exige al crear una nueva.
// La columna ya se agregó a mano en producción (Database/07_TelefonoSolicitudesHU16.sql),
// así que Up() es idempotente: solo la agrega si no existe.
[Migration(202610291100, "HU-16: columna TelefonoContacto en Solicitudes")]
public class M006_AgregarTelefonoContactoSolicitudes : Migration
{
    private const string Tabla = "Solicitudes";
    private const string Columna = "TelefonoContacto";

    public override void Up()
    {
        Execute.Sql(@"
IF COL_LENGTH(N'dbo.Solicitudes', N'TelefonoContacto') IS NULL
    ALTER TABLE dbo.Solicitudes ADD TelefonoContacto NVARCHAR(20) NULL;");
    }

    public override void Down()
    {
        Delete.Column(Columna).FromTable(Tabla);
    }
}
