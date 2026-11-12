namespace GlamSpaces.Domain.Interfaces;

// HU-07 (fotos): dónde viven los archivos de las fotos (Azure Blob Storage en Infrastructure).
// Si el almacenamiento falla, las implementaciones lanzan ErrorAlmacenamiento.
public interface IAlmacenamientoFotos
{
    // Sube el archivo a {ruta} (ej. "12/3f2a….jpg") y regresa su URL pública.
    Task<string> Subir(string ruta, Stream contenido, string contentType);

    // Borra el archivo de esa URL. Si ya no existe, o la URL no es de nuestro contenedor
    // (fotos viejas con URLs externas), no hace nada y no es error.
    Task Eliminar(string url);

    // Borra todos los archivos del salón (carpeta {salonId}/).
    Task EliminarCarpetaDeSalon(int salonId);
}
