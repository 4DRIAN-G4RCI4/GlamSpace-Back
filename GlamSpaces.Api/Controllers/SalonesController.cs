using GlamSpaces.Api.Data;
using GlamSpaces.Api.Dtos;
using GlamSpaces.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlamSpaces.Api.Controllers;

// Cubre HU-06 (CRUD de salones y sus paquetes).
// Solo valida reglas de negocio; las validaciones de formato las hace el frontend (HU-07).
[ApiController]
[Route("api/salones")]
public class SalonesController : ControllerBase
{
    private readonly GlamSpacesContext _db;

    public SalonesController(GlamSpacesContext db)
    {
        _db = db;
    }

    // POST /api/salones
    // Crea un salón asociado al administrador. Siempre nace "no_publicado"
    // porque todavía no tiene paquetes.
    [HttpPost]
    public async Task<ActionResult<SalonResponse>> Crear([FromBody] SalonRequest request)
    {
        var error = ValidarSalon(request);
        if (error != null) return BadRequest(new { mensaje = error });

        bool esAdmin = await _db.Usuarios
            .AnyAsync(u => u.Id == request.AdminId && u.TipoCuenta == "administrador");
        if (!esAdmin)
            return BadRequest(new { mensaje = "El usuario no existe o no es administrador." });

        var salon = new Salon
        {
            AdminId = request.AdminId,
            Nombre = request.Nombre.Trim(),
            Zona = request.Zona.Trim(),
            Capacidad = request.Capacidad,
            Descripcion = request.Descripcion?.Trim(),
            Estado = "no_publicado",
            FechaCreacion = DateTime.Now,
        };

        _db.Salones.Add(salon);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(ObtenerPorId), new { id = salon.Id }, ASalonResponse(salon));
    }

    // GET /api/salones/{id}
    // Detalle del salón con su lista completa de paquetes y fotos.
    [HttpGet("{id:int}")]
    public async Task<ActionResult<SalonResponse>> ObtenerPorId(int id)
    {
        var salon = await BuscarConDetalle(id);
        if (salon == null) return NotFound();
        return Ok(ASalonResponse(salon));
    }

    // PUT /api/salones/{id}
    // Actualiza los datos del salón. Solo el dueño. Para publicarlo
    // (Estado = "publicado") necesita al menos un paquete.
    [HttpPut("{id:int}")]
    public async Task<ActionResult<SalonResponse>> Actualizar(int id, [FromBody] SalonRequest request)
    {
        var error = ValidarSalon(request);
        if (error != null) return BadRequest(new { mensaje = error });

        var salon = await BuscarConDetalle(id);
        if (salon == null) return NotFound();
        if (salon.AdminId != request.AdminId) return NoEsDueno();

        // Si no mandan Estado, se conserva el actual.
        var estado = request.Estado?.Trim().ToLowerInvariant() ?? salon.Estado;
        if (estado != "publicado" && estado != "no_publicado")
            return BadRequest(new { mensaje = "El estado debe ser \"publicado\" o \"no_publicado\"." });

        if (estado == "publicado" && salon.Paquetes.Count == 0)
            return BadRequest(new { mensaje = "Para publicar el salón se requiere al menos un paquete." });

        salon.Nombre = request.Nombre.Trim();
        salon.Zona = request.Zona.Trim();
        salon.Capacidad = request.Capacidad;
        salon.Descripcion = request.Descripcion?.Trim();
        salon.Estado = estado;

        await _db.SaveChangesAsync();
        return Ok(ASalonResponse(salon));
    }

    // POST /api/salones/{id}/paquetes
    // Agrega un paquete al salón. Solo el dueño.
    [HttpPost("{id:int}/paquetes")]
    public async Task<ActionResult<PaqueteResponse>> AgregarPaquete(int id, [FromBody] PaqueteRequest request)
    {
        var error = ValidarPaquete(request);
        if (error != null) return BadRequest(new { mensaje = error });

        var salon = await _db.Salones.FindAsync(id);
        if (salon == null) return NotFound();
        if (salon.AdminId != request.AdminId) return NoEsDueno();

        var paquete = new Paquete
        {
            SalonId = id,
            NombrePaquete = request.NombrePaquete.Trim(),
            Descripcion = request.Descripcion?.Trim(),
            Precio = request.Precio,
        };

        _db.Paquetes.Add(paquete);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(ObtenerPorId), new { id }, APaqueteResponse(paquete));
    }

    // Validación compartida con PaquetesController.
    internal static string? ValidarPaquete(PaqueteRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.NombrePaquete)) return "El nombre del paquete es obligatorio.";
        if (r.Precio <= 0) return "El precio debe ser mayor a 0.";
        return null;
    }

    // Sin JWT no hay esquema de autenticación y Forbid() lanzaría excepción,
    // por eso el 403 se arma a mano.
    internal static ObjectResult NoEsDueno() =>
        new(new { mensaje = "Solo el administrador dueño del salón puede modificarlo." }) { StatusCode = 403 };

    internal static PaqueteResponse APaqueteResponse(Paquete p) => new()
    {
        Id = p.Id,
        SalonId = p.SalonId,
        NombrePaquete = p.NombrePaquete,
        Descripcion = p.Descripcion,
        Precio = p.Precio,
    };

    private Task<Salon?> BuscarConDetalle(int id) => _db.Salones
        .Include(s => s.Paquetes)
        .Include(s => s.Fotos)
        .AsSplitQuery() // evita multiplicar filas paquetes x fotos
        .FirstOrDefaultAsync(s => s.Id == id);

    private static string? ValidarSalon(SalonRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Nombre)) return "El nombre del salón es obligatorio.";
        if (string.IsNullOrWhiteSpace(r.Zona)) return "La zona es obligatoria.";
        if (r.Capacidad <= 0) return "La capacidad debe ser mayor a 0.";
        return null;
    }

    private static SalonResponse ASalonResponse(Salon s) => new()
    {
        Id = s.Id,
        AdminId = s.AdminId,
        Nombre = s.Nombre,
        Zona = s.Zona,
        Capacidad = s.Capacidad,
        Descripcion = s.Descripcion,
        Estado = s.Estado,
        FechaCreacion = s.FechaCreacion,
        Paquetes = s.Paquetes.Select(APaqueteResponse).ToList(),
        Fotos = s.Fotos.Select(f => f.Url).ToList(),
    };
}
