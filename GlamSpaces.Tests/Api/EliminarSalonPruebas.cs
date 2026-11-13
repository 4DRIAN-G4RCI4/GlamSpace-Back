using Microsoft.Extensions.Logging;

namespace GlamSpaces.Tests.Api;

// Mis salones: POST /api/salones/eliminar. El SP decide (2001, 2002, 2010, 5000); después del COMMIT
// el controller borra la carpeta de fotos como mejor esfuerzo y, si falla, solo lo registra.
public class EliminarSalonPruebas
{
    private readonly ISalonRepositorio _repo = Substitute.For<ISalonRepositorio>();
    private readonly AlmacenamientoFalso _almacen = new();
    private readonly LoggerFalso _logger = new();

    private SalonesController Controller() =>
        new(_repo, new FotosSalonServicio(Substitute.For<IFotoSalonRepositorio>(), _almacen), _logger);

    private static readonly EliminarSalonRequest Request = new() { Id = 12, AdminId = 3 };

    private static (int Status, RespuestaApi<int> Cuerpo) Leer(ActionResult<RespuestaApi<int>> accion)
    {
        var resultado = Assert.IsType<ObjectResult>(accion.Result);
        return (resultado.StatusCode!.Value, Assert.IsType<RespuestaApi<int>>(resultado.Value));
    }

    [Fact]
    public async Task Eliminar_ConExito_Regresa200YBorraLaCarpetaDeFotosDelSalon()
    {
        // Arreglar
        await _almacen.Subir("12/a.jpg", new MemoryStream([1]), "image/jpeg");
        await _almacen.Subir("120/b.jpg", new MemoryStream([1]), "image/jpeg");
        _repo.Eliminar(Request).Returns(RespuestaApi<int>.Ok(12, "Salón eliminado correctamente."));

        // Actuar
        var (status, cuerpo) = Leer(await Controller().Eliminar(Request));

        // Afirmar
        Assert.Equal(200, status);
        Assert.True(cuerpo.Exito);
        Assert.Equal(12, cuerpo.Datos);
        Assert.Equal("Salón eliminado correctamente.", cuerpo.Mensaje);
        Assert.Equal([12], _almacen.CarpetasEliminadas);
        Assert.StartsWith("120/", Assert.Single(_almacen.Archivos).Key); // otro salón no se toca
        Assert.Empty(_logger.Mensajes);
    }

    [Theory]
    [InlineData(CodigosError.SalonNoEncontrado, 404)]
    [InlineData(CodigosError.NoEsDueno, 403)]
    [InlineData(CodigosError.SalonConSolicitudesActivas, 409)]
    [InlineData(CodigosError.ErrorInterno, 500)]
    public async Task Eliminar_ConErrorDelSp_RegresaSuStatusYNoTocaLasFotos(int codigo, int statusEsperado)
    {
        // Arreglar
        _repo.Eliminar(Request).Returns(RespuestaApi<int>.Error(codigo, "Error del SP."));

        // Actuar
        var (status, cuerpo) = Leer(await Controller().Eliminar(Request));

        // Afirmar
        Assert.Equal(statusEsperado, status);
        Assert.Equal(codigo, cuerpo.Codigo);
        Assert.False(cuerpo.Exito);
        Assert.Empty(_almacen.CarpetasEliminadas);
    }

    [Fact]
    public async Task Eliminar_SiFallaElAlmacenamiento_ElSalonQuedaEliminadoYSoloSeRegistraSinSecretos()
    {
        // Arreglar
        _almacen.FallarAlEliminar = true;
        _repo.Eliminar(Request).Returns(RespuestaApi<int>.Ok(12, "Salón eliminado correctamente."));

        // Actuar
        var (status, cuerpo) = Leer(await Controller().Eliminar(Request));

        // Afirmar
        Assert.Equal(200, status);
        Assert.True(cuerpo.Exito);
        var (nivel, mensaje) = Assert.Single(_logger.Mensajes);
        Assert.Equal(LogLevel.Warning, nivel);
        Assert.Contains("12", mensaje);
        Assert.DoesNotContain("AccountKey", mensaje);
        Assert.DoesNotContain("blob.core.windows.net", mensaje);
    }

    [Fact]
    public async Task Eliminar_PorHttp_LeeElCuerpoEnCamelCaseYRegresaElFormatoEstandar()
    {
        // Arreglar
        using var fabrica = new FabricaApi();
        fabrica.Salones.Eliminar(Arg.Is<EliminarSalonRequest>(r => r.Id == 12 && r.AdminId == 3))
            .Returns(RespuestaApi<int>.Error(CodigosError.SalonConSolicitudesActivas,
                "Tiene solicitudes pendientes o confirmadas; resuélvelas antes de eliminar el salón."));

        // Actuar
        var respuesta = await fabrica.CreateClient().PostAsJsonAsync("/api/salones/eliminar", new { id = 12, adminId = 3 });
        var json = await respuesta.Content.ReadAsStringAsync();

        // Afirmar
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Contains("\"codigo\":2010", json);
        Assert.Contains("\"exito\":false", json);
        Assert.Contains("resuélvelas antes de eliminar el salón", json);
        await fabrica.Almacenamiento.DidNotReceive().EliminarCarpetaDeSalon(Arg.Any<int>());
    }

    // Guarda lo que se registró (nivel y mensaje ya formateado).
    private sealed class LoggerFalso : ILogger<SalonesController>
    {
        public List<(LogLevel Nivel, string Mensaje)> Mensajes { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Mensajes.Add((logLevel, formatter(state, exception)));
    }
}
