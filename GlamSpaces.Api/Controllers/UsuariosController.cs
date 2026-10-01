using System.Text.RegularExpressions;
using GlamSpaces.Api.Data;
using GlamSpaces.Api.Dtos;
using GlamSpaces.Api.Models;
using GlamSpaces.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GlamSpaces.Api.Controllers;

[ApiController]
[Route("api/usuarios")]
public class UsuariosController : ControllerBase
{
    private static readonly Regex RegexCorreo = new(
        @"^[^\s@]+@[^\s@]+\.[^\s@]+$",
        RegexOptions.Compiled
    );

    private const int LongitudMinPassword = 8;

    private readonly GlamSpacesContext _db;

    public UsuariosController(GlamSpacesContext db)
    {
        _db = db;
    }

    // POST /api/usuarios/registro
    // Cubre HU-02 (registro de cliente) y HU-03 (registro de administrador de salón).
    [HttpPost("registro")]
    public async Task<ActionResult<UsuarioResponse>> Registrar([FromBody] RegistroRequest request)
    {
        var errores = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(request.NombreCompleto))
            errores["nombreCompleto"] = "Escribe tu nombre completo.";

        if (string.IsNullOrWhiteSpace(request.Correo))
            errores["correo"] = "Escribe tu correo electrónico.";
        else if (!RegexCorreo.IsMatch(request.Correo.Trim()))
            errores["correo"] = "El formato del correo no es válido.";

        if (string.IsNullOrEmpty(request.Password))
            errores["password"] = "Escribe una contraseña.";
        else if (request.Password.Length < LongitudMinPassword)
            errores["password"] = $"La contraseña debe tener al menos {LongitudMinPassword} caracteres.";

        var tipoCuenta = request.TipoCuenta?.Trim().ToLowerInvariant() ?? "";
        if (tipoCuenta != "cliente" && tipoCuenta != "administrador")
            errores["tipoCuenta"] = "El tipo de cuenta debe ser \"cliente\" o \"administrador\".";

        if (tipoCuenta == "administrador" && string.IsNullOrWhiteSpace(request.NombreSalon))
            errores["nombreSalon"] = "Escribe el nombre de tu salón.";

        if (errores.Count > 0)
            return ValidationProblem(new ValidationProblemDetails(
                errores.ToDictionary(e => e.Key, e => new[] { e.Value })
            ));

        var correoNormalizado = request.Correo.Trim().ToLowerInvariant();

        bool correoYaExiste = await _db.Usuarios
            .AnyAsync(u => u.Correo.ToLower() == correoNormalizado);

        if (correoYaExiste)
        {
            return Conflict(new { mensaje = "Ya existe una cuenta con este correo.", campo = "correo" });
        }

        var usuario = new Usuario
        {
            NombreCompleto = request.NombreCompleto.Trim(),
            Correo = correoNormalizado,
            PasswordHash = PasswordHasher.Hashear(request.Password),
            TipoCuenta = tipoCuenta,
            NombreSalon = tipoCuenta == "administrador" ? request.NombreSalon!.Trim() : null,
            FechaRegistro = DateTime.Now,
        };

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(ObtenerPorId), new { id = usuario.Id }, AUsuarioResponse(usuario));
    }

    // POST /api/usuarios/login
    // Cubre HU-04 (inicio de sesión).
    [HttpPost("login")]
    public async Task<ActionResult<UsuarioResponse>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Correo) || string.IsNullOrEmpty(request.Password))
        {
            return BadRequest(new { mensaje = "Correo y contraseña son obligatorios." });
        }

        var correoNormalizado = request.Correo.Trim().ToLowerInvariant();
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Correo == correoNormalizado);

        // Mensaje genérico a propósito: no revela si falló el correo o la contraseña.
        if (usuario == null || !PasswordHasher.Verificar(request.Password, usuario.PasswordHash))
        {
            return Unauthorized(new { mensaje = "Correo o contraseña incorrectos." });
        }

        return Ok(AUsuarioResponse(usuario));
    }

    // GET /api/usuarios/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<UsuarioResponse>> ObtenerPorId(int id)
    {
        var usuario = await _db.Usuarios.FindAsync(id);
        if (usuario == null) return NotFound();
        return Ok(AUsuarioResponse(usuario));
    }

    private static UsuarioResponse AUsuarioResponse(Usuario u) => new()
    {
        Id = u.Id,
        NombreCompleto = u.NombreCompleto,
        Correo = u.Correo,
        TipoCuenta = u.TipoCuenta,
        NombreSalon = u.NombreSalon,
        FechaRegistro = u.FechaRegistro,
    };
}
