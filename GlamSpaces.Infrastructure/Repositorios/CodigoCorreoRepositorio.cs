namespace GlamSpaces.Infrastructure.Repositorios;

// Sprint 10 · Códigos de 6 dígitos que se mandan por correo. Solo se guarda el hash (el mismo PBKDF2
// de las contraseñas); la vigencia y la espera entre códigos las aplica sp_CodigoCorreo_Crear con el
// reloj de la BD.
public class CodigoCorreoRepositorio : ICodigoCorreoRepositorio
{
    internal const int MinutosVigencia = 15;
    internal const int SegundosEspera = 60;
    internal const int MaximoIntentos = 5;

    private readonly IDbConnection _db;

    public CodigoCorreoRepositorio(IDbConnection db)
    {
        _db = db;
    }

    public async Task<RespuestaApi<object?>> Guardar(int usuarioId, string proposito, string codigo)
    {
        var p = StoredProcedure.Parametros(new
        {
            UsuarioId = usuarioId,
            Proposito = proposito,
            HashCodigo = PasswordHasher.Hashear(codigo),
            MinutosVigencia,
            SegundosEspera,
        });
        await _db.ExecuteAsync("sp_CodigoCorreo_Crear", p, commandType: CommandType.StoredProcedure);

        return p.RespuestaSinDatos();
    }

    public async Task<ResultadoCodigo> Validar(int usuarioId, string proposito, string codigo)
    {
        var vigente = await _db.QueryFirstOrDefaultAsync<CodigoVigente>(
            "sp_CodigoCorreo_ObtenerVigente",
            StoredProcedure.Parametros(new { UsuarioId = usuarioId, Proposito = proposito }),
            commandType: CommandType.StoredProcedure);

        if (vigente is null || vigente.Vencido) return ResultadoCodigo.Vencido;
        if (vigente.Intentos >= MaximoIntentos) return ResultadoCodigo.DemasiadosIntentos;

        var correcto = PasswordHasher.Verificar(codigo, vigente.HashCodigo);
        var intentos = await _db.QueryFirstOrDefaultAsync<int>(
            "sp_CodigoCorreo_RegistrarIntento",
            StoredProcedure.Parametros(new { vigente.Id, Correcto = correcto }),
            commandType: CommandType.StoredProcedure);

        if (correcto) return ResultadoCodigo.Valido;
        return intentos >= MaximoIntentos ? ResultadoCodigo.DemasiadosIntentos : ResultadoCodigo.Incorrecto;
    }

    // Renglón de sp_CodigoCorreo_ObtenerVigente.
    internal sealed class CodigoVigente
    {
        public int Id { get; set; }
        public string HashCodigo { get; set; } = string.Empty;
        public int Intentos { get; set; }
        public bool Vencido { get; set; }
    }
}
