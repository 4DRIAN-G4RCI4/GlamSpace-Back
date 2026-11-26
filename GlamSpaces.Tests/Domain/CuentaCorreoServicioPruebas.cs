namespace GlamSpaces.Tests.Domain;

// Sprint 10 · Verificación del correo y recuperación de la cuenta: el servicio con dobles de las
// cuentas, los códigos y el correo. Ninguna prueba manda correos ni toca la BD.
public class CuentaCorreoServicioPruebas
{
    private readonly IUsuarioRepositorio _usuarios = Substitute.For<IUsuarioRepositorio>();
    private readonly ICodigoCorreoRepositorio _codigos = Substitute.For<ICodigoCorreoRepositorio>();
    private readonly IEnvioCorreo _correo = Substitute.For<IEnvioCorreo>();

    private CuentaCorreoServicio Servicio => new(_usuarios, _codigos, _correo);

    private static UsuarioResponse Ana(bool verificado) =>
        new() { Id = 7, NombreCompleto = "Ana Pérez", Correo = "ana@glamspaces.com", TipoCuenta = "cliente", CorreoVerificado = verificado };

    private static RespuestaApi<object?> Ok() => RespuestaApi<object?>.Ok(null, "ok");

    public CuentaCorreoServicioPruebas()
    {
        _codigos.Guardar(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>()).Returns(Ok());
    }

    // ---------- Registrar ----------

    [Fact]
    public async Task Registrar_CuentaSinVerificar_GuardaYMandaElCodigoDeVerificacion()
    {
        // Arreglar
        var request = new RegistroRequest { Correo = "ana@glamspaces.com", Password = "Segura123", TipoCuenta = "cliente" };
        _usuarios.Registrar(request).Returns(RespuestaApi<UsuarioResponse>.Ok(Ana(false), "Cuenta creada correctamente."));

        // Actuar
        var r = await Servicio.Registrar(request);

        // Afirmar: un código de 6 dígitos, guardado y mandado por correo con el mismo valor
        Assert.True(r.Exito);
        Assert.False(r.Datos!.CorreoVerificado);
        Assert.Contains("código", r.Mensaje);
        var codigo = (string)_codigos.ReceivedCalls().Single().GetArguments()[2]!;
        Assert.Matches("^[0-9]{6}$", codigo);
        await _codigos.Received(1).Guardar(7, PropositoCodigo.Verificacion, codigo);
        await _correo.Received(1).Enviar("ana@glamspaces.com", Arg.Is<string>(a => a.Contains(codigo)), Arg.Is<string>(h => h.Contains(codigo)), Arg.Is<string>(t => t.Contains(codigo)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Registrar_YaVerificadaOConError_NoMandaNada(bool exito)
    {
        // Arreglar
        _usuarios.Registrar(Arg.Any<RegistroRequest>()).Returns(exito
            ? RespuestaApi<UsuarioResponse>.Ok(Ana(true), "Cuenta creada correctamente.")
            : RespuestaApi<UsuarioResponse>.Error(CodigosError.CorreoDuplicado, "Ya existe una cuenta con este correo."));

        // Actuar
        var r = await Servicio.Registrar(new RegistroRequest());

        // Afirmar
        Assert.Equal(exito, r.Exito);
        await _codigos.DidNotReceiveWithAnyArgs().Guardar(default, default!, default!);
    }

    // ---------- Login ----------

    [Fact]
    public async Task Login_SinVerificar_ReenviaElCodigoYResponde1007()
    {
        // Arreglar
        _usuarios.Login(Arg.Any<LoginRequest>()).Returns(RespuestaApi<UsuarioResponse>.Ok(Ana(false), "Inicio de sesión correcto."));

        // Actuar
        var r = await Servicio.Login(new LoginRequest());

        // Afirmar
        Assert.Equal(CodigosError.CorreoSinVerificar, r.Codigo);
        Assert.Null(r.Datos);
        await _codigos.Received(1).Guardar(7, PropositoCodigo.Verificacion, Arg.Any<string>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Login_VerificadaOConError_RegresaLoDelRepositorio(bool exito)
    {
        // Arreglar
        var respuesta = exito
            ? RespuestaApi<UsuarioResponse>.Ok(Ana(true), "Inicio de sesión correcto.")
            : RespuestaApi<UsuarioResponse>.Error(CodigosError.CredencialesIncorrectas, "Correo o contraseña incorrectos.");
        _usuarios.Login(Arg.Any<LoginRequest>()).Returns(respuesta);

        // Actuar
        var r = await Servicio.Login(new LoginRequest());

        // Afirmar
        Assert.Same(respuesta, r);
        await _codigos.DidNotReceiveWithAnyArgs().Guardar(default, default!, default!);
    }

    // ---------- VerificarCorreo ----------

    [Fact]
    public async Task VerificarCorreo_SinCuenta_RespondeComoCodigoIncorrecto()
    {
        // Actuar
        var r = await Servicio.VerificarCorreo(new VerificarCorreoRequest { Correo = null!, Codigo = "123456" });

        // Afirmar
        Assert.Equal(CodigosError.CodigoIncorrecto, r.Codigo);
        await _codigos.DidNotReceiveWithAnyArgs().Validar(default, default!, default!);
    }

    [Fact]
    public async Task VerificarCorreo_YaVerificada_RegresaElUsuarioSinValidar()
    {
        // Arreglar
        _usuarios.ObtenerPorCorreo("ana@glamspaces.com").Returns(Ana(true));

        // Actuar
        var r = await Servicio.VerificarCorreo(new VerificarCorreoRequest { Correo = "ana@glamspaces.com" });

        // Afirmar
        Assert.True(r.Exito);
        Assert.Equal(7, r.Datos!.Id);
        await _codigos.DidNotReceiveWithAnyArgs().Validar(default, default!, default!);
    }

    [Fact]
    public async Task VerificarCorreo_CodigoValido_LimpiaElCodigoYMarcaLaCuenta()
    {
        // Arreglar
        _usuarios.ObtenerPorCorreo("ana@glamspaces.com").Returns(Ana(false));
        _codigos.Validar(7, PropositoCodigo.Verificacion, "123456").Returns(ResultadoCodigo.Valido);
        var verificada = RespuestaApi<UsuarioResponse>.Ok(Ana(true), "Tu correo quedó verificado.");
        _usuarios.MarcarCorreoVerificado(7).Returns(verificada);

        // Actuar
        var r = await Servicio.VerificarCorreo(new VerificarCorreoRequest { Correo = "ana@glamspaces.com", Codigo = " 12 34-56 " });

        // Afirmar
        Assert.Same(verificada, r);
    }

    [Theory]
    [InlineData(ResultadoCodigo.Incorrecto, CodigosError.CodigoIncorrecto)]
    [InlineData(ResultadoCodigo.Vencido, CodigosError.CodigoVencido)]
    [InlineData(ResultadoCodigo.DemasiadosIntentos, CodigosError.DemasiadosIntentos)]
    public async Task VerificarCorreo_CodigoQueNoSirve_RegresaSuError(ResultadoCodigo resultado, int codigoEsperado)
    {
        // Arreglar
        _usuarios.ObtenerPorCorreo(Arg.Any<string>()).Returns(Ana(false));
        _codigos.Validar(7, PropositoCodigo.Verificacion, Arg.Any<string>()).Returns(resultado);

        // Actuar
        var r = await Servicio.VerificarCorreo(new VerificarCorreoRequest { Correo = "ana@glamspaces.com", Codigo = null! });

        // Afirmar
        Assert.Equal(codigoEsperado, r.Codigo);
        await _usuarios.DidNotReceiveWithAnyArgs().MarcarCorreoVerificado(default);
    }

    // ---------- ReenviarCodigo ----------

    [Fact]
    public async Task ReenviarCodigo_PropositoInvalido_Responde1001()
    {
        // Actuar
        var r = await Servicio.ReenviarCodigo(new ReenviarCodigoRequest { Correo = "ana@glamspaces.com", Proposito = "otro" });

        // Afirmar
        Assert.Equal(CodigosError.DatosInvalidos, r.Codigo);
    }

    [Fact]
    public async Task ReenviarCodigo_SinCuentaOYaVerificada_RespondeIgualSinMandar()
    {
        // Arreglar
        _usuarios.ObtenerPorCorreo("ana@glamspaces.com").Returns(Ana(true));

        // Actuar
        var sinCuenta = await Servicio.ReenviarCodigo(new ReenviarCodigoRequest { Correo = null!, Proposito = PropositoCodigo.Verificacion });
        var yaVerificada = await Servicio.ReenviarCodigo(new ReenviarCodigoRequest { Correo = "ana@glamspaces.com", Proposito = PropositoCodigo.Verificacion });

        // Afirmar
        Assert.Equal(CuentaCorreoServicio.MensajeCodigoEnviado, sinCuenta.Mensaje);
        Assert.Equal(CuentaCorreoServicio.MensajeCodigoEnviado, yaVerificada.Mensaje);
        await _codigos.DidNotReceiveWithAnyArgs().Guardar(default, default!, default!);
    }

    [Fact]
    public async Task ReenviarCodigo_Recuperacion_MandaAunqueLaCuentaEsteVerificada()
    {
        // Arreglar
        _usuarios.ObtenerPorCorreo("ana@glamspaces.com").Returns(Ana(true));

        // Actuar
        var r = await Servicio.ReenviarCodigo(new ReenviarCodigoRequest { Correo = "ana@glamspaces.com", Proposito = PropositoCodigo.Recuperacion });

        // Afirmar
        Assert.True(r.Exito);
        await _codigos.Received(1).Guardar(7, PropositoCodigo.Recuperacion, Arg.Any<string>());
        await _correo.ReceivedWithAnyArgs(1).Enviar(default!, default!, default!, default!);
    }

    [Fact]
    public async Task ReenviarCodigo_PidioOtroHacePoco_Responde1011SinMandar()
    {
        // Arreglar
        _usuarios.ObtenerPorCorreo(Arg.Any<string>()).Returns(Ana(false));
        _codigos.Guardar(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(RespuestaApi<object?>.Error(CodigosError.EsperaParaReenviar, "Espera un momento antes de pedir otro código."));

        // Actuar
        var r = await Servicio.ReenviarCodigo(new ReenviarCodigoRequest { Correo = "ana@glamspaces.com", Proposito = PropositoCodigo.Verificacion });

        // Afirmar
        Assert.Equal(CodigosError.EsperaParaReenviar, r.Codigo);
        await _correo.DidNotReceiveWithAnyArgs().Enviar(default!, default!, default!, default!);
    }

    [Fact]
    public async Task ReenviarCodigo_ElCorreoNoSale_Responde5000()
    {
        // Arreglar
        _usuarios.ObtenerPorCorreo(Arg.Any<string>()).Returns(Ana(false));
        _correo.Enviar(default!, default!, default!, default!).ThrowsAsyncForAnyArgs(new ErrorCorreo(new InvalidOperationException("Azure")));

        // Actuar
        var r = await Servicio.ReenviarCodigo(new ReenviarCodigoRequest { Correo = "ana@glamspaces.com", Proposito = PropositoCodigo.Verificacion });

        // Afirmar
        Assert.Equal(CodigosError.ErrorInterno, r.Codigo);
        Assert.DoesNotContain("Azure", r.Mensaje);
    }

    // ---------- Recuperar ----------

    [Fact]
    public async Task Recuperar_SiempreRespondeLoMismoYSoloMandaSiHayCuenta()
    {
        // Arreglar
        _usuarios.ObtenerPorCorreo("ana@glamspaces.com").Returns(Ana(true));

        // Actuar
        var conCuenta = await Servicio.Recuperar(new RecuperarCuentaRequest { Correo = "ana@glamspaces.com" });
        var sinCuenta = await Servicio.Recuperar(new RecuperarCuentaRequest { Correo = null! });

        // Afirmar
        Assert.Equal(CuentaCorreoServicio.MensajeCodigoEnviado, conCuenta.Mensaje);
        Assert.Equal(CuentaCorreoServicio.MensajeCodigoEnviado, sinCuenta.Mensaje);
        await _codigos.Received(1).Guardar(7, PropositoCodigo.Recuperacion, Arg.Any<string>());
    }

    // ---------- Restablecer ----------

    [Theory]
    [InlineData("")]
    [InlineData("corta")]
    public async Task Restablecer_ContrasenaCorta_Responde1001SinBuscarLaCuenta(string contrasena)
    {
        // Actuar
        var r = await Servicio.Restablecer(new RestablecerContrasenaRequest { Correo = "ana@glamspaces.com", Codigo = "123456", NuevaContrasena = contrasena });

        // Afirmar
        Assert.Equal(CodigosError.DatosInvalidos, r.Codigo);
        await _usuarios.DidNotReceiveWithAnyArgs().ObtenerPorCorreo(default!);
    }

    [Fact]
    public async Task Restablecer_SinCuenta_RespondeComoCodigoIncorrecto()
    {
        // Actuar
        var r = await Servicio.Restablecer(new RestablecerContrasenaRequest { Correo = null!, Codigo = "123456", NuevaContrasena = "NuevaClave1" });

        // Afirmar
        Assert.Equal(CodigosError.CodigoIncorrecto, r.Codigo);
    }

    [Fact]
    public async Task Restablecer_CodigoValido_CambiaLaContrasena()
    {
        // Arreglar
        _usuarios.ObtenerPorCorreo("ana@glamspaces.com").Returns(Ana(true));
        _codigos.Validar(7, PropositoCodigo.Recuperacion, "654321").Returns(ResultadoCodigo.Valido);
        var cambiada = RespuestaApi<object?>.Ok(null, "Tu contraseña se cambió correctamente.");
        _usuarios.CambiarContrasena(7, "NuevaClave1").Returns(cambiada);

        // Actuar
        var r = await Servicio.Restablecer(new RestablecerContrasenaRequest { Correo = "ana@glamspaces.com", Codigo = "654321", NuevaContrasena = "NuevaClave1" });

        // Afirmar
        Assert.Same(cambiada, r);
    }

    [Fact]
    public async Task Restablecer_CodigoVencido_NoCambiaNada()
    {
        // Arreglar
        _usuarios.ObtenerPorCorreo(Arg.Any<string>()).Returns(Ana(true));
        _codigos.Validar(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<string>()).Returns(ResultadoCodigo.Vencido);

        // Actuar
        var r = await Servicio.Restablecer(new RestablecerContrasenaRequest { Correo = "ana@glamspaces.com", Codigo = "654321", NuevaContrasena = "NuevaClave1" });

        // Afirmar
        Assert.Equal(CodigosError.CodigoVencido, r.Codigo);
        await _usuarios.DidNotReceiveWithAnyArgs().CambiarContrasena(default, default!);
    }

    // ---------- Plantillas y propósitos ----------

    [Fact]
    public void PlantillaCorreo_VerificacionYRecuperacion_LlevanElCodigoYElTextoCorrecto()
    {
        // Actuar
        var verificar = PlantillaCorreo.Para(PropositoCodigo.Verificacion, "Ana <b>Pérez</b>", "042517");
        var recuperar = PlantillaCorreo.Para(PropositoCodigo.Recuperacion, "  ", "998877");

        // Afirmar
        Assert.Equal("042517 es tu código para verificar tu correo", verificar.Asunto);
        Assert.Contains("042517", verificar.Html);
        Assert.Contains("Hola, Ana", verificar.Texto);
        Assert.DoesNotContain("<b>", verificar.Html); // el nombre se codifica para el HTML
        Assert.Contains("activarla", verificar.Texto);
        Assert.Equal("998877 es tu código para cambiar tu contraseña", recuperar.Asunto);
        Assert.StartsWith("Hola:", recuperar.Texto);
        Assert.Contains("tu contraseña sigue igual", recuperar.Html);
        Assert.Contains("15 minutos", recuperar.Texto);
    }

    [Fact]
    public void PlantillaCorreo_SinNombre_SaludaSinNombre()
    {
        // Actuar
        var m = PlantillaCorreo.Para(PropositoCodigo.Verificacion, null!, "000001");

        // Afirmar
        Assert.StartsWith("Hola:", m.Texto);
    }

    [Theory]
    [InlineData("verificacion", true)]
    [InlineData("recuperacion", true)]
    [InlineData("otro", false)]
    [InlineData(null, false)]
    public void PropositoCodigo_EsValido(string? proposito, bool esperado)
        => Assert.Equal(esperado, PropositoCodigo.EsValido(proposito));
}
