namespace GlamSpaces.Domain.Entities;

public class Reservacion
{
    public int Id { get; set; }
    public int SalonId { get; set; }
    public int ClienteId { get; set; } // Usuario (TipoCuenta = cliente) que reserva
    public int? PaqueteId { get; set; } // paquete elegido; null si todavía no elige
    public DateTime FechaEvento { get; set; } // solo fecha (DATE en la BD)
    public string Estado { get; set; } = "pendiente"; // "pendiente", "confirmada" o "cancelada"
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
}
