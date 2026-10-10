namespace GlamSpaces.Domain.Dtos;

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

public class LoginRequest
{
    public string Correo { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

// Lo que regresa el API: nunca incluye PasswordHash.
public class UsuarioResponse
{
    public int Id { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string TipoCuenta { get; set; } = string.Empty;
    public string? NombreSalon { get; set; }
    public DateTime FechaRegistro { get; set; }

    public static UsuarioResponse Desde(Usuario u) => new()
    {
        Id = u.Id,
        NombreCompleto = u.NombreCompleto,
        Correo = u.Correo,
        TipoCuenta = u.TipoCuenta,
        NombreSalon = u.NombreSalon,
        FechaRegistro = u.FechaRegistro,
    };
}
