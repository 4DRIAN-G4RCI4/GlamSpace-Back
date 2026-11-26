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
    // Sprint 10: las cuentas nuevas verifican su correo con un código. Por omisión true: si un SP no
    // regresa la columna, nunca se bloquea a nadie por error.
    public bool CorreoVerificado { get; set; } = true;
}
