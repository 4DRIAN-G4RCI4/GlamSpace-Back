namespace GlamSpaces.Migrations.Migraciones;

// 015 · Verificación del correo y recuperación de la cuenta con código (Sprint 10).
// Up() ejecuta Database/20_VerificacionCorreo.sql (incrustado), idempotente: columna
// Usuarios.CorreoVerificado (DEFAULT 1, las cuentas existentes quedan verificadas), tabla CodigosCorreo,
// los SP de usuario que regresan CorreoVerificado y los 5 SP nuevos de códigos.
// Down() borra los SP nuevos, restaura los 3 SP de usuario de 19_TryCatchBitacora.sql (con TRY/CATCH,
// sin CorreoVerificado) y quita la tabla y la columna con su DEFAULT.
[Migration(202611261000, "Verificación del correo y recuperación de la cuenta con código")]
public class M015_VerificacionCorreo : Migration
{
    internal const string Script = "Database.20_VerificacionCorreo.sql";

    internal static readonly string[] ProcedimientosNuevos =
    [
        "sp_CodigoCorreo_Crear",
        "sp_CodigoCorreo_ObtenerVigente",
        "sp_CodigoCorreo_RegistrarIntento",
        "sp_Usuario_MarcarCorreoVerificado",
        "sp_Usuario_CambiarContrasena",
    ];

    // Los SP de usuario que cambian en el 20; Down los deja como en el 19.
    internal static readonly string[] ProcedimientosRestaurados =
    [
        "sp_Usuario_Registrar",
        "sp_Usuario_ObtenerPorCorreo",
        "sp_Usuario_Obtener",
    ];

    public override void Up()
    {
        foreach (var lote in ScriptsSql.Lotes(Script))
        {
            Execute.Sql(lote);
        }
    }

    public override void Down()
    {
        foreach (var procedimiento in ProcedimientosNuevos)
        {
            Execute.Sql($"DROP PROCEDURE IF EXISTS dbo.{procedimiento};");
        }
        foreach (var procedimiento in ProcedimientosRestaurados)
        {
            Execute.Sql(M008_EstadoSolicitudesHU17.LoteDelProcedimiento(M014_TryCatchBitacora.Script, procedimiento));
        }
        Execute.Sql("DROP TABLE IF EXISTS dbo.CodigosCorreo;");
        Execute.Sql("IF COL_LENGTH(N'dbo.Usuarios', N'CorreoVerificado') IS NOT NULL BEGIN ALTER TABLE dbo.Usuarios DROP CONSTRAINT IF EXISTS DF_Usuarios_CorreoVerificado; ALTER TABLE dbo.Usuarios DROP COLUMN CorreoVerificado; END");
    }
}
