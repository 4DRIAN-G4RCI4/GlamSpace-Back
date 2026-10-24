namespace GlamSpaces.Migrations.Migraciones;

// 003 · Adición de columna: teléfono de contacto del salón (opcional, para no afectar a los existentes).
[Migration(202610230920, "Agrega la columna Telefono a Salones")]
public class M003_AgregarColumnaTelefonoSalones : Migration
{
    public override void Up()
    {
        Alter.Table("Salones")
            .AddColumn("Telefono").AsString(20).Nullable(); // NVARCHAR(20) NULL
    }

    public override void Down()
    {
        Delete.Column("Telefono").FromTable("Salones");
    }
}
