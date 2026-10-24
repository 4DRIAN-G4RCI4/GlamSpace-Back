namespace GlamSpaces.Migrations.Migraciones;

// 004 · Índice / restricción: un usuario no puede guardar el mismo salón dos veces como favorito.
[Migration(202610230930, "Índice único (UsuarioId, SalonId) en BusquedasFavoritas")]
public class M004_IndiceUnicoBusquedasFavoritas : Migration
{
    private const string Indice = "UX_BusquedasFavoritas_Usuario_Salon";

    public override void Up()
    {
        Create.Index(Indice).OnTable("BusquedasFavoritas")
            .OnColumn("UsuarioId").Ascending()
            .OnColumn("SalonId").Ascending()
            .WithOptions().Unique();
    }

    public override void Down()
    {
        Delete.Index(Indice).OnTable("BusquedasFavoritas");
    }
}
