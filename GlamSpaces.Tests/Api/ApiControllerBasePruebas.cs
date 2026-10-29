namespace GlamSpaces.Tests.Api;

// Traducción de RespuestaApi.Codigo al status HTTP.
public class ApiControllerBasePruebas
{
    // Controller mínimo para llamar al método protegido Responder.
    private sealed class ControladorDePrueba : ApiControllerBase
    {
        public ObjectResult Probar(int codigo) => Responder(RespuestaApi<string>.Error(codigo, "mensaje"));
    }

    [Theory]
    [InlineData(CodigosError.Exito, StatusCodes.Status200OK)]
    [InlineData(CodigosError.DatosInvalidos, StatusCodes.Status400BadRequest)]
    [InlineData(CodigosError.CorreoDuplicado, StatusCodes.Status409Conflict)]
    [InlineData(CodigosError.CredencialesIncorrectas, StatusCodes.Status401Unauthorized)]
    [InlineData(CodigosError.NoEsAdministrador, StatusCodes.Status400BadRequest)]
    [InlineData(CodigosError.UsuarioNoEncontrado, StatusCodes.Status404NotFound)]
    [InlineData(CodigosError.SalonNoEncontrado, StatusCodes.Status404NotFound)]
    [InlineData(CodigosError.NoEsDueno, StatusCodes.Status403Forbidden)]
    [InlineData(CodigosError.SalonSinPaquetes, StatusCodes.Status400BadRequest)]
    [InlineData(CodigosError.EstadoInvalido, StatusCodes.Status400BadRequest)]
    [InlineData(CodigosError.PaqueteNoEncontrado, StatusCodes.Status404NotFound)]
    [InlineData(CodigosError.UltimoPaquete, StatusCodes.Status400BadRequest)]
    [InlineData(CodigosError.SolicitudNoEncontrada, StatusCodes.Status404NotFound)]
    [InlineData(CodigosError.FechaNoDisponible, StatusCodes.Status409Conflict)]
    [InlineData(CodigosError.NoEsCliente, StatusCodes.Status400BadRequest)]
    [InlineData(CodigosError.SolicitudDuplicada, StatusCodes.Status409Conflict)]
    [InlineData(CodigosError.SolicitudAjena, StatusCodes.Status403Forbidden)]
    [InlineData(CodigosError.ErrorInterno, StatusCodes.Status500InternalServerError)]
    public void Responder_TraduceCadaCodigoAlStatusHttpCorrecto(int codigo, int statusEsperado)
    {
        // Arreglar
        var controlador = new ControladorDePrueba();

        // Actuar
        var resultado = controlador.Probar(codigo);

        // Afirmar
        Assert.Equal(statusEsperado, resultado.StatusCode);
        Assert.Equal(codigo, Assert.IsType<RespuestaApi<string>>(resultado.Value).Codigo);
    }
}
