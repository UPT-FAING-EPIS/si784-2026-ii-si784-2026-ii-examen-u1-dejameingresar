using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Data;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Models;

namespace TaskFlow.Api.Controllers;

/// <summary>
/// Panel de usuario: actividades asignadas y progreso personal.
/// </summary>
[ApiController]
[Route("dashboard")]
[Authorize]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly TaskFlowContext _contexto;

    /// <summary>Crea el controlador con el contexto de datos.</summary>
    public DashboardController(TaskFlowContext contexto)
    {
        _contexto = contexto;
    }

    /// <summary>GET /dashboard/{userId} — Resumen del progreso personal.</summary>
    /// <param name="userId">Usuario cuyo panel se consulta.</param>
    [HttpGet("{userId:int}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> Panel(int userId)
    {
        var usuario = await _contexto.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (usuario is null)
        {
            return NotFound();
        }

        var tareas = await _contexto.Tasks
            .AsNoTracking()
            .Include(t => t.Project)
            .Include(t => t.Assignee)
            .Include(t => t.Comments)
            .Where(t => t.AssigneeId == userId)
            .OrderBy(t => t.DueDate)
            .ToListAsync();

        var proyectos = await _contexto.Projects
            .AsNoTracking()
            .Include(p => p.Owner)
            .Include(p => p.Tasks)
            .Where(p => p.OwnerId == userId || p.Tasks.Any(t => t.AssigneeId == userId))
            .ToListAsync();

        var porEstado = tareas
            .GroupBy(t => t.Status)
            .ToDictionary(g => g.Key.ToString(), g => g.Count());

        return Ok(new
        {
            Usuario = new UserDto
            {
                Id = usuario.Id,
                Name = usuario.Name,
                Email = usuario.Email,
                Role = usuario.Role.ToString()
            },
            TotalTareasAsignadas = tareas.Count,
            Completadas = porEstado.GetValueOrDefault(nameof(EstadoTarea.Completada)),
            Pendientes = porEstado.GetValueOrDefault(nameof(EstadoTarea.Pendiente)),
            EnProgreso = porEstado.GetValueOrDefault(nameof(EstadoTarea.EnProgreso)),
            Bloqueadas = porEstado.GetValueOrDefault(nameof(EstadoTarea.Bloqueada)),
            ProgresoPersonal = tareas.Count == 0
                ? 0
                : (int)Math.Round(tareas.Average(t => t.Progress)),
            Proyectos = proyectos.Select(p => new ProjectDto
            {
                Id = p.Id,
                Name = p.Name,
                OwnerId = p.OwnerId,
                OwnerName = p.Owner?.Name ?? string.Empty,
                TotalTasks = p.Tasks.Count,
                CompletedTasks = p.Tasks.Count(t => t.Status == EstadoTarea.Completada),
                Progress = p.Tasks.Count == 0
                    ? 0
                    : (int)Math.Round(p.Tasks.Count(t => t.Status == EstadoTarea.Completada) * 100.0 / p.Tasks.Count)
            }).ToList(),
            Tareas = tareas.Select(t => new TaskDto
            {
                Id = t.Id,
                Title = t.Title,
                ProjectId = t.ProjectId,
                ProjectName = t.Project?.Name ?? string.Empty,
                Status = t.Status.ToString(),
                Progress = t.Progress,
                DueDate = t.DueDate,
                CommentCount = t.Comments.Count
            }).ToList()
        });
    }

    /// <summary>GET /dashboard/admin — Resumen global para el administrador.</summary>
    [Authorize(Roles = "Administrador")]
    [HttpGet("admin")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> PanelAdmin()
    {
        var totalUsuarios = await _contexto.Users.CountAsync();
        var totalProyectos = await _contexto.Projects.CountAsync();
        var totalTareas = await _contexto.Tasks.CountAsync();
        var completadas = await _contexto.Tasks.CountAsync(t => t.Status == EstadoTarea.Completada);

        return Ok(new
        {
            TotalUsuarios = totalUsuarios,
            TotalProyectos = totalProyectos,
            TotalTareas = totalTareas,
            Completadas = completadas,
            AvanceGlobal = totalTareas == 0
                ? 0
                : (int)Math.Round(completadas * 100.0 / totalTareas)
        });
    }
}
