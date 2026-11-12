namespace GlamSpaces.Migrations.Migraciones;

// 010 · HU-07 (fotos): subida real de imágenes (Sprint 7). La tabla FotosSalon no cambia.
// Up() ejecuta Database/12_FotosSalonHU07.sql (incrustado, sin copias a mano); es idempotente.
[Migration(202611121000, "HU-07 (fotos): SP para agregar y eliminar fotos de un salón")]
public class M010_FotosSalonHU07 : Migration
{
    internal const string ScriptFotos = "Database.12_FotosSalonHU07.sql";

    public override void Up()
    {
        foreach (var lote in ScriptsSql.Lotes(ScriptFotos))
        {
            Execute.Sql(lote);
        }
    }

    public override void Down()
    {
        Execute.Sql("DROP PROCEDURE IF EXISTS dbo.sp_FotoSalon_Eliminar;");
        Execute.Sql("DROP PROCEDURE IF EXISTS dbo.sp_FotoSalon_Agregar;");
    }
}
