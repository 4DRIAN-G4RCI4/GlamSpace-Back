namespace GlamSpaces.Domain.Dtos;

// Para endpoints que solo necesitan un id (ej. POST /api/salones/obtener).
public class IdRequest
{
    public int Id { get; set; }
}

// Base de cualquier listado paginado.
public class PaginacionRequest
{
    public int Pagina { get; set; } = 1;
    public int TamanoPagina { get; set; } = 10; // el SP lo limita a 100
}
