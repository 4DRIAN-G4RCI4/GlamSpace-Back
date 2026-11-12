namespace GlamSpaces.Domain.Comun;

// Catálogo de códigos que regresa la API en RespuestaApi.Codigo.
// Los stored procedures regresan estos mismos números en su parámetro @Codigo.
public static class CodigosError
{
    public const int Exito = 0;

    // 1xxx: datos y usuarios
    public const int DatosInvalidos = 1001;
    public const int CorreoDuplicado = 1002;
    public const int CredencialesIncorrectas = 1003;
    public const int NoEsAdministrador = 1004;
    public const int UsuarioNoEncontrado = 1005;

    // 2xxx: salones
    public const int SalonNoEncontrado = 2001;
    public const int NoEsDueno = 2002;
    public const int SalonSinPaquetes = 2003;
    public const int EstadoInvalido = 2004;
    // HU-07 (fotos): subida real de imágenes. Salón inexistente = 2001 y no es dueño = 2002.
    public const int ArchivoRequerido = 2005;
    public const int FormatoFotoInvalido = 2006;
    public const int FotoMuyPesada = 2007;
    public const int LimiteDeFotos = 2008;
    public const int FotoNoEncontrada = 2009;

    // 3xxx: paquetes
    public const int PaqueteNoEncontrado = 3001;
    public const int UltimoPaquete = 3002;

    // 4xxx: solicitudes de reservación (HU-15)
    public const int SolicitudNoEncontrada = 4001;
    public const int FechaNoDisponible = 4002;
    public const int NoEsCliente = 4003;
    public const int SolicitudDuplicada = 4004;
    public const int SolicitudAjena = 4005;
    public const int SolicitudYaAtendida = 4006; // HU-17: ya no está pendiente (se confirmó o rechazó)

    // 5xxx: errores del servidor
    public const int ErrorInterno = 5000;
}
