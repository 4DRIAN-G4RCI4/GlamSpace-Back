namespace GlamSpaces.Api.Dtos;

// Un solo request sirve para los dos tipos de cuenta (cliente o administrador).
// Si TipoCuenta == "administrador", NombreSalon es obligatorio.
public class RegistroRequest
{
    public string NombreCompleto { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string TipoCuenta { get; set; } = string.Empty; // "cliente" o "administrador"
    public string? NombreSalon { get; set; }
}
