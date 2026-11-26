namespace GlamSpaces.Domain.Comun;

// Sprint 10 · El servicio de correo (Azure Communication Services) no respondió o rechazó el envío.
// La implementación envuelve aquí el error de Azure; nunca se muestra el detalle al usuario.
public sealed class ErrorCorreo : Exception
{
    public ErrorCorreo(Exception interna) : base("Falló el envío del correo.", interna) { }
}
