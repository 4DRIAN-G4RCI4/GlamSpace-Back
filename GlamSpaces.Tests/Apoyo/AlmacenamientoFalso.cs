namespace GlamSpaces.Tests.Apoyo;

// HU-07 (fotos): almacenamiento de fotos en memoria (no llama a Azure). Guarda lo que se subió y
// se borró, y puede fallar a propósito para probar cómo responde la API.
public sealed class AlmacenamientoFalso : IAlmacenamientoFotos
{
    public const string Base = "https://cuenta.blob.core.windows.net/fotos-salones/";

    public Dictionary<string, (byte[] Contenido, string ContentType)> Archivos { get; } = [];
    public List<string> Eliminadas { get; } = [];
    public List<int> CarpetasEliminadas { get; } = [];
    public bool FallarAlSubir { get; set; }
    public bool FallarAlEliminar { get; set; }

    public async Task<string> Subir(string ruta, Stream contenido, string contentType)
    {
        if (FallarAlSubir) throw new ErrorAlmacenamiento(new InvalidOperationException("Azure no respondió"));
        using var copia = new MemoryStream();
        await contenido.CopyToAsync(copia);
        Archivos[ruta] = (copia.ToArray(), contentType);
        return Base + ruta;
    }

    public Task Eliminar(string url)
    {
        if (FallarAlEliminar) throw new ErrorAlmacenamiento(new InvalidOperationException("Azure no respondió"));
        Eliminadas.Add(url);
        if (url.StartsWith(Base)) Archivos.Remove(url[Base.Length..]);
        return Task.CompletedTask;
    }

    public Task EliminarCarpetaDeSalon(int salonId)
    {
        if (FallarAlEliminar) throw new ErrorAlmacenamiento(new InvalidOperationException("Azure no respondió"));
        CarpetasEliminadas.Add(salonId);
        foreach (var ruta in Archivos.Keys.Where(r => r.StartsWith($"{salonId}/")).ToList()) Archivos.Remove(ruta);
        return Task.CompletedTask;
    }
}
