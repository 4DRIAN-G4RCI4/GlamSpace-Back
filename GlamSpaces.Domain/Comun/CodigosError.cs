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
    public const int YaEsAdministrador = 1006; // convertirAAdministrador sobre una cuenta que ya lo es
    // Sprint 10 · verificación del correo y recuperación de la cuenta con código
    public const int CorreoSinVerificar = 1007; // login de una cuenta que no ha verificado su correo
    public const int CodigoIncorrecto = 1008;
    public const int CodigoVencido = 1009; // también cuando no hay código vigente
    public const int DemasiadosIntentos = 1010; // 5 intentos fallidos con el mismo código
    public const int EsperaParaReenviar = 1011; // se pidió otro código hace menos de 60 s

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
    // Mis salones: no se elimina un salón con solicitudes pendientes o confirmadas.
    public const int SalonConSolicitudesActivas = 2010;

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
