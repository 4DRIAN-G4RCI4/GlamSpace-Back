namespace GlamSpaces.Tests.Infrastructure;

// Solo la validación que ocurre ANTES de llamar a la BD. El resto del repositorio
// ejecuta stored procedures y se cubre con pruebas de integración aparte.
public class UsuarioRepositorioPruebas
{
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
