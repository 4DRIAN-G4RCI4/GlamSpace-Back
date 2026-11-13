namespace GlamSpaces.Migrations.Migraciones;

// 012 · "Mis salones" del administrador (mejoras de HU-07 / HU-08, Sprint 7). No cambia tablas.
// Up() ejecuta Database/14_MisSalonesAdmin.sql (incrustado, sin copias a mano); es idempotente:
// sp_Salon_Listar con FotoPrincipal y TotalFotos, y sp_Salon_Eliminar nuevo.
// Down() borra sp_Salon_Eliminar y restaura sp_Salon_Listar tal como está en Database/02_StoredProcedures.sql.
[Migration(202611141000, "Mis salones: fotoPrincipal y totalFotos en el listado, y eliminar salón")]
public class M012_MisSalonesAdmin : Migration
{
    internal const string ScriptMisSalones = "Database.14_MisSalonesAdmin.sql";
    internal const string ScriptBase = "Database.02_StoredProcedures.sql";

    public override void Up()
    {
        foreach (var lote in ScriptsSql.Lotes(ScriptMisSalones))
        {
            Execute.Sql(lote);
        }
    }

    public override void Down()
    {
        Execute.Sql("DROP PROCEDURE IF EXISTS dbo.sp_Salon_Eliminar;");
        Execute.Sql(M008_EstadoSolicitudesHU17.LoteDelProcedimiento(ScriptBase, "sp_Salon_Listar"));
    }
}
