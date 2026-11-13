using GlamSpaces.Domain.Interfaces;

namespace GlamSpaces.Domain.Servicios;

// HU-07 (fotos): subida real de imágenes. Coordina el archivo (IAlmacenamientoFotos) y su registro
// (IFotoSalonRepositorio). Las reglas del salón (existe, dueño, máximo 8 fotos) viven en los SP.
//
// Subir:    valida el archivo → lo sube → registra la URL. Si el registro falla, borra el archivo
//           (no quedan archivos huérfanos).
// Eliminar: valida (sin borrar) → borra el archivo → borra el registro. Si el almacenamiento falla,
//           el registro sigue y se puede reintentar.
public class FotosSalonServicio
{
    public const long TamanoMaximo = 5 * 1024 * 1024; // 5 MB

    private const string MensajeErrorAlmacenamiento = "No se pudo guardar la foto en este momento. Intenta de nuevo más tarde.";

    private readonly IFotoSalonRepositorio _fotos;
    private readonly IAlmacenamientoFotos _almacenamiento;

    public FotosSalonServicio(IFotoSalonRepositorio fotos, IAlmacenamientoFotos almacenamiento)
    {
        _fotos = fotos;
        _almacenamiento = almacenamiento;
    }

    public async Task<RespuestaApi<FotoSalonResponse>> Subir(int salonId, int adminId, Stream? contenido, long tamano)
    {
        if (contenido is null || tamano <= 0)
            return Error(CodigosError.ArchivoRequerido, "Selecciona una imagen para subir.");
        if (tamano > TamanoMaximo)
            return Error(CodigosError.FotoMuyPesada, "La imagen pesa más de 5 MB.");

        using var copia = new MemoryStream();
        await contenido.CopyToAsync(copia);
        if (copia.Length == 0)
            return Error(CodigosError.ArchivoRequerido, "Selecciona una imagen para subir.");
        if (copia.Length > TamanoMaximo)
            return Error(CodigosError.FotoMuyPesada, "La imagen pesa más de 5 MB.");

        var formato = FormatoImagen.Detectar(copia.GetBuffer().AsSpan(0, (int)copia.Length));
        if (formato is null)
            return Error(CodigosError.FormatoFotoInvalido, "Solo se aceptan imágenes JPG, PNG o WEBP.");

        copia.Position = 0;
        string url;
        try
        {
            url = await _almacenamiento.Subir($"{salonId}/{Guid.NewGuid():N}{formato.Extension}", copia, formato.ContentType);
        }
        catch (ErrorAlmacenamiento)
        {
            return Error(CodigosError.ErrorInterno, MensajeErrorAlmacenamiento);
        }

        RespuestaApi<FotoSalonResponse> registro;
        try
        {
            registro = await _fotos.Agregar(salonId, adminId, url);
        }
        catch
        {
            await EliminarSinFallar(url); // la BD no respondió: el archivo no se queda huérfano
            throw;
        }

        if (!registro.Exito) await EliminarSinFallar(url); // salón inexistente, no es dueño, ya tiene 8...
        return registro;
    }

    public async Task<RespuestaApi<FotoSalonResponse>> Eliminar(EliminarFotoRequest request)
    {
        var foto = await _fotos.Validar(request);
        if (!foto.Exito) return foto;

        try
        {
            await _almacenamiento.Eliminar(foto.Datos!.Url);
        }
        catch (ErrorAlmacenamiento)
        {
            return Error(CodigosError.ErrorInterno, "No se pudo eliminar la foto en este momento. Intenta de nuevo más tarde.");
        }

        return await _fotos.Eliminar(request);
    }

    // Borra todos los archivos del salón (carpeta {salonId}/) después de eliminarlo de la BD.
    // Mejor esfuerzo: si el almacenamiento falla regresa false y el salón ya quedó eliminado.
    public async Task<bool> EliminarFotosDeSalon(int salonId)
    {
        try
        {
            await _almacenamiento.EliminarCarpetaDeSalon(salonId);
            return true;
        }
        catch (ErrorAlmacenamiento)
        {
            return false;
        }
    }

    private async Task EliminarSinFallar(string url)
    {
        try
        {
            await _almacenamiento.Eliminar(url);
        }
        catch (ErrorAlmacenamiento)
        {
            // Se responde el error original; el archivo queda sin registro (caso raro: la BD y el
            // almacenamiento fallaron al mismo tiempo).
        }
    }

    private static RespuestaApi<FotoSalonResponse> Error(int codigo, string mensaje) =>
        RespuestaApi<FotoSalonResponse>.Error(codigo, mensaje);
}
