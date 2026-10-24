namespace GlamSpaces.Migrations.Migraciones;

// 002 · Creación de tabla: salones que un usuario guarda como favoritos.
[Migration(202610230910, "Crea la tabla BusquedasFavoritas")]
public class M002_CrearTablaBusquedasFavoritas : Migration
{
    public override void Up()
    {
        Create.Table("BusquedasFavoritas")
            .WithColumn("Id").AsInt32().NotNullable().PrimaryKey("PK_BusquedasFavoritas").Identity()
            .WithColumn("UsuarioId").AsInt32().NotNullable()
                .ForeignKey("FK_BusquedasFavoritas_Usuarios", "Usuarios", "Id")
            .WithColumn("SalonId").AsInt32().NotNullable()
                .ForeignKey("FK_BusquedasFavoritas_Salones", "Salones", "Id")
            // DEFAULT GETDATE(): la fecha se llena sola al guardar el favorito.
            .WithColumn("FechaCreacion").AsDateTime2().NotNullable().WithDefault(SystemMethods.CurrentDateTime);
    }

    public override void Down()
    {
        // Borra la tabla con sus llaves foráneas.
        Delete.Table("BusquedasFavoritas");
    }
}
