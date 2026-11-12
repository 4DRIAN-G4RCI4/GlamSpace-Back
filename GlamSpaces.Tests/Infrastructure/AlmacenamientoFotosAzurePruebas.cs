using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace GlamSpaces.Tests.Infrastructure;

// HU-07 (fotos): la implementación de Azure Blob Storage con los clientes del SDK como dobles
// (el SDK de Azure está hecho para eso). Ninguna prueba llama a Azure.
public class AlmacenamientoFotosAzurePruebas
{
    private static readonly Uri UriContenedor = new("https://cuenta.blob.core.windows.net/fotos-salones");

    private readonly BlobContainerClient _contenedor = Substitute.For<BlobContainerClient>();

    public AlmacenamientoFotosAzurePruebas()
    {
        _contenedor.Uri.Returns(UriContenedor);
    }

    private AlmacenamientoFotosAzure Almacen => new(_contenedor);

    [Fact]
    public async Task Subir_GuardaConContentTypeYCacheControlYRegresaLaUrlDelBlob()
    {
        // Arreglar
        var blob = Substitute.For<BlobClient>();
        blob.Uri.Returns(new Uri(UriContenedor + "/12/a.jpg"));
        _contenedor.GetBlobClient("12/a.jpg").Returns(blob);
        using var contenido = new MemoryStream([1, 2, 3]);

        // Actuar
        var url = await Almacen.Subir("12/a.jpg", contenido, "image/jpeg");

        // Afirmar
        Assert.Equal("https://cuenta.blob.core.windows.net/fotos-salones/12/a.jpg", url);
        await blob.Received(1).UploadAsync(contenido, Arg.Is<BlobUploadOptions>(o =>
            o.HttpHeaders.ContentType == "image/jpeg" && o.HttpHeaders.CacheControl == "public, max-age=31536000, immutable"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Subir_SiAzureFalla_LanzaErrorAlmacenamiento()
    {
        // Arreglar
        var blob = Substitute.For<BlobClient>();
        blob.UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RequestFailedException(403, "AuthorizationFailure"));
        _contenedor.GetBlobClient(Arg.Any<string>()).Returns(blob);

        // Actuar / Afirmar: el detalle de Azure queda adentro, el mensaje es genérico
        var error = await Assert.ThrowsAsync<ErrorAlmacenamiento>(() => Almacen.Subir("1/a.jpg", new MemoryStream([1]), "image/png"));
        Assert.IsType<RequestFailedException>(error.InnerException);
        Assert.DoesNotContain("Authorization", error.Message);
    }

    [Fact]
    public async Task Eliminar_UrlDeNuestroContenedor_BorraElBlobSiExiste()
    {
        // Actuar
        await Almacen.Eliminar("https://cuenta.blob.core.windows.net/fotos-salones/12/a.jpg");

        // Afirmar: DeleteIfExists → si ya no existe, no es error
        await _contenedor.Received(1).DeleteBlobIfExistsAsync("12/a.jpg", Arg.Any<DeleteSnapshotsOption>(),
            Arg.Any<BlobRequestConditions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Eliminar_UrlExterna_NoHaceNada()
    {
        // Actuar: fotos viejas que se guardaron con URLs de otros sitios
        await Almacen.Eliminar("https://upload.wikimedia.org/x/salon.jpg");

        // Afirmar
        await _contenedor.DidNotReceiveWithAnyArgs().DeleteBlobIfExistsAsync(default!, default, default, default);
    }

    [Fact]
    public async Task EliminarCarpetaDeSalon_BorraCadaBlobConElPrefijoDelSalon()
    {
        // Arreglar
        var blobs = new[] { BlobsModelFactory.BlobItem(name: "4/a.jpg"), BlobsModelFactory.BlobItem(name: "4/b.png") };
        _contenedor.GetBlobsAsync(BlobTraits.None, BlobStates.None, "4/", Arg.Any<CancellationToken>())
            .Returns(AsyncPageable<BlobItem>.FromPages([Page<BlobItem>.FromValues(blobs, null, Substitute.For<Response>())]));

        // Actuar
        await Almacen.EliminarCarpetaDeSalon(4);

        // Afirmar: el prefijo lleva "/" para no tocar la carpeta 40/
        await _contenedor.Received(1).DeleteBlobIfExistsAsync("4/a.jpg", Arg.Any<DeleteSnapshotsOption>(), Arg.Any<BlobRequestConditions>(), Arg.Any<CancellationToken>());
        await _contenedor.Received(1).DeleteBlobIfExistsAsync("4/b.png", Arg.Any<DeleteSnapshotsOption>(), Arg.Any<BlobRequestConditions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EliminarCarpetaDeSalon_SiAzureFalla_LanzaErrorAlmacenamiento()
    {
        // Arreglar
        _contenedor.GetBlobsAsync(Arg.Any<BlobTraits>(), Arg.Any<BlobStates>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Throws(new RequestFailedException(500, "Interno"));

        // Actuar / Afirmar
        await Assert.ThrowsAsync<ErrorAlmacenamiento>(() => Almacen.EliminarCarpetaDeSalon(4));
    }

    [Theory]
    [InlineData("https://cuenta.blob.core.windows.net/fotos-salones/12/a.jpg", "12/a.jpg")]
    [InlineData("https://CUENTA.blob.core.windows.net/fotos-salones/12/a.jpg?sv=1", "12/a.jpg")]
    [InlineData("https://cuenta.blob.core.windows.net/fotos-salones/12/con%20espacio.png", "12/con espacio.png")]
    [InlineData("https://cuenta.blob.core.windows.net/otro-contenedor/12/a.jpg", null)]
    [InlineData("https://cuenta.blob.core.windows.net/fotos-salones/", null)]
    [InlineData("", null)]
    public void RutaEnElContenedor_SoloReconoceUrlsDeNuestroContenedor(string url, string? esperada)
    {
        Assert.Equal(esperada, AlmacenamientoFotosAzure.RutaEnElContenedor(UriContenedor, url));
    }

    [Fact]
    public void ConstructorPublico_UsaLaCadenaYElContenedorDeLasOpciones()
    {
        // Arreglar: cadena del emulador (no se conecta al construir)
        var opciones = new OpcionesAlmacenamiento { CadenaConexion = "UseDevelopmentStorage=true", Contenedor = "fotos-salones" };

        // Actuar
        var almacen = new AlmacenamientoFotosAzure(opciones);

        // Afirmar
        Assert.NotNull(almacen);
    }
}
