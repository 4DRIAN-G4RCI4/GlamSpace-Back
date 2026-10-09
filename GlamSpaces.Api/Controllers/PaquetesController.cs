using GlamSpaces.Api.Data;
using GlamSpaces.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlamSpaces.Api.Controllers;

// Cubre HU-06: editar y borrar paquetes (agregarlos está en SalonesController).
[ApiController]
[Route("api/paquetes")]
public class PaquetesController : ControllerBase
{
    private readonly GlamSpacesContext _db;

    public PaquetesController(GlamSpacesContext db)
    {
        _db = db;
    }

    // PUT /api/paquetes/{id}
    // Actualiza un paquete. Solo el dueño del salón al que pertenece.
    [HttpPut("{id:int}")]
    public async Task<ActionResult<PaqueteResponse>> Actualizar(int id, [FromBody] PaqueteRequest request)
    {
        var error = SalonesController.ValidarPaquete(request);
        if (error != null) return BadRequest(new { mensaje = error });

        var paquete = await _db.Paquetes.Include(p => p.Salon).FirstOrDefaultAsync(p => p.Id == id);
        if (paquete == null) return NotFound();
        if (paquete.Salon!.AdminId != request.AdminId) return SalonesController.NoEsDueno();

        paquete.NombrePaquete = request.NombrePaquete.Trim();
        paquete.Descripcion = request.Descripcion?.Trim();
        paquete.Precio = request.Precio;

        await _db.SaveChangesAsync();
        return Ok(SalonesController.APaqueteResponse(paquete));
    }

    // DELETE /api/paquetes/{id}?adminId=5
    // Borra un paquete. Solo el dueño. No deja un salón publicado sin paquetes.
    // AdminId va en la URL porque un DELETE normalmente no lleva body.
    // TODO: cuando haya JWT, sacar AdminId del token en vez de recibirlo en la URL.
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id, [FromQuery] int adminId)
    {
        var paquete = await _db.Paquetes.Include(p => p.Salon).FirstOrDefaultAsync(p => p.Id == id);
        if (paquete == null) return NotFound();
        if (paquete.Salon!.AdminId != adminId) return SalonesController.NoEsDueno();

        if (paquete.Salon.Estado == "publicado")
        {
            int totalPaquetes = await _db.Paquetes.CountAsync(p => p.SalonId == paquete.SalonId);
            if (totalPaquetes <= 1)
                return BadRequest(new { mensaje = "Un salón publicado debe tener al menos un paquete. Despublícalo antes de borrar el último." });
        }

        _db.Paquetes.Remove(paquete);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
