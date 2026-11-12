namespace GlamSpaces.Migrations.Migraciones;

// 011 · HU-07 (fotos), Sprint 7: fotosPreview en la búsqueda. sp_Salon_Buscar regresa un tercer result
// set con hasta 5 fotos por salón de la página. La tabla FotosSalon no cambia.
// Up() ejecuta Database/13_FotosPreviewBuscar.sql (incrustado, sin copias a mano); es idempotente.
// Down() restaura sp_Salon_Buscar tal como está en Database/02_StoredProcedures.sql (2 result sets).
[Migration(202611131000, "HU-07 (fotos): fotosPreview en la búsqueda (tercer result set de sp_Salon_Buscar)")]
public class M011_FotosPreviewBuscar : Migration
{
    internal const string ScriptPreview = "Database.13_FotosPreviewBuscar.sql";
    internal const string ScriptBase = "Database.02_StoredProcedures.sql";

    public override void Up()
    {
        foreach (var lote in ScriptsSql.Lotes(ScriptPreview))
        {
            Execute.Sql(lote);
        }
    }

    public override void Down()
    {
        Execute.Sql(M008_EstadoSolicitudesHU17.LoteDelProcedimiento(ScriptBase, "sp_Salon_Buscar"));
    }
}
