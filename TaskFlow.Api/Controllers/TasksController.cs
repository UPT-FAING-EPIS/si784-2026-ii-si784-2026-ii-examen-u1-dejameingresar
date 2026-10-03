using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Data;
using TaskFlow.Api.Dtos;
using TaskFlow.Api.Models;

namespace TaskFlow.Api.Controllers;

/// <summary>Actividades: alta, listado por proyecto, actualizacion y comentarios.</summary>
[ApiController]
[Route("tasks")]
[Authorize]
[Produces("application/json")]
public class TasksController : ControllerBase
{
    private readonly TaskFlowContext _contexto;

    /// <summary>Crea el controlador con el contexto de datos.</summary>
    public TasksController(TaskFlowContext contexto)
    {
        _contexto = contexto;
    }

    /// <summary>POST /tasks — Crea una actividad.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TaskDto>> Crear([FromBody] CreateTaskRequest request)
    {
        if (!await _contexto.Projects.AnyAsync(p => p.Id == request.ProjectId))
        {
            ModelState.AddModelError(nameof(request.ProjectId),
                "El proyecto indicado no existe en el sistema.");
            return ValidationProblem(ModelState);
        }

        if (request.AssigneeId.HasValue
            && !await _contexto.Users.AnyAsync(u => u.Id == request.AssigneeId.Value))
        {
            ModelState.AddModelError(nameof(request.AssigneeId),
                "El miembro asignado no existe en el sistema.");
            return ValidationProblem(ModelState);
        }

        var tarea = new TaskItem
        {
            Title = request.Title,
            Description = request.Description,
            ProjectId = request.ProjectId,
            AssigneeId = request.AssigneeId,
            DueDate = DateTimeConverter.ToUtc(request.DueDate),
            Status = EstadoTarea.Pendiente,
            Progress = 0
        };

        _contexto.Tasks.Add(tarea);
        await _contexto.SaveChangesAsync();

        var dto = await Mapear(tarea.Id);
        return CreatedAtAction(nameof(Listar), new { projectId = dto.ProjectId }, dto);
    }

    /// <summary>GET /tasks?projectId={id} — Lista las actividades de un proyecto.</summary>
    /// <param name="projectId">Proyecto cuyas actividades se desean.</param>
    /// <param name="assigneeId">Si se indica, filtra por miembro asignado.</param>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TaskDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<TaskDto>>> Listar(
        [FromQuery] int? projectId,
        [FromQuery] int? assigneeId)
    {
        var consulta = _contexto.Tasks
            .Include(t => t.Project)
            .Include(t => t.Assignee)
            .Include(t => t.Comments)
            .AsNoTracking();

        if (projectId.HasValue)
        {
            consulta = consulta.Where(t => t.ProjectId == projectId.Value);
        }

        if (assigneeId.HasValue)
        {
            consulta = consulta.Where(t => t.AssigneeId == assigneeId.Value);
        }

        var tareas = await consulta.OrderBy(t => t.Id).ToListAsync();
        return Ok(tareas.Select(MapearEstatico));
    }

    /// <summary>PUT /tasks/{id} — Actualiza el estado o la informacion de una actividad.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TaskDto>> Actualizar(int id, [FromBody] UpdateTaskRequest request)
    {
        var tarea = await _contexto.Tasks.FirstOrDefaultAsync(t => t.Id == id);
        if (tarea is null)
        {
            return NotFound();
        }

        if (request.AssigneeId.HasValue
            && !await _contexto.Users.AnyAsync(u => u.Id == request.AssigneeId.Value))
        {
            ModelState.AddModelError(nameof(request.AssigneeId),
                "El miembro asignado no existe en el sistema.");
            return ValidationProblem(ModelState);
        }

        if (request.Progress < 0 || request.Progress > 100)
        {
            ModelState.AddModelError(nameof(request.Progress),
                "El avance debe estar entre 0 y 100.");
            return ValidationProblem(ModelState);
        }

        // Una actividad completada debe tener el avance al 100 por ciento.
        if (request.Status == EstadoTarea.Completada && request.Progress != 100)
        {
            ModelState.AddModelError(nameof(request.Progress),
                "Una actividad completada debe tener un avance del 100 por ciento.");
            return ValidationProblem(ModelState);
        }

        tarea.Title = request.Title;
        tarea.Description = request.Description;
        tarea.AssigneeId = request.AssigneeId;
        tarea.Status = request.Status;
        tarea.Progress = request.Progress;
        tarea.DueDate = DateTimeConverter.ToUtc(request.DueDate);

        await _contexto.SaveChangesAsync();

        var dto = await Mapear(id);
        return Ok(dto);
    }

    /// <summary>POST /tasks/{id}/comments — Agrega un comentario a una actividad.</summary>
    [HttpPost("{id:int}/comments")]
    [ProducesResponseType(typeof(CommentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommentDto>> AgregarComentario(
        int id,
        [FromBody] CreateCommentRequest request)
    {
        var tarea = await _contexto.Tasks.FirstOrDefaultAsync(t => t.Id == id);
        if (tarea is null)
        {
            return NotFound();
        }

        if (!await _contexto.Users.AnyAsync(u => u.Id == request.AuthorId))
        {
            ModelState.AddModelError(nameof(request.AuthorId),
                "El autor indicado no existe en el sistema.");
            return ValidationProblem(ModelState);
        }

        var comentario = new Comment
        {
            Body = request.Body,
            TaskId = id,
            AuthorId = request.AuthorId
        };

        _contexto.Comments.Add(comentario);
        await _contexto.SaveChangesAsync();

        var autor = await _contexto.Users
            .AsNoTracking()
            .Where(u => u.Id == request.AuthorId)
            .Select(u => u.Name)
            .FirstOrDefaultAsync();

        return Created($"/tasks/{id}/comments", new CommentDto
        {
            Id = comentario.Id,
            Body = comentario.Body,
            TaskId = comentario.TaskId,
            AuthorId = comentario.AuthorId,
            AuthorName = autor ?? string.Empty,
            CreatedAt = comentario.CreatedAt
        });
    }

    private async Task<TaskDto> Mapear(int id)
    {
        var tarea = await _contexto.Tasks
            .AsNoTracking()
            .Include(t => t.Project)
            .Include(t => t.Assignee)
            .Include(t => t.Comments)
            .FirstAsync(t => t.Id == id);
        return MapearEstatico(tarea);
    }

    private static TaskDto MapearEstatico(TaskItem tarea) => new()
    {
        Id = tarea.Id,
        Title = tarea.Title,
        Description = tarea.Description,
        ProjectId = tarea.ProjectId,
        ProjectName = tarea.Project?.Name ?? string.Empty,
        AssigneeId = tarea.AssigneeId,
        AssigneeName = tarea.Assignee?.Name,
        Status = tarea.Status.ToString(),
        Progress = tarea.Progress,
        DueDate = tarea.DueDate,
        CommentCount = tarea.Comments.Count
    };
}
