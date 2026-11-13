using GlamSpaces.Tests.Domain;

namespace GlamSpaces.Tests.Api;

// HU-07 (fotos): subirFoto y eliminarFoto de punta a punta por HTTP (multipart real, Program.cs real),
// con el repositorio y el almacenamiento como dobles: ni base de datos ni Azure.
public sealed class FotosEndpointsPruebas : IDisposable
{
    private const string UrlBlob = "https://cuenta.blob.core.windows.net/fotos-salones/12/a.jpg";

    private readonly FabricaApi _fabrica = new();

    public void Dispose() => _fabrica.Dispose();

    private static MultipartFormDataContent Formulario(byte[]? archivo, string nombre = "foto.jpg", int salonId = 12, int adminId = 3)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(salonId.ToString()), "salonId" },
            { new StringContent(adminId.ToString()), "adminId" },
        };
        if (archivo is not null) form.Add(new ByteArrayContent(archivo), "archivo", nombre);
        return form;
    }

    private static async Task<(HttpStatusCode Status, RespuestaApi<FotoSalonResponse> Cuerpo)> Leer(HttpResponseMessage respuesta) =>
        (respuesta.StatusCode, (await respuesta.Content.ReadFromJsonAsync<RespuestaApi<FotoSalonResponse>>())!);

    [Fact]
    public async Task SubirFoto_JpgValido_Regresa200ConFotoIdYUrl()
    {
        // Arreglar
        _fabrica.Almacenamiento.Subir(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>()).Returns(UrlBlob);
        _fabrica.Fotos.Agregar(12, 3, UrlBlob).Returns(RespuestaApi<FotoSalonResponse>.Ok(new FotoSalonResponse { FotoId = 40, Url = UrlBlob }, "Foto subida correctamente."));

        // Actuar
        var (status, cuerpo) = await Leer(await _fabrica.CreateClient().PostAsync("/api/salones/subirFoto", Formulario(FormatoImagenPruebas.Jpg)));

        // Afirmar
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.True(cuerpo.Exito);
        Assert.Equal(40, cuerpo.Datos!.FotoId);
        Assert.Equal(UrlBlob, cuerpo.Datos.Url);
        await _fabrica.Almacenamiento.Received(1).Subir(Arg.Is<string>(r => r.StartsWith("12/") && r.EndsWith(".jpg")), Arg.Any<Stream>(), "image/jpeg");
    }

    [Fact]
    public async Task SubirFoto_SinSalonIdNiAdminId_LlegaComoCeroYRegresaElFormatoEstandar()
    {
        // Arreglar: el formulario solo trae el archivo
        _fabrica.Almacenamiento.Subir(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>()).Returns(UrlBlob);
        _fabrica.Fotos.Agregar(0, 0, UrlBlob).Returns(RespuestaApi<FotoSalonResponse>.Error(CodigosError.SalonNoEncontrado, "El salón no existe."));
        var form = new MultipartFormDataContent { { new ByteArrayContent(FormatoImagenPruebas.Jpg), "archivo", "foto.jpg" } };

        // Actuar
        var (status, cuerpo) = await Leer(await _fabrica.CreateClient().PostAsync("/api/salones/subirFoto", form));

        // Afirmar: sin 400 de validación del framework; el SP decide, igual que antes
        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Equal(CodigosError.SalonNoEncontrado, cuerpo.Codigo);
        Assert.False(cuerpo.Exito);
        await _fabrica.Fotos.Received(1).Agregar(0, 0, UrlBlob);
    }

    [Fact]
    public async Task SubirFoto_SinArchivo_Regresa400Con2005()
    {
        // Actuar
        var (status, cuerpo) = await Leer(await _fabrica.CreateClient().PostAsync("/api/salones/subirFoto", Formulario(null)));

        // Afirmar
        Assert.Equal(HttpStatusCode.BadRequest, status);
        Assert.Equal(CodigosError.ArchivoRequerido, cuerpo.Codigo);
    }

    [Fact]
    public async Task SubirFoto_ArchivoFalsoConExtensionJpg_Regresa415Con2006()
    {
        // Actuar
        var (status, cuerpo) = await Leer(await _fabrica.CreateClient().PostAsync("/api/salones/subirFoto",
            Formulario("no soy una imagen"u8.ToArray(), "falsa.jpg")));

        // Afirmar
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, status);
        Assert.Equal(CodigosError.FormatoFotoInvalido, cuerpo.Codigo);
        await _fabrica.Almacenamiento.DidNotReceiveWithAnyArgs().Subir(default!, default!, default!);
    }

    [Fact]
    public async Task SubirFoto_MasDe5Mb_Regresa413Con2007()
    {
        // Arreglar
        var grande = new byte[6 * 1024 * 1024];
        FormatoImagenPruebas.Jpg.CopyTo(grande, 0);

        // Actuar
        var (status, cuerpo) = await Leer(await _fabrica.CreateClient().PostAsync("/api/salones/subirFoto", Formulario(grande)));

        // Afirmar
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, status);
        Assert.Equal(CodigosError.FotoMuyPesada, cuerpo.Codigo);
    }

    [Fact]
    public async Task SubirFoto_NovenaFoto_Regresa409Con2008YBorraElArchivo()
    {
        // Arreglar
        _fabrica.Almacenamiento.Subir(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>()).Returns(UrlBlob);
        _fabrica.Fotos.Agregar(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>())
            .Returns(RespuestaApi<FotoSalonResponse>.Error(CodigosError.LimiteDeFotos, "El salón ya tiene 8 fotos. Elimina una para subir otra."));

        // Actuar
        var (status, cuerpo) = await Leer(await _fabrica.CreateClient().PostAsync("/api/salones/subirFoto", Formulario(FormatoImagenPruebas.Png, "foto.png")));

        // Afirmar
        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal(CodigosError.LimiteDeFotos, cuerpo.Codigo);
        await _fabrica.Almacenamiento.Received(1).Eliminar(UrlBlob);
    }

    [Fact]
    public async Task EliminarFoto_Regresa200YBorraArchivoYRegistro()
    {
        // Arreglar
        var foto = RespuestaApi<FotoSalonResponse>.Ok(new FotoSalonResponse { FotoId = 40, Url = UrlBlob }, "Foto eliminada correctamente.");
        _fabrica.Fotos.Validar(Arg.Any<EliminarFotoRequest>()).Returns(foto);
        _fabrica.Fotos.Eliminar(Arg.Any<EliminarFotoRequest>()).Returns(foto);

        // Actuar
        var (status, cuerpo) = await Leer(await _fabrica.CreateClient().PostAsJsonAsync("/api/salones/eliminarFoto",
            new { salonId = 12, fotoId = 40, adminId = 3 }));

        // Afirmar
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(40, cuerpo.Datos!.FotoId);
        await _fabrica.Almacenamiento.Received(1).Eliminar(UrlBlob);
        await _fabrica.Fotos.Received(1).Eliminar(Arg.Is<EliminarFotoRequest>(r => r.SalonId == 12 && r.FotoId == 40 && r.AdminId == 3));
    }

    [Fact]
    public async Task EliminarFoto_FotoDeOtroSalon_Regresa404Con2009()
    {
        // Arreglar
        _fabrica.Fotos.Validar(Arg.Any<EliminarFotoRequest>())
            .Returns(RespuestaApi<FotoSalonResponse>.Error(CodigosError.FotoNoEncontrada, "La foto no existe o no es de este salón."));

        // Actuar
        var (status, cuerpo) = await Leer(await _fabrica.CreateClient().PostAsJsonAsync("/api/salones/eliminarFoto",
            new { salonId = 12, fotoId = 99, adminId = 3 }));

        // Afirmar
        Assert.Equal(HttpStatusCode.NotFound, status);
        Assert.Equal(CodigosError.FotoNoEncontrada, cuerpo.Codigo);
    }

    [Fact]
    public async Task ObtenerSalon_JsonTraeFotosComoAntesYFotosDetalleConFotoIdYUrl()
    {
        // Arreglar
        var salon = new Salon { Id = 12, Nombre = "Jardín", Fotos = [new FotoSalon { Id = 40, SalonId = 12, Url = UrlBlob }] };
        _fabrica.Salones.Obtener(12).Returns(RespuestaApi<SalonResponse>.Ok(SalonResponse.Desde(salon), "Consulta exitosa."));

        // Actuar
        var json = await (await _fabrica.CreateClient().PostAsJsonAsync("/api/salones/obtener", new { id = 12 })).Content.ReadAsStringAsync();

        // Afirmar: el contrato que ya usa el front no cambia y se agrega fotosDetalle
        Assert.Contains($"\"fotos\":[\"{UrlBlob}\"]", json);
        Assert.Contains($"\"fotosDetalle\":[{{\"fotoId\":40,\"url\":\"{UrlBlob}\"}}]", json);
    }

    [Theory]
    [InlineData("Almacenamiento:CadenaConexion", "Almacenamiento__CadenaConexion")]
    [InlineData("Almacenamiento:Contenedor", "Almacenamiento__Contenedor")]
    public void SinVariablesDeAlmacenamiento_LaApiNoArrancaYDiceCualFalta(string clave, string variable)
    {
        // Arreglar
        using var fabrica = new FabricaApi().WithWebHostBuilder(b => b.UseSetting(clave, ""));

        // Actuar
        var error = Assert.ThrowsAny<Exception>(() => fabrica.CreateClient());

        // Afirmar: el mensaje dice qué variable falta y nunca muestra el valor de la cadena
        Assert.Contains(variable, error.ToString());
        Assert.DoesNotContain("UseDevelopmentStorage", error.ToString());
    }
}
