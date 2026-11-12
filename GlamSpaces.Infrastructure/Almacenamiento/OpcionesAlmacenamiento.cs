namespace GlamSpaces.Infrastructure.Almacenamiento;

// HU-07 (fotos): configuración del almacenamiento de fotos (Azure Blob Storage).
// Variables de entorno: Almacenamiento__CadenaConexion y Almacenamiento__Contenedor (fotos-salones).
// En local van en user-secrets; NUNCA en appsettings.json ni en el repo. Program.cs las valida al arrancar.
public sealed class OpcionesAlmacenamiento
{
    public const string Seccion = "Almacenamiento";

    public string CadenaConexion { get; set; } = string.Empty;
    public string Contenedor { get; set; } = string.Empty;
}
