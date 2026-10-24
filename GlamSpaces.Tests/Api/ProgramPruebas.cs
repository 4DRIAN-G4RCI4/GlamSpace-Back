namespace GlamSpaces.Tests.Api;

// Configuración real de Program.cs (CORS, errores, validación de JSON, healthcheck) probada
// de punta a punta en memoria. Los repositorios son dobles: no hay base de datos.
public class ProgramPruebas : IClassFixture<FabricaApi>
{
    private readonly FabricaApi _fabrica;

    public ProgramPruebas(FabricaApi fabrica)
    {
        _fabrica = fabrica;
        _fabrica.Salones.ClearReceivedCalls();
    }

    private static StringContent Json(string texto) => new(texto, Encoding.UTF8, "application/json");

    [Fact]
    public async Task Peticion_Exitosa_RegresaElFormatoEstandarConCodigoCero()
    {
        // Arreglar
        _fabrica.Salones.Obtener(1).Returns(RespuestaApi<SalonResponse>.Ok(new SalonResponse { Id = 1, Nombre = "Jardín" }, "Consulta exitosa."));
        var cliente = _fabrica.CreateClient();

        // Actuar
        var respuesta = await cliente.PostAsync("/api/salones/obtener", Json("{\"id\":1}"));
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaApi<SalonResponse>>();

        // Afirmar
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(0, cuerpo!.Codigo);
        Assert.Equal("Jardín", cuerpo.Datos!.Nombre);
    }

    [Fact]
    public async Task JsonConTipoIncorrecto_Regresa400ConCodigo1001()
    {
        // Arreglar
        var cliente = _fabrica.CreateClient();

        // Actuar: "capacidad" debe ser número.
        var respuesta = await cliente.PostAsync("/api/salones/crear", Json("{\"adminId\":1,\"capacidad\":\"abc\"}"));
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaApi<object>>();

        // Afirmar
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal(CodigosError.DatosInvalidos, cuerpo!.Codigo);
        Assert.StartsWith("El JSON enviado no es válido", cuerpo.Mensaje);
        await _fabrica.Salones.DidNotReceive().Crear(Arg.Any<SalonRequest>());
    }

    [Fact]
    public async Task ExcepcionNoControlada_Regresa500ConCodigo5000()
    {
        // Arreglar: el repositorio falla como si la BD no respondiera.
        _fabrica.Salones.Listar(Arg.Any<ListarSalonesRequest>()).ThrowsAsync(new InvalidOperationException("BD caída"));
        var cliente = _fabrica.CreateClient();

        // Actuar
        var respuesta = await cliente.PostAsync("/api/salones/listar", Json("{}"));
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaApi<object>>();

        // Afirmar: el cliente no ve el detalle de la excepción.
        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);
        Assert.Equal(CodigosError.ErrorInterno, cuerpo!.Codigo);
        Assert.DoesNotContain("BD caída", cuerpo.Mensaje);
    }

    [Fact]
    public async Task Cors_OrigenDelFrontend_RecibePermiso()
    {
        // Arreglar
        var cliente = _fabrica.CreateClient();
        var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/salones/buscar");
        preflight.Headers.Add("Origin", FabricaApi.OrigenFrontend);
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type");

        // Actuar
        var respuesta = await cliente.SendAsync(preflight);

        // Afirmar
        Assert.True(respuesta.Headers.TryGetValues("Access-Control-Allow-Origin", out var origenes));
        Assert.Equal(FabricaApi.OrigenFrontend, Assert.Single(origenes));
    }

    [Fact]
    public async Task Cors_OrigenDesconocido_NoRecibePermiso()
    {
        // Arreglar
        var cliente = _fabrica.CreateClient();
        var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/salones/buscar");
        preflight.Headers.Add("Origin", "https://sitio-malicioso.example");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");

        // Actuar
        var respuesta = await cliente.SendAsync(preflight);

        // Afirmar: sin este encabezado, el navegador bloquea la llamada (corrección de S5122).
        Assert.False(respuesta.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Health_SinBaseDeDatos_RespondeUnhealthy()
    {
        // Arreglar: la fábrica deja la cadena de conexión vacía.
        var cliente = _fabrica.CreateClient();

        // Actuar
        var respuesta = await cliente.GetAsync("/health");

        // Afirmar
        Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
        Assert.Equal("Unhealthy", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Swagger_PublicaLaDocumentacionDeLosEndpoints()
    {
        // Arreglar
        var cliente = _fabrica.CreateClient();

        // Actuar
        var documento = await cliente.GetStringAsync("/swagger/v1/swagger.json");

        // Afirmar
        Assert.Contains("/api/salones/buscar", documento);
        Assert.Contains("/api/salones/disponibilidad", documento);
    }
}
