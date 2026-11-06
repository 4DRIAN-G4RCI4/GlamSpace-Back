namespace GlamSpaces.Migrations.Migraciones;

// 008 · HU-17 (Sprint 6): estado de solicitud y bloqueo de fecha.
// Up() ejecuta Database/09_EstadoSolicitudesHU17.sql (incrustado, sin copias a mano):
// índice UX_Solicitudes_Confirmada, disponibilidad con solicitudes confirmadas, sp_Solicitud_Crear
// que también revisa Reservaciones y el nuevo sp_Solicitud_CambiarEstado. Todo es idempotente.
// Down() deja todo como en la 007: quita el SP nuevo y el índice, y restaura las versiones anteriores
// de sp_Salon_Disponibilidad (02_StoredProcedures.sql) y sp_Solicitud_Crear (08_SolicitudesHU15.sql).
[Migration(202611051000, "HU-17: confirmar o rechazar solicitudes y bloqueo de fecha")]
public class M008_EstadoSolicitudesHU17 : Migration
{
    internal const string ScriptHu17 = "Database.09_EstadoSolicitudesHU17.sql";
    internal const string ScriptBase = "Database.02_StoredProcedures.sql";
    internal const string ScriptHu15 = "Database.08_SolicitudesHU15.sql";

    public override void Up()
    {
        foreach (var lote in ScriptsSql.Lotes(ScriptHu17))
        {
            Execute.Sql(lote);
        }
    }

    public override void Down()
    {
        Execute.Sql("DROP PROCEDURE IF EXISTS dbo.sp_Solicitud_CambiarEstado;");
        Execute.Sql("DROP INDEX IF EXISTS UX_Solicitudes_Confirmada ON dbo.Solicitudes;");
        Execute.Sql(LoteDelProcedimiento(ScriptBase, "sp_Salon_Disponibilidad"));
        Execute.Sql(LoteDelProcedimiento(ScriptHu15, "sp_Solicitud_Crear"));
    }

    // El lote del script que crea ese stored procedure (cada SP va en su propio lote, separado por GO).
    internal static string LoteDelProcedimiento(string recurso, string procedimiento) =>
        ScriptsSql.Lotes(recurso).Single(l => l.Contains($"CREATE OR ALTER PROCEDURE {procedimiento}\n")
                                           || l.Contains($"CREATE OR ALTER PROCEDURE {procedimiento}\r\n"));
}
