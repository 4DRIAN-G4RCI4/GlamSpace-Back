namespace GlamSpaces.Tests.Api;

// Sprint 10 · Verificación del correo y recuperación de la cuenta de punta a punta por HTTP
// (Program.cs real) con los repositorios y el correo como dobles: ni base de datos ni Azure.
// Revisa el contrato que usa el front: rutas, nombres de campo (camelCase) y status HTTP.
public sealed class CorreoEndpointsPruebas : IDisposable
{
    private readonly FabricaApi _fabrica = new();

    public void Dispose() => _fabrica.Dispose();

    private static UsuarioResponse Ana(bool verificado) =>
        new() { Id = 7, NombreCompleto = "Ana Pérez", Correo = "ana@glamspaces.com", TipoCuenta = "cliente", CorreoVerificado = verificado };

    private async Task<(HttpStatusCode Status, RespuestaApi<T> Cuerpo)> Post<T>(string ruta, object cuerpo)
    {
        var respuesta = await _fabrica.CreateClient().PostAsJsonAsync(ruta, cuerpo);
        return (respuesta.StatusCode, (await respuesta.Content.ReadFromJsonAsync<RespuestaApi<T>>())!);
    }

    [Fact]
    public async Task Registro_CuentaNueva_RegresaCorreoVerificadoFalseYMandaElCodigo()
    {
        // Arreglar
        _fabrica.Usuarios.Registrar(Arg.Any<RegistroRequest>()).Returns(RespuestaApi<UsuarioResponse>.Ok(Ana(false), "Cuenta creada correctamente."));
        _fabrica.Codigos.Guardar(default, default!, default!).ReturnsForAnyArgs(RespuestaApi<object?>.Ok(null, "Código guardado."));
        var respuesta = await _fabrica.CreateClient().PostAsJsonAsync("/api/usuarios/registro",
            new { nombreCompleto = "Ana Pérez", correo = "ana@glamspaces.com", password = "Segura123", tipoCuenta = "cliente" });

        // Actuar
        var json = await respuesta.Content.ReadAsStringAsync();

        // Afirmar: el front lee "correoVerificado": false para ir a la pantalla del código
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("\"correoVerificado\":false", json);
        await _fabrica.Correo.ReceivedWithAnyArgs(1).Enviar(default!, default!, default!, default!);
    }

    [Fact]
    public async Task Login_SinVerificar_Responde403Con1007()
    {
        // Arreglar
        _fabrica.Usuarios.Login(Arg.Any<LoginRequest>()).Returns(RespuestaApi<UsuarioResponse>.Ok(Ana(false), "Inicio de sesión correcto."));
        _fabrica.Codigos.Guardar(default, default!, default!).ReturnsForAnyArgs(RespuestaApi<object?>.Ok(null, "Código guardado."));

        // Actuar
        var (status, cuerpo) = await Post<UsuarioResponse>("/api/usuarios/login", new { correo = "ana@glamspaces.com", password = "Segura123" });

        // Afirmar
        Assert.Equal(HttpStatusCode.Forbidden, status);
        Assert.Equal(CodigosError.CorreoSinVerificar, cuerpo.Codigo);
    }

    [Fact]
    public async Task VerificarCorreo_CodigoValido_Regresa200ConElUsuario()
    {
        // Arreglar
        _fabrica.Usuarios.ObtenerPorCorreo("ana@glamspaces.com").Returns(Ana(false));
        _fabrica.Codigos.Validar(7, "verificacion", "123456").Returns(ResultadoCodigo.Valido);
        _fabrica.Usuarios.MarcarCorreoVerificado(7).Returns(RespuestaApi<UsuarioResponse>.Ok(Ana(true), "Tu correo quedó verificado."));

        // Actuar
        var (status, cuerpo) = await Post<UsuarioResponse>("/api/usuarios/verificarCorreo", new { correo = "ana@glamspaces.com", codigo = "123456" });

        // Afirmar
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(cuerpo.Datos!.CorreoVerificado);
    }

    [Fact]
    public async Task ReenviarCodigo_PidioOtroHacePoco_Responde429Con1011()
    {
        // Arreglar
        _fabrica.Usuarios.ObtenerPorCorreo(Arg.Any<string>()).Returns(Ana(false));
        _fabrica.Codigos.Guardar(default, default!, default!)
            .ReturnsForAnyArgs(RespuestaApi<object?>.Error(CodigosError.EsperaParaReenviar, "Espera un momento antes de pedir otro código."));

        // Actuar
        var (status, cuerpo) = await Post<object>("/api/usuarios/reenviarCodigo", new { correo = "ana@glamspaces.com", proposito = "verificacion" });

        // Afirmar
        Assert.Equal(HttpStatusCode.TooManyRequests, status);
        Assert.Equal(CodigosError.EsperaParaReenviar, cuerpo.Codigo);
    }

    [Fact]
    public async Task Recuperar_SinCuenta_Regresa200ConElMensajeDeSiempre()
    {
        // Actuar
        var (status, cuerpo) = await Post<object>("/api/usuarios/recuperar", new { correo = "nadie@x.com" });

        // Afirmar
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(CuentaCorreoServicio.MensajeCodigoEnviado, cuerpo.Mensaje);
    }

    [Fact]
    public async Task Restablecer_CodigoValido_Regresa200()
    {
        // Arreglar
        _fabrica.Usuarios.ObtenerPorCorreo("ana@glamspaces.com").Returns(Ana(true));
        _fabrica.Codigos.Validar(7, "recuperacion", "654321").Returns(ResultadoCodigo.Valido);
        _fabrica.Usuarios.CambiarContrasena(7, "NuevaClave1").Returns(RespuestaApi<object?>.Ok(null, "Tu contraseña se cambió correctamente."));

        // Actuar
        var (status, cuerpo) = await Post<object>("/api/usuarios/restablecer", new { correo = "ana@glamspaces.com", codigo = "654321", nuevaContrasena = "NuevaClave1" });

        // Afirmar
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(cuerpo.Exito);
    }

    [Fact]
    public void Program_RegistraElEnvioDeAzureConLaConfiguracion()
    {
        // Arreglar: una fábrica que NO reemplaza el envío de correos
        using var fabrica = new FabricaSinDobleDeCorreo();

        // Actuar: se resuelve el registro real de Program.cs (no manda nada: solo arma el cliente)
        using var alcance = fabrica.Services.CreateScope();
        var envio = alcance.ServiceProvider.GetRequiredService<IEnvioCorreo>();

        // Afirmar
        Assert.IsType<EnvioCorreoAzure>(envio);
    }

    // Igual que FabricaApi pero dejando el IEnvioCorreo de Program.cs (para probar su registro).
    private sealed class FabricaSinDobleDeCorreo : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Pruebas");
            builder.UseSetting("ConnectionStrings:GlamSpacesDb", "");
            builder.UseSetting("Almacenamiento:CadenaConexion", "UseDevelopmentStorage=true");
            builder.UseSetting("Almacenamiento:Contenedor", "fotos-pruebas");
            builder.UseSetting("Correo:CadenaConexion", "endpoint=https://pruebas.communication.azure.com/;accesskey=cHJ1ZWJhcw==");
            builder.UseSetting("Correo:Remitente", "DoNotReply@pruebas.azurecomm.net");
        }
    }
}
