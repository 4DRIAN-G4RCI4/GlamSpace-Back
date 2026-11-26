namespace GlamSpaces.Domain.Servicios;

// Sprint 10 · Los correos con el código: asunto, HTML (con la paleta de GlamSpaces) y texto plano
// para los clientes de correo que no muestran HTML. El nombre se codifica para el HTML.
public static class PlantillaCorreo
{
    public sealed record Mensaje(string Asunto, string Html, string Texto);

    public static Mensaje Para(string proposito, string nombre, string codigo)
    {
        var primerNombre = (nombre ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        var saludo = primerNombre.Length > 0 ? $"Hola, {primerNombre}" : "Hola";
        var esRecuperacion = proposito == PropositoCodigo.Recuperacion;

        var asunto = esRecuperacion ? $"{codigo} es tu código para cambiar tu contraseña" : $"{codigo} es tu código para verificar tu correo";
        var explicacion = esRecuperacion
            ? "Recibimos una solicitud para cambiar la contraseña de tu cuenta de GlamSpaces. Escribe este código en la app:"
            : "Gracias por crear tu cuenta en GlamSpaces. Para activarla, escribe este código en la app:";
        var aviso = esRecuperacion
            ? "Si no pediste cambiar tu contraseña, ignora este correo: tu contraseña sigue igual."
            : "Si no creaste una cuenta en GlamSpaces, ignora este correo.";
        const string vigencia = "El código vence en 15 minutos y solo sirve una vez.";

        var texto = $"{saludo}:\n\n{explicacion}\n\n    {codigo}\n\n{vigencia}\n{aviso}\n\n— GlamSpaces";

        var html = $"""
            <!doctype html>
            <html lang="es"><body style="margin:0;padding:24px;background:#fbf7f2;font-family:Arial,Helvetica,sans-serif;color:#2b1f24">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:480px;margin:0 auto;background:#ffffff;border:1px solid #e8dfd9;border-radius:16px">
                <tr><td style="background:#6b2942;border-radius:16px 16px 0 0;padding:20px 28px;color:#f3e4c8;font-family:Georgia,serif;font-size:18px;letter-spacing:4px">GLAMSPACES</td></tr>
                <tr><td style="padding:28px">
                  <p style="margin:0 0 12px;font-size:16px;font-weight:bold">{WebUtility.HtmlEncode(saludo)}:</p>
                  <p style="margin:0 0 20px;font-size:14px;line-height:22px;color:#7c6b70">{explicacion}</p>
                  <p style="margin:0 0 20px;text-align:center;font-size:32px;font-weight:bold;letter-spacing:8px;color:#6b2942">{codigo}</p>
                  <p style="margin:0 0 8px;font-size:13px;color:#7c6b70">{vigencia}</p>
                  <p style="margin:0;font-size:13px;color:#7c6b70">{aviso}</p>
                </td></tr>
              </table>
            </body></html>
            """;

        return new Mensaje(asunto, html, texto);
    }
}
