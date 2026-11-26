namespace GlamSpaces.Domain.Interfaces;

// Sprint 10 · Quién manda los correos (Azure Communication Services Email en Infrastructure).
// Si el envío falla, las implementaciones lanzan ErrorCorreo.
public interface IEnvioCorreo
{
    Task Enviar(string para, string asunto, string html, string texto);
}
