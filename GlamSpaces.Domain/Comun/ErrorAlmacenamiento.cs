namespace GlamSpaces.Domain.Comun;

// HU-07 (fotos): el almacenamiento de archivos (Azure Blob Storage) no respondió o rechazó la operación.
// La implementación envuelve aquí el error de Azure; la API responde 5000 con un mensaje genérico
// y nunca muestra el detalle interno.
public sealed class ErrorAlmacenamiento : Exception
{
    public ErrorAlmacenamiento(Exception interna) : base("Falló el almacenamiento de fotos.", interna) { }
}
