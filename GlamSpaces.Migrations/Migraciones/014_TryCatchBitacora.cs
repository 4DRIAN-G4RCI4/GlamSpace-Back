namespace GlamSpaces.Migrations.Migraciones;

// 014 · TRY/CATCH en todos los stored procedures y bitácora de errores (Sprint 10, pedido del profe).
// Up() ejecuta Database/19_TryCatchBitacora.sql (incrustado): crea BitacoraErrores y
// sp_Bitacora_RegistrarError, y vuelve a crear los 22 SP con su cuerpo dentro de TRY/CATCH. Idempotente.
// Down() restaura cada SP desde el script de su versión anterior (sin TRY/CATCH) y después borra
// sp_Bitacora_RegistrarError y la tabla BitacoraErrores.
[Migration(202611251000, "TRY/CATCH en los stored procedures y bitácora de errores")]
public class M014_TryCatchBitacora : Migration
{
    internal const string Script = "Database.19_TryCatchBitacora.sql";

    // De qué script sale la versión anterior de cada SP (la que restaura Down).
    internal static readonly IReadOnlyDictionary<string, string> VersionAnterior = new Dictionary<string, string>
    {
        ["sp_FotoSalon_Agregar"] = "Database.12_FotosSalonHU07.sql",
        ["sp_FotoSalon_Eliminar"] = "Database.12_FotosSalonHU07.sql",
        ["sp_Paquete_Actualizar"] = "Database.02_StoredProcedures.sql",
        ["sp_Paquete_Crear"] = "Database.02_StoredProcedures.sql",
        ["sp_Paquete_Eliminar"] = "Database.02_StoredProcedures.sql",
        ["sp_Salon_Actualizar"] = "Database.02_StoredProcedures.sql",
        ["sp_Salon_Buscar"] = "Database.13_FotosPreviewBuscar.sql",
        ["sp_Salon_Crear"] = "Database.02_StoredProcedures.sql",
        ["sp_Salon_Disponibilidad"] = "Database.09_EstadoSolicitudesHU17.sql",
        ["sp_Salon_Eliminar"] = "Database.14_MisSalonesAdmin.sql",
        ["sp_Salon_Listar"] = "Database.14_MisSalonesAdmin.sql",
        ["sp_Salon_Obtener"] = "Database.02_StoredProcedures.sql",
        ["sp_Solicitud_CambiarEstado"] = "Database.09_EstadoSolicitudesHU17.sql",
        ["sp_Solicitud_Crear"] = "Database.09_EstadoSolicitudesHU17.sql",
        ["sp_Solicitud_ListarPorCliente"] = "Database.18_FotoSolicitudesYConversionAdmin.sql",
        ["sp_Solicitud_ListarPorSalon"] = "Database.18_FotoSolicitudesYConversionAdmin.sql",
        ["sp_Solicitud_Obtener"] = "Database.08_SolicitudesHU15.sql",
        ["sp_Usuario_ConvertirAAdministrador"] = "Database.18_FotoSolicitudesYConversionAdmin.sql",
        ["sp_Usuario_Obtener"] = "Database.02_StoredProcedures.sql",
        ["sp_Usuario_ObtenerCredenciales"] = "Database.18_FotoSolicitudesYConversionAdmin.sql",
        ["sp_Usuario_ObtenerPorCorreo"] = "Database.02_StoredProcedures.sql",
        ["sp_Usuario_Registrar"] = "Database.02_StoredProcedures.sql",
    };

    public override void Up()
    {
        foreach (var lote in ScriptsSql.Lotes(Script))
        {
            Execute.Sql(lote);
        }
    }

    public override void Down()
    {
        // Primero los SP (ya no llaman a la bitácora), después el SP y la tabla de la bitácora.
        foreach (var (procedimiento, script) in VersionAnterior)
        {
            Execute.Sql(M008_EstadoSolicitudesHU17.LoteDelProcedimiento(script, procedimiento));
        }
        Execute.Sql("DROP PROCEDURE IF EXISTS dbo.sp_Bitacora_RegistrarError;");
        Execute.Sql("DROP TABLE IF EXISTS dbo.BitacoraErrores;");
    }
}
