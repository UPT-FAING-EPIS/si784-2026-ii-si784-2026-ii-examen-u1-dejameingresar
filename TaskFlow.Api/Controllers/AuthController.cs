using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Data;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Models;
using TaskFlow.Api.Services;

namespace TaskFlow.Api.Controllers;

/// <summary>Registro e inicio de sesion con token JWT.</summary>
[ApiController]
[Route("auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly TaskFlowContext _contexto;
    private readonly ITokenService _tokens;

    /// <summary>Crea el controlador con el contexto y el servicio de tokens.</summary>
    public AuthController(TaskFlowContext contexto, ITokenService tokens)
    {
        _contexto = contexto;
        _tokens = tokens;
    }

    /// <summary>POST /auth/register — Registra un usuario nuevo.</summary>
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserDto>> Registrar([FromBody] RegisterRequest request)
    {
        var correo = request.Email.Trim().ToLowerInvariant();
        if (await _contexto.Users.AnyAsync(u => u.Email == correo))
        {
            ModelState.AddModelError(nameof(request.Email),
                "Ya existe un usuario registrado con ese correo.");
            return ValidationProblem(ModelState);
        }

        var usuario = new User
        {
            Name = request.Name,
            Email = correo,
            PasswordHash = _tokens.HashearContrasena(request.Password),
            Role = UserRole.Usuario
        };

        _contexto.Users.Add(usuario);
        await _contexto.SaveChangesAsync();

        return Created("/auth/login", new UserDto
        {
            Id = usuario.Id,
            Name = usuario.Name,
            Email = usuario.Email,
            Role = usuario.Role.ToString()
        });
    }

    /// <summary>POST /auth/login — Inicia sesion y devuelve el token JWT.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> IniciarSesion([FromBody] LoginRequest request)
    {
        var correo = request.Email.Trim().ToLowerInvariant();
        var usuario = await _contexto.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == correo);

        if (usuario is null || !_tokens.VerificarContrasena(request.Password, usuario.PasswordHash))
        {
            return Unauthorized(new { mensaje = "Credenciales incorrectas." });
        }

        var rol = usuario.Role.ToString();
        return Ok(new LoginResponse
        {
            Token = _tokens.CrearToken(usuario.Id, usuario.Name, usuario.Email, rol),
            User = new UserDto
            {
                Id = usuario.Id,
                Name = usuario.Name,
                Email = usuario.Email,
                Role = rol
            }
        });
    }
}
