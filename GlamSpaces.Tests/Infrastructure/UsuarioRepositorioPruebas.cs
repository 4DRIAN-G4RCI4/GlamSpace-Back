namespace GlamSpaces.Tests.Infrastructure;

// Registro (valida y hashea la contraseña antes del SP), login (mensaje genérico) y obtener.
// Usa ConexionFalsa (Dapper real, sin BD) salvo donde se comprueba que ni se toca la BD.
public class UsuarioRepositorioPruebas
{
    private static DataTable UsuarioFila(string hash) => ConexionFalsa.Tabla(
        ("Id", 5), ("NombreCompleto", "Ana Pérez"), ("Correo", "ana@glamspaces.com"), ("PasswordHash", hash),
        ("TipoCuenta", "cliente"), ("NombreSalon", null), ("FechaRegistro", new DateTime(2026, 11, 1)));

    [Fact]
    public async Task Registrar_MandaElHashNuncaLaContrasena()
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(0, "Usuario registrado correctamente.", UsuarioFila("hash-guardado"));
        var request = new RegistroRequest { NombreCompleto = "Ana Pérez", Correo = "ana@glamspaces.com", Password = "Segura123", TipoCuenta = "cliente" };

        // Actuar
        var r = await new UsuarioRepositorio(conexion).Registrar(request);

        // Afirmar
        Assert.Equal("sp_Usuario_Registrar", conexion.ComandoEjecutado);
        Assert.False(conexion.Parametros.ContainsKey("Password"));
        var hash = Assert.IsType<string>(conexion.Parametros["PasswordHash"]);
        Assert.NotEqual("Segura123", hash);
        Assert.True(PasswordHasher.Verificar("Segura123", hash));
        Assert.True(r.Exito);
        Assert.Equal(5, r.Datos!.Id);
        Assert.Equal("cliente", r.Datos.TipoCuenta);
    }

    [Fact]
    public async Task Registrar_CorreoDuplicado_RegresaElCodigoDelSp()
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(CodigosError.CorreoDuplicado, "El correo ya está registrado.");

        // Actuar
        var r = await new UsuarioRepositorio(conexion).Registrar(new RegistroRequest { Correo = "ana@glamspaces.com", Password = "Segura123", TipoCuenta = "cliente" });

        // Afirmar
        Assert.Equal(CodigosError.CorreoDuplicado, r.Codigo);
        Assert.Null(r.Datos);
    }

    [Fact]
    public async Task Login_ConContrasenaCorrecta_RegresaElUsuario()
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(0, "Consulta exitosa.", UsuarioFila(PasswordHasher.Hashear("Segura123")));

        // Actuar
        var r = await new UsuarioRepositorio(conexion).Login(new LoginRequest { Correo = "ana@glamspaces.com", Password = "Segura123" });

        // Afirmar
        Assert.Equal("sp_Usuario_ObtenerPorCorreo", conexion.ComandoEjecutado);
        Assert.Equal("ana@glamspaces.com", conexion.Parametros["Correo"]);
        Assert.True(r.Exito);
        Assert.Equal("Inicio de sesión correcto.", r.Mensaje);
        Assert.Equal("Ana Pérez", r.Datos!.NombreCompleto);
    }

    [Fact]
    public async Task Login_ConContrasenaIncorrecta_RegresaElMensajeGenerico()
    {
        // Arreglar
        var conexion = new ConexionFalsa().Responder(0, "Consulta exitosa.", UsuarioFila(PasswordHasher.Hashear("Segura123")));

        // Actuar
        var r = await new UsuarioRepositorio(conexion).Login(new LoginRequest { Correo = "ana@glamspaces.com", Password = "Otra1234" });

        // Afirmar
        Assert.Equal(CodigosError.CredencialesIncorrectas, r.Codigo);
        Assert.Equal("Correo o contraseña incorrectos.", r.Mensaje);
        Assert.Null(r.Datos);
    }

    [Fact]
    public async Task Login_CorreoInexistente_RegresaElMismoMensajeGenerico()
    {
        // Arreglar: el SP no regresa filas
        var conexion = new ConexionFalsa().Responder(0, "Consulta exitosa.");

        // Actuar
        var r = await new UsuarioRepositorio(conexion).Login(new LoginRequest { Correo = "nadie@glamspaces.com", Password = "Segura123" });

        // Afirmar
        Assert.Equal(CodigosError.CredencialesIncorrectas, r.Codigo);
        Assert.Equal("Correo o contraseña incorrectos.", r.Mensaje);
    }

    [Fact]
    public async Task Obtener_RegresaElUsuarioOElCodigoDelSp()
    {
        // Arreglar
        var existe = new ConexionFalsa().Responder(0, "Consulta exitosa.", UsuarioFila("x"));
        var noExiste = new ConexionFalsa().Responder(CodigosError.UsuarioNoEncontrado, "El usuario no existe.");

        // Actuar
        var r1 = await new UsuarioRepositorio(existe).Obtener(5);
        var r2 = await new UsuarioRepositorio(noExiste).Obtener(999);

        // Afirmar
        Assert.Equal("sp_Usuario_Obtener", existe.ComandoEjecutado);
        Assert.Equal(5, existe.Parametros["Id"]);
        Assert.Equal(5, r1.Datos!.Id);
        Assert.Equal(CodigosError.UsuarioNoEncontrado, r2.Codigo);
        Assert.Null(r2.Datos);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234567")] // 7 caracteres: uno menos del mínimo
    public async Task Registrar_ConContrasenaCorta_RegresaDatosInvalidosSinTocarLaBaseDeDatos(string password)
    {
        // Arreglar
        var conexion = Substitute.For<IDbConnection>();
        var repositorio = new UsuarioRepositorio(conexion);
        var request = new RegistroRequest
        {
            NombreCompleto = "Ana",
            Correo = "ana@glamspaces.com",
            Password = password,
            TipoCuenta = "cliente",
        };

        // Actuar
        var respuesta = await repositorio.Registrar(request);

        // Afirmar
        Assert.Equal(CodigosError.DatosInvalidos, respuesta.Codigo);
        Assert.Equal("La contraseña debe tener al menos 8 caracteres.", respuesta.Mensaje);
        Assert.Empty(conexion.ReceivedCalls());
    }
}
