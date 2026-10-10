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

    // 3xxx: paquetes
    public const int PaqueteNoEncontrado = 3001;
    public const int UltimoPaquete = 3002;

    // 5xxx: errores del servidor
    public const int ErrorInterno = 5000;
}
