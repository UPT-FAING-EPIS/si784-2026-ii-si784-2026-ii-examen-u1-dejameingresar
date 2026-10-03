using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Data;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Models;

namespace TaskFlow.Api.Controllers;

/// <summary>
/// Proyectos: alta, listado, detalle, edicion y eliminacion.
/// </summary>
[ApiController]
[Route("projects")]
[Authorize]
[Produces("application/json")]
public class ProjectsController : ControllerBase
{
    private readonly TaskFlowContext _contexto;

    /// <summary>Crea el controlador con el contexto de datos.</summary>
    public ProjectsController(TaskFlowContext contexto)
    {
        _contexto = contexto;
    }

    /// <summary>POST /projects — Crea un proyecto.</summary>
    /// <param name="request">Datos del proyecto.</param>
    /// <response code="201">Proyecto creado.</response>
    /// <response code="400">Los datos no son validos.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProjectDto>> Crear([FromBody] CreateProjectRequest request)
    {
        if (request.EndDate.HasValue && request.EndDate.Value < request.StartDate)
        {
            ModelState.AddModelError(nameof(request.EndDate),
                "La fecha de fin no puede ser anterior a la fecha de inicio.");
            return ValidationProblem(ModelState);
        }

        if (!await _contexto.Users.AnyAsync(u => u.Id == request.OwnerId))
        {
            ModelState.AddModelError(nameof(request.OwnerId),
                "El responsable indicado no existe en el sistema.");
            return ValidationProblem(ModelState);
        }

        var proyecto = new Project
        {
            Name = request.Name,
            Description = request.Description,
            StartDate = DateTimeConverter.ToUtc(request.StartDate),
            EndDate = DateTimeConverter.ToUtc(request.EndDate),
            OwnerId = request.OwnerId
        };

        _contexto.Projects.Add(proyecto);
        await _contexto.SaveChangesAsync();

        var dto = await Mapear(proyecto.Id);
        return CreatedAtAction(nameof(Detalle), new { id = dto.Id }, dto);
    }

    /// <summary>GET /projects — Lista los proyectos.</summary>
    /// <param name="ownerId">Si se indica, filtra por responsable.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ProjectDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ProjectDto>>> Listar([FromQuery] int? ownerId)
    {
        var consulta = _contexto.Projects
            .Include(p => p.Owner)
            .Include(p => p.Tasks)
            .AsNoTracking();

        if (ownerId.HasValue)
        {
            consulta = consulta.Where(p => p.OwnerId == ownerId.Value);
        }

        var proyectos = await consulta.OrderBy(p => p.Id).ToListAsync();
        return Ok(proyectos.Select(MapearEstatico));
    }

    /// <summary>GET /projects/{id} — Detalle de un proyecto.</summary>
    [HttpGet("{id:int}", Name = nameof(Detalle))]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDto>> Detalle(int id)
    {
        var dto = await Mapear(id);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>PUT /projects/{id} — Edita un proyecto.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ProjectDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectDto>> Editar(int id, [FromBody] UpdateProjectRequest request)
    {
        if (request.EndDate.HasValue && request.EndDate.Value < request.StartDate)
        {
            ModelState.AddModelError(nameof(request.EndDate),
                "La fecha de fin no puede ser anterior a la fecha de inicio.");
            return ValidationProblem(ModelState);
        }

        var proyecto = await _contexto.Projects
            .Include(p => p.Owner)
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (proyecto is null)
        {
            return NotFound();
        }

        if (!await _contexto.Users.AnyAsync(u => u.Id == request.OwnerId))
        {
            ModelState.AddModelError(nameof(request.OwnerId),
                "El responsable indicado no existe en el sistema.");
            return ValidationProblem(ModelState);
        }

        proyecto.Name = request.Name;
        proyecto.Description = request.Description;
        proyecto.StartDate = DateTimeConverter.ToUtc(request.StartDate);
        proyecto.EndDate = DateTimeConverter.ToUtc(request.EndDate);
        proyecto.OwnerId = request.OwnerId;

        await _contexto.SaveChangesAsync();
        return Ok(MapearEstatico(proyecto));
    }

    /// <summary>DELETE /projects/{id} — Elimina un proyecto y sus actividades.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(int id)
    {
        var proyecto = await _contexto.Projects.FirstOrDefaultAsync(p => p.Id == id);
        if (proyecto is null)
        {
            return NotFound();
        }

        _contexto.Projects.Remove(proyecto);
        await _contexto.SaveChangesAsync();
        return NoContent();
    }

    private async Task<ProjectDto?> Mapear(int id)
    {
        var proyecto = await _contexto.Projects
            .AsNoTracking()
            .Include(p => p.Owner)
            .Include(p => p.Tasks)
            .FirstOrDefaultAsync(p => p.Id == id);
        return proyecto is null ? null : MapearEstatico(proyecto);
    }

    private static ProjectDto MapearEstatico(Project proyecto)
    {
        var total = proyecto.Tasks.Count;
        var completadas = proyecto.Tasks.Count(t => t.Status == EstadoTarea.Completada);
        return new ProjectDto
        {
            Id = proyecto.Id,
            Name = proyecto.Name,
            Description = proyecto.Description,
            StartDate = proyecto.StartDate,
            EndDate = proyecto.EndDate,
            OwnerId = proyecto.OwnerId,
            OwnerName = proyecto.Owner?.Name ?? string.Empty,
            TotalTasks = total,
            CompletedTasks = completadas,
            Progress = total == 0 ? 0 : (int)Math.Round(completadas * 100.0 / total)
        };
    }
}
