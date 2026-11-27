namespace GlamSpaces.Domain.Servicios;

// Sprint 10 · Verificación del correo y recuperación de la cuenta con un código de 6 dígitos.
// Coordina las cuentas (IUsuarioRepositorio), los códigos (ICodigoCorreoRepositorio) y el envío
// (IEnvioCorreo). El hash, la vigencia (15 min), la espera entre códigos (60 s) y los 5 intentos
// viven en el repositorio y en los SP.
//
//   Registrar → crea la cuenta sin verificar y manda el código de verificación.
//   Login     → si la cuenta no ha verificado su correo: le reenvía el código y responde 1007.
//   VerificarCorreo / Restablecer → comprueban el código; Recuperar → manda el de recuperación.
// Recuperar y reenviar responden siempre lo mismo para no revelar qué correos tienen cuenta.
//   PedirCodigoConversion / ConvertirAAdministrador → convertir la cuenta pide contraseña Y código.
//   Ese código se guarda con el propósito "verificacion" (la cuenta ya está verificada, así que no
//   choca con otro uso) y se manda con la plantilla de conversión.
public class CuentaCorreoServicio
{
    public const string MensajeCodigoEnviado = "Si el correo tiene una cuenta, te mandamos un código.";
    private const string MensajeSinVerificar = "Verifica tu correo para entrar: te mandamos un código.";
    private const int LongitudMinPassword = 8;

    private readonly IUsuarioRepositorio _usuarios;
    private readonly ICodigoCorreoRepositorio _codigos;
    private readonly IEnvioCorreo _correo;

    public CuentaCorreoServicio(IUsuarioRepositorio usuarios, ICodigoCorreoRepositorio codigos, IEnvioCorreo correo)
    {
        _usuarios = usuarios;
        _codigos = codigos;
        _correo = correo;
    }

    public async Task<RespuestaApi<UsuarioResponse>> Registrar(RegistroRequest request)
    {
        var r = await _usuarios.Registrar(request);
        if (r.Exito && !r.Datos!.CorreoVerificado)
        {
            // Si el correo no sale, la cuenta igual queda creada: al iniciar sesión se le reenvía.
            await MandarCodigo(r.Datos, PropositoCodigo.Verificacion);
            r.Mensaje = "Cuenta creada. Te mandamos un código para verificar tu correo.";
        }
        return r;
    }

    public async Task<RespuestaApi<UsuarioResponse>> Login(LoginRequest request)
    {
        var r = await _usuarios.Login(request);
        if (!r.Exito || r.Datos!.CorreoVerificado) return r;

        await MandarCodigo(r.Datos, PropositoCodigo.Verificacion); // si ya se mandó hace poco (1011), sirve el anterior
        return RespuestaApi<UsuarioResponse>.Error(CodigosError.CorreoSinVerificar, MensajeSinVerificar);
    }

    public async Task<RespuestaApi<UsuarioResponse>> VerificarCorreo(VerificarCorreoRequest request)
    {
        var usuario = await _usuarios.ObtenerPorCorreo(request.Correo ?? string.Empty);
        // Sin cuenta: mismo mensaje que un código incorrecto (no revela si el correo existe).
        if (usuario is null) return ErrorDeCodigo<UsuarioResponse>(ResultadoCodigo.Incorrecto);
        if (usuario.CorreoVerificado) return RespuestaApi<UsuarioResponse>.Ok(usuario, "Tu correo ya estaba verificado.");

        var resultado = await _codigos.Validar(usuario.Id, PropositoCodigo.Verificacion, Limpiar(request.Codigo));
        return resultado == ResultadoCodigo.Valido
            ? await _usuarios.MarcarCorreoVerificado(usuario.Id)
            : ErrorDeCodigo<UsuarioResponse>(resultado);
    }

    public async Task<RespuestaApi<object?>> ReenviarCodigo(ReenviarCodigoRequest request)
    {
        if (!PropositoCodigo.EsValido(request.Proposito))
            return RespuestaApi<object?>.Error(CodigosError.DatosInvalidos, "El propósito debe ser \"verificacion\" o \"recuperacion\".");

        var usuario = await _usuarios.ObtenerPorCorreo(request.Correo ?? string.Empty);
        var noHaceFalta = usuario is null || (request.Proposito == PropositoCodigo.Verificacion && usuario.CorreoVerificado);
        if (noHaceFalta) return RespuestaApi<object?>.Ok(null, MensajeCodigoEnviado);

        var envio = await MandarCodigo(usuario!, request.Proposito);
        return envio.Codigo == CodigosError.EsperaParaReenviar || envio.Codigo == CodigosError.ErrorInterno
            ? envio
            : RespuestaApi<object?>.Ok(null, MensajeCodigoEnviado);
    }

    public async Task<RespuestaApi<object?>> Recuperar(RecuperarCuentaRequest request)
    {
        var usuario = await _usuarios.ObtenerPorCorreo(request.Correo ?? string.Empty);
        if (usuario is not null)
            await MandarCodigo(usuario, PropositoCodigo.Recuperacion);
        // Siempre lo mismo, exista o no la cuenta y aunque se haya pedido otro hace poco.
        return RespuestaApi<object?>.Ok(null, MensajeCodigoEnviado);
    }

    public async Task<RespuestaApi<object?>> Restablecer(RestablecerContrasenaRequest request)
    {
        if (string.IsNullOrEmpty(request.NuevaContrasena) || request.NuevaContrasena.Length < LongitudMinPassword)
            return RespuestaApi<object?>.Error(CodigosError.DatosInvalidos, $"La contraseña debe tener al menos {LongitudMinPassword} caracteres.");

        var usuario = await _usuarios.ObtenerPorCorreo(request.Correo ?? string.Empty);
        if (usuario is null) return ErrorDeCodigo<object?>(ResultadoCodigo.Incorrecto);

        var resultado = await _codigos.Validar(usuario.Id, PropositoCodigo.Recuperacion, Limpiar(request.Codigo));
        return resultado == ResultadoCodigo.Valido
            ? await _usuarios.CambiarContrasena(usuario.Id, request.NuevaContrasena)
            : ErrorDeCodigo<object?>(resultado);
    }

    // Paso 1 de la conversión: comprueba la contraseña y manda el código al correo de la cuenta.
    // 1005 no existe · 1003 contraseña incorrecta · 1006 ya es administrador · 1011 espera 60 s.
    public async Task<RespuestaApi<object?>> PedirCodigoConversion(ConvertirAdministradorRequest request)
    {
        var cuenta = await _usuarios.Obtener(request.UsuarioId);
        if (!cuenta.Exito) return RespuestaApi<object?>.Error(cuenta.Codigo, cuenta.Mensaje);

        var login = await _usuarios.Login(new LoginRequest { Correo = cuenta.Datos!.Correo, Password = request.Contrasena ?? string.Empty });
        if (!login.Exito) return RespuestaApi<object?>.Error(login.Codigo, login.Mensaje);
        if (login.Datos!.TipoCuenta == "administrador")
            return RespuestaApi<object?>.Error(CodigosError.YaEsAdministrador, "Tu cuenta ya es de administrador.");

        var envio = await MandarCodigo(login.Datos, PropositoCodigo.Verificacion, PlantillaCorreo.Conversion);
        return envio.Exito ? RespuestaApi<object?>.Ok(null, "Te mandamos un código a tu correo.") : envio;
    }

    // Paso 2: sin el código correcto no se convierte. Después el repositorio vuelve a comprobar la contraseña.
    public async Task<RespuestaApi<UsuarioResponse>> ConvertirAAdministrador(ConvertirAdministradorRequest request)
    {
        var resultado = await _codigos.Validar(request.UsuarioId, PropositoCodigo.Verificacion, Limpiar(request.Codigo));
        return resultado == ResultadoCodigo.Valido
            ? await _usuarios.ConvertirAAdministrador(request)
            : ErrorDeCodigo<UsuarioResponse>(resultado);
    }

    // Genera el código, lo guarda (hasheado) y lo manda. 1011 si se pidió otro hace menos de 60 s;
    // 5000 si el correo no salió.
    private async Task<RespuestaApi<object?>> MandarCodigo(UsuarioResponse usuario, string proposito, string? plantilla = null)
    {
        var codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var guardado = await _codigos.Guardar(usuario.Id, proposito, codigo);
        if (!guardado.Exito) return guardado;

        var mensaje = PlantillaCorreo.Para(plantilla ?? proposito, usuario.NombreCompleto, codigo);
        try
        {
            await _correo.Enviar(usuario.Correo, mensaje.Asunto, mensaje.Html, mensaje.Texto);
        }
        catch (ErrorCorreo)
        {
            return RespuestaApi<object?>.Error(CodigosError.ErrorInterno, "No se pudo mandar el correo. Intenta de nuevo en un momento.");
        }
        return RespuestaApi<object?>.Ok(null, MensajeCodigoEnviado);
    }

    private static string Limpiar(string? codigo) => new((codigo ?? string.Empty).Where(char.IsAsciiDigit).ToArray());

    private static RespuestaApi<T> ErrorDeCodigo<T>(ResultadoCodigo resultado) => resultado switch
    {
        ResultadoCodigo.Vencido => RespuestaApi<T>.Error(CodigosError.CodigoVencido, "El código ya venció. Pide uno nuevo."),
        ResultadoCodigo.DemasiadosIntentos => RespuestaApi<T>.Error(CodigosError.DemasiadosIntentos, "Hiciste demasiados intentos con este código. Pide uno nuevo."),
        _ => RespuestaApi<T>.Error(CodigosError.CodigoIncorrecto, "El código no es correcto."),
    };
}
