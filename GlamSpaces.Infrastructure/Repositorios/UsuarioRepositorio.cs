namespace GlamSpaces.Infrastructure.Repositorios;

public class UsuarioRepositorio : IUsuarioRepositorio
{
    private const int LongitudMinPassword = 8;

    private readonly IDbConnection _db;

    public UsuarioRepositorio(IDbConnection db)
    {
        _db = db;
    }

    public async Task<RespuestaApi<UsuarioResponse>> Registrar(RegistroRequest request)
    {
        // La contraseña se valida y se hashea aquí: el SP nunca la recibe en texto plano.
        if (string.IsNullOrEmpty(request.Password) || request.Password.Length < LongitudMinPassword)
            return RespuestaApi<UsuarioResponse>.Error(CodigosError.DatosInvalidos,
                $"La contraseña debe tener al menos {LongitudMinPassword} caracteres.");

        var p = StoredProcedure.Parametros(new
        {
            request.NombreCompleto,
            request.Correo,
            PasswordHash = PasswordHasher.Hashear(request.Password),
            request.TipoCuenta,
            request.NombreSalon,
        });
        var usuario = await _db.QueryFirstOrDefaultAsync<Usuario>(
            "sp_Usuario_Registrar", p, commandType: CommandType.StoredProcedure);

        return p.Respuesta(usuario, UsuarioResponse.Desde);
    }

    public async Task<RespuestaApi<UsuarioResponse>> Login(LoginRequest request)
    {
        var p = StoredProcedure.Parametros(new { request.Correo });
        var usuario = await _db.QueryFirstOrDefaultAsync<Usuario>(
            "sp_Usuario_ObtenerPorCorreo", p, commandType: CommandType.StoredProcedure);

        // Mensaje genérico a propósito: no revela si falló el correo o la contraseña.
        if (usuario == null || !PasswordHasher.Verificar(request.Password ?? "", usuario.PasswordHash))
            return RespuestaApi<UsuarioResponse>.Error(CodigosError.CredencialesIncorrectas,
                "Correo o contraseña incorrectos.");

        return RespuestaApi<UsuarioResponse>.Ok(UsuarioResponse.Desde(usuario), "Inicio de sesión correcto.");
    }

    public async Task<RespuestaApi<UsuarioResponse>> Obtener(int id)
    {
        var p = StoredProcedure.Parametros(new { Id = id });
        var usuario = await _db.QueryFirstOrDefaultAsync<Usuario>(
            "sp_Usuario_Obtener", p, commandType: CommandType.StoredProcedure);

        return p.Respuesta(usuario, UsuarioResponse.Desde);
    }

    // Verifica la contraseña con el mismo PBKDF2 del login y después cambia el rol en el SP.
    // El orden (existe → contraseña → ya es admin) evita revelar el rol de una cuenta ajena.
    public async Task<RespuestaApi<UsuarioResponse>> ConvertirAAdministrador(ConvertirAdministradorRequest request)
    {
        var credenciales = StoredProcedure.Parametros(new { Id = request.UsuarioId });
        var usuario = await _db.QueryFirstOrDefaultAsync<Usuario>(
            "sp_Usuario_ObtenerCredenciales", credenciales, commandType: CommandType.StoredProcedure);
        if (usuario == null)
            return credenciales.Respuesta(usuario, UsuarioResponse.Desde); // 1005: el usuario no existe

        if (!PasswordHasher.Verificar(request.Contrasena ?? "", usuario.PasswordHash))
            return RespuestaApi<UsuarioResponse>.Error(CodigosError.CredencialesIncorrectas, "La contraseña es incorrecta.");

        var p = StoredProcedure.Parametros(new { Id = request.UsuarioId });
        var convertido = await _db.QueryFirstOrDefaultAsync<Usuario>(
            "sp_Usuario_ConvertirAAdministrador", p, commandType: CommandType.StoredProcedure);

        return p.Respuesta(convertido, UsuarioResponse.Desde);
    }
}
