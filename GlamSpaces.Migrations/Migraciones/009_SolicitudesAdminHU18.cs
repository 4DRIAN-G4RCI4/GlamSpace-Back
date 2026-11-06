namespace GlamSpaces.Migrations.Migraciones;

// 009 · HU-18 (Sprint 6): listado de las solicitudes de un salón para su administrador.
// Up() ejecuta Database/11_SolicitudesAdminHU18.sql (incrustado, sin copias a mano); es idempotente.
// Aceptar y rechazar usan sp_Solicitud_CambiarEstado de la 008, así que aquí no cambia nada más.
[Migration(202611051100, "HU-18: listado de solicitudes de un salón para su administrador")]
public class M009_SolicitudesAdminHU18 : Migration
{
    internal const string ScriptHu18 = "Database.11_SolicitudesAdminHU18.sql";

    public override void Up()
    {
        foreach (var lote in ScriptsSql.Lotes(ScriptHu18))
        {
            Execute.Sql(lote);
        }
    }

    public override void Down()
    {
        Execute.Sql("DROP PROCEDURE IF EXISTS dbo.sp_Solicitud_ListarPorSalon;");
    }
}
