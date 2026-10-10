namespace GlamSpaces.Domain.Entities;

public class Usuario
{
    public int Id { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string TipoCuenta { get; set; } = string.Empty; // "cliente" o "administrador"
    public string? NombreSalon { get; set; } // solo si TipoCuenta = administrador
    public DateTime FechaRegistro { get; set; } = DateTime.Now;
}
