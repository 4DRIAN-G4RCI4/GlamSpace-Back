namespace GlamSpaces.Tests.Infrastructure;

// Sprint 10 · Repositorio de códigos, métodos nuevos de usuarios y el envío con Azure Communication
// Services. Los repositorios usan ConexionFalsa (Dapper real, sin BD); el cliente de Azure es un doble.
public class CorreoInfraestructuraPruebas
{
    private static DataTable Vigente(int intentos, bool vencido, string hash) => ConexionFalsa.Tabla(
        ("Id", 31), ("HashCodigo", hash), ("Intentos", intentos), ("Vencido", vencido));

    private static DataTable Intentos(int n) => ConexionFalsa.Tabla(("Intentos", n));

    // ---------- CodigoCorreoRepositorio.Guardar ----------

    [Fact]
    public async Task Guardar_MandaElHashNuncaElCodigoYLaVigencia()
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(0, "Código guardado.");

        // Actuar
        var r = await new CodigoCorreoRepositorio(conexion).Guardar(7, PropositoCodigo.Verificacion, "123456");

        // Afirmar
        Assert.True(r.Exito);
        Assert.Equal("sp_CodigoCorreo_Crear", conexion.ComandoEjecutado);
        var hash = Assert.IsType<string>(conexion.Parametros["HashCodigo"]);
        Assert.NotEqual("123456", hash);
        Assert.True(PasswordHasher.Verificar("123456", hash));
        Assert.Equal(15, conexion.Parametros["MinutosVigencia"]);
        Assert.Equal(60, conexion.Parametros["SegundosEspera"]);
    }

    [Fact]
    public async Task Guardar_PidioOtroHacePoco_RegresaEl1011DelSp()
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(CodigosError.EsperaParaReenviar, "Espera un momento antes de pedir otro código.");

        // Actuar
        var r = await new CodigoCorreoRepositorio(conexion).Guardar(7, PropositoCodigo.Verificacion, "123456");

        // Afirmar
        Assert.Equal(CodigosError.EsperaParaReenviar, r.Codigo);
    }

    // ---------- CodigoCorreoRepositorio.Validar ----------

    [Fact]
    public async Task Validar_SinCodigoVigente_EsVencido()
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(0, "Consulta exitosa.");

        // Actuar
        var r = await new CodigoCorreoRepositorio(conexion).Validar(7, PropositoCodigo.Verificacion, "123456");

        // Afirmar
        Assert.Equal(ResultadoCodigo.Vencido, r);
        Assert.Equal(["sp_CodigoCorreo_ObtenerVigente"], conexion.ComandosEjecutados);
    }

    [Theory]
    [InlineData(0, true, ResultadoCodigo.Vencido)]
    [InlineData(5, false, ResultadoCodigo.DemasiadosIntentos)]
    public async Task Validar_VencidoOBloqueado_NoRegistraIntento(int intentos, bool vencido, ResultadoCodigo esperado)
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(0, "Consulta exitosa.", Vigente(intentos, vencido, PasswordHasher.Hashear("123456")));

        // Actuar
        var r = await new CodigoCorreoRepositorio(conexion).Validar(7, PropositoCodigo.Verificacion, "123456");

        // Afirmar
        Assert.Equal(esperado, r);
        Assert.Single(conexion.ComandosEjecutados);
    }

    [Fact]
    public async Task Validar_Correcto_LoMarcaUsadoYEsValido()
    {
        // Arreglar
        var conexion = new ConexionFalsa()
            .Responder(0, "Consulta exitosa.", Vigente(1, false, PasswordHasher.Hashear("123456")))
            .Despues(0, "Intento registrado.", Intentos(1));

        // Actuar
        var r = await new CodigoCorreoRepositorio(conexion).Validar(7, PropositoCodigo.Verificacion, "123456");

        // Afirmar
        Assert.Equal(ResultadoCodigo.Valido, r);
        Assert.Equal("sp_CodigoCorreo_RegistrarIntento", conexion.ComandoEjecutado);
        Assert.Equal(31, conexion.Parametros["Id"]);
        Assert.Equal(true, conexion.Parametros["Correcto"]);
    }

    [Theory]
    [InlineData(2, ResultadoCodigo.Incorrecto)]
    [InlineData(5, ResultadoCodigo.DemasiadosIntentos)]
    public async Task Validar_Incorrecto_SumaUnIntentoYSeBloqueaAlQuinto(int intentosDespues, ResultadoCodigo esperado)
    {
        // Arreglar
        var conexion = new ConexionFalsa()
            .Responder(0, "Consulta exitosa.", Vigente(intentosDespues - 1, false, PasswordHasher.Hashear("123456")))
            .Despues(0, "Intento registrado.", Intentos(intentosDespues));

        // Actuar
        var r = await new CodigoCorreoRepositorio(conexion).Validar(7, PropositoCodigo.Verificacion, "999999");

        // Afirmar
        Assert.Equal(esperado, r);
        Assert.Equal(false, conexion.Parametros["Correcto"]);
    }

    // ---------- UsuarioRepositorio (Sprint 10) ----------

    private static DataTable UsuarioFila(bool verificado) => ConexionFalsa.Tabla(
        ("Id", 7), ("NombreCompleto", "Ana Pérez"), ("Correo", "ana@glamspaces.com"), ("PasswordHash", "hash"),
        ("TipoCuenta", "cliente"), ("NombreSalon", null), ("FechaRegistro", new DateTime(2026, 11, 1)), ("CorreoVerificado", verificado));

    [Fact]
    public async Task ObtenerPorCorreo_ConYSinCuenta()
    {
        // Arreglar
        var conCuenta = new ConexionFalsa().Responder(0, "Consulta exitosa.", UsuarioFila(false));
        var sinCuenta = new ConexionFalsa().Responder(0, "Consulta exitosa.");

        // Actuar
        var ana = await new UsuarioRepositorio(conCuenta).ObtenerPorCorreo("ana@glamspaces.com");
        var nadie = await new UsuarioRepositorio(sinCuenta).ObtenerPorCorreo("nadie@x.com");

        // Afirmar
        Assert.Equal("sp_Usuario_ObtenerPorCorreo", conCuenta.ComandoEjecutado);
        Assert.False(ana!.CorreoVerificado);
        Assert.Equal(7, ana.Id);
        Assert.Null(nadie);
    }

    [Fact]
    public async Task MarcarCorreoVerificado_RegresaElUsuarioVerificado()
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(0, "Tu correo quedó verificado.", UsuarioFila(true));

        // Actuar
        var r = await new UsuarioRepositorio(conexion).MarcarCorreoVerificado(7);

        // Afirmar
        Assert.Equal("sp_Usuario_MarcarCorreoVerificado", conexion.ComandoEjecutado);
        Assert.True(r.Datos!.CorreoVerificado);
    }

    [Fact]
    public async Task CambiarContrasena_MandaElHashYRegresaLoDelSp()
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(0, "Tu contraseña se cambió correctamente.");
        var noExiste = new ConexionFalsa().Responder(CodigosError.UsuarioNoEncontrado, "El usuario no existe.");

        // Actuar
        var r = await new UsuarioRepositorio(conexion).CambiarContrasena(7, "NuevaClave1");
        var error = await new UsuarioRepositorio(noExiste).CambiarContrasena(99, "NuevaClave1");

        // Afirmar
        Assert.True(r.Exito);
        Assert.Equal("sp_Usuario_CambiarContrasena", conexion.ComandoEjecutado);
        Assert.True(PasswordHasher.Verificar("NuevaClave1", Assert.IsType<string>(conexion.Parametros["PasswordHash"])));
        Assert.Equal(CodigosError.UsuarioNoEncontrado, error.Codigo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("corta")]
    public async Task CambiarContrasena_Corta_NiSiquieraTocaLaBd(string contrasena)
    {
        // Arreglar
        var conexion = new ConexionFalsa();

        // Actuar
        var r = await new UsuarioRepositorio(conexion).CambiarContrasena(7, contrasena);

        // Afirmar
        Assert.Equal(CodigosError.DatosInvalidos, r.Codigo);
        Assert.Null(conexion.ComandoEjecutado);
    }

    // ---------- EnvioCorreoAzure ----------

    [Fact]
    public async Task Enviar_MandaConElRemitenteSinEsperarLaEntrega()
    {
        // Arreglar
        var cliente = Substitute.For<EmailClient>();
        var envio = new EnvioCorreoAzure(cliente, "DoNotReply@glamspaces.azurecomm.net");

        // Actuar
        await envio.Enviar("ana@glamspaces.com", "Asunto", "<p>html</p>", "texto");

        // Afirmar
        await cliente.Received(1).SendAsync(WaitUntil.Started, "DoNotReply@glamspaces.azurecomm.net", "ana@glamspaces.com",
            "Asunto", "<p>html</p>", "texto", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Enviar_AzureFalla_LanzaErrorCorreo()
    {
        // Arreglar
        var cliente = Substitute.For<EmailClient>();
        cliente.SendAsync(default, default!, default!, default!, default!, default, default)
            .ThrowsAsyncForAnyArgs(new RequestFailedException(429, "Too many requests"));
        var envio = new EnvioCorreoAzure(cliente, "DoNotReply@glamspaces.azurecomm.net");

        // Actuar y afirmar
        var error = await Assert.ThrowsAsync<ErrorCorreo>(() => envio.Enviar("ana@glamspaces.com", "a", "h", "t"));
        Assert.IsType<RequestFailedException>(error.InnerException);
    }

    [Fact]
    public void ConstructorPublico_ArmaElClienteConLaCadenaDeConexion()
    {
        // Actuar: con una cadena válida se arma sin llamar a Azure
        var envio = new EnvioCorreoAzure(new OpcionesCorreo
        {
            CadenaConexion = "endpoint=https://pruebas.communication.azure.com/;accesskey=cHJ1ZWJhcw==",
            Remitente = "DoNotReply@pruebas.azurecomm.net",
        });

        // Afirmar
        Assert.NotNull(envio);
        Assert.Equal("Correo", OpcionesCorreo.Seccion);
    }
}
