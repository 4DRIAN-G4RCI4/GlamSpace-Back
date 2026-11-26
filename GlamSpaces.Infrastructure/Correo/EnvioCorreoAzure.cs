namespace GlamSpaces.Infrastructure.Correo;

// Sprint 10 · Correos con Azure Communication Services Email (recurso glamspaces-comunicacion,
// dominio administrado por Azure). WaitUntil.Started: la petición no espera a que el correo se
// entregue, solo a que Azure lo acepte. Cualquier error de Azure se envuelve en ErrorCorreo.
public sealed class EnvioCorreoAzure : IEnvioCorreo
{
    private readonly EmailClient _cliente;
    private readonly string _remitente;

    public EnvioCorreoAzure(OpcionesCorreo opciones)
        : this(new EmailClient(opciones.CadenaConexion), opciones.Remitente) { }

    internal EnvioCorreoAzure(EmailClient cliente, string remitente)
    {
        _cliente = cliente;
        _remitente = remitente;
    }

    public async Task Enviar(string para, string asunto, string html, string texto)
    {
        try
        {
            await _cliente.SendAsync(WaitUntil.Started, _remitente, para, asunto, html, texto);
        }
        catch (RequestFailedException e)
        {
            throw new ErrorCorreo(e);
        }
    }
}

// Variables de entorno: Correo__CadenaConexion (Communication Services → Claves) y Correo__Remitente
// (DoNotReply@….azurecomm.net). En local van en user-secrets; NUNCA en appsettings.json ni en el repo.
public sealed class OpcionesCorreo
{
    public const string Seccion = "Correo";

    public string CadenaConexion { get; set; } = string.Empty;
    public string Remitente { get; set; } = string.Empty;
}
