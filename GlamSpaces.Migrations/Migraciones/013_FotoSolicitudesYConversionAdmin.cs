namespace GlamSpaces.Migrations.Migraciones;

// 013 · Foto del salón en las solicitudes y convertir cliente en administrador (Sprint 7). No cambia tablas.
// Up() ejecuta Database/18_FotoSolicitudesYConversionAdmin.sql (incrustado, sin copias a mano); es
// idempotente: FotoPrincipal en sp_Solicitud_ListarPorCliente y sp_Solicitud_ListarPorSalon, y los SP
// nuevos sp_Usuario_ObtenerCredenciales y sp_Usuario_ConvertirAAdministrador.
// Down() borra los SP nuevos y restaura los dos listados de 08_SolicitudesHU15.sql y 11_SolicitudesAdminHU18.sql.
[Migration(202611151000, "Foto del salón en las solicitudes y convertir cliente en administrador")]
public class M013_FotoSolicitudesYConversionAdmin : Migration
{
    internal const string Script = "Database.18_FotoSolicitudesYConversionAdmin.sql";
    internal const string ScriptHu15 = "Database.08_SolicitudesHU15.sql";
    internal const string ScriptHu18 = "Database.11_SolicitudesAdminHU18.sql";

    public override void Up()
    {
        foreach (var lote in ScriptsSql.Lotes(Script))
        {
            Execute.Sql(lote);
        }
    }

    public override void Down()
    {
        Execute.Sql("DROP PROCEDURE IF EXISTS dbo.sp_Usuario_ConvertirAAdministrador;");
        Execute.Sql("DROP PROCEDURE IF EXISTS dbo.sp_Usuario_ObtenerCredenciales;");
        Execute.Sql(M008_EstadoSolicitudesHU17.LoteDelProcedimiento(ScriptHu15, "sp_Solicitud_ListarPorCliente"));
        Execute.Sql(M008_EstadoSolicitudesHU17.LoteDelProcedimiento(ScriptHu18, "sp_Solicitud_ListarPorSalon"));
    }
}
