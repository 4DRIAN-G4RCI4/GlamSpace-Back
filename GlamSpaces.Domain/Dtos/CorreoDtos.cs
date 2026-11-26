namespace GlamSpaces.Domain.Dtos;

// Sprint 10 · Verificación del correo y recuperación de la cuenta con un código de 6 dígitos.

// POST /api/usuarios/verificarCorreo
public class VerificarCorreoRequest
{
    public string Correo { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
}

// POST /api/usuarios/reenviarCodigo · Proposito: "verificacion" o "recuperacion"
public class ReenviarCodigoRequest
{
    public string Correo { get; set; } = string.Empty;
    public string Proposito { get; set; } = string.Empty;
}

// POST /api/usuarios/recuperar
public class RecuperarCuentaRequest
{
    public string Correo { get; set; } = string.Empty;
}

// POST /api/usuarios/restablecer
public class RestablecerContrasenaRequest
{
    public string Correo { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string NuevaContrasena { get; set; } = string.Empty;
}

// Para qué es un código. Son los mismos textos que guarda la tabla CodigosCorreo.
public static class PropositoCodigo
{
    public const string Verificacion = "verificacion";
    public const string Recuperacion = "recuperacion";

    public static bool EsValido(string? proposito) => proposito is Verificacion or Recuperacion;
}

// Resultado de comprobar un código (lo decide ICodigoCorreoRepositorio.Validar).
public enum ResultadoCodigo
{
    Valido,
    Incorrecto,
    Vencido, // también cuando no hay ningún código vigente
    DemasiadosIntentos,
}
