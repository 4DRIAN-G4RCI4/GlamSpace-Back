namespace GlamSpaces.Api.Dtos;

// Lo que regresa el API: nunca incluye PasswordHash.
public class UsuarioResponse
{
    public int Id { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string TipoCuenta { get; set; } = string.Empty;
    public string? NombreSalon { get; set; }
    public DateTime FechaRegistro { get; set; }
}
