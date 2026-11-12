using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace GlamSpaces.Infrastructure.Almacenamiento;

// HU-07 (fotos): fotos de los salones en Azure Blob Storage. El contenedor es público de solo
// lectura por blob: cualquiera puede ver una foto con su URL, pero nadie puede listar el contenedor.
// Cualquier error de Azure se envuelve en ErrorAlmacenamiento (la API responde 5000 sin detalles).
public sealed class AlmacenamientoFotosAzure : IAlmacenamientoFotos
{
    // Los nombres llevan un GUID nuevo en cada subida: el archivo nunca cambia y se puede cachear un año.
    internal const string CacheControl = "public, max-age=31536000, immutable";

    private readonly BlobContainerClient _contenedor;

    public AlmacenamientoFotosAzure(OpcionesAlmacenamiento opciones)
        : this(new BlobContainerClient(opciones.CadenaConexion, opciones.Contenedor)) { }

    internal AlmacenamientoFotosAzure(BlobContainerClient contenedor)
    {
        _contenedor = contenedor;
    }

    public async Task<string> Subir(string ruta, Stream contenido, string contentType)
    {
        var blob = _contenedor.GetBlobClient(ruta);
        await Ejecutar(() => blob.UploadAsync(contenido, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType, CacheControl = CacheControl },
        }));
        return blob.Uri.ToString();
    }

    public async Task Eliminar(string url)
    {
        var ruta = RutaEnElContenedor(_contenedor.Uri, url);
        if (ruta is null) return; // URL externa (fotos viejas): no es nuestra, no hay nada que borrar
        await Ejecutar(() => _contenedor.DeleteBlobIfExistsAsync(ruta)); // si ya no existe, no es error
    }

    public Task EliminarCarpetaDeSalon(int salonId) => Ejecutar(async () =>
    {
        await foreach (var blob in _contenedor.GetBlobsAsync(BlobTraits.None, BlobStates.None, $"{salonId}/", CancellationToken.None))
        {
            await _contenedor.DeleteBlobIfExistsAsync(blob.Name);
        }
    });

    // "https://cuenta.blob.core.windows.net/fotos-salones/12/a.jpg" → "12/a.jpg"; otra URL → null.
    internal static string? RutaEnElContenedor(Uri contenedor, string url)
    {
        var baseUrl = contenedor.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/";
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith(baseUrl, StringComparison.OrdinalIgnoreCase)) return null;
        var ruta = Uri.UnescapeDataString(url[baseUrl.Length..].Split('?', '#')[0]);
        return ruta.Length == 0 ? null : ruta;
    }

    private static async Task Ejecutar(Func<Task> operacion)
    {
        try
        {
            await operacion();
        }
        catch (Exception e) when (e is RequestFailedException or AggregateException or HttpRequestException)
        {
            throw new ErrorAlmacenamiento(e);
        }
    }
}
