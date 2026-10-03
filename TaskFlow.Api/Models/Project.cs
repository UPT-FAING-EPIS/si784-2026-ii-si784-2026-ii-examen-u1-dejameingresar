using System.ComponentModel.DataAnnotations;

using System.Text.Json.Serialization;
using TaskFlow.Api.Data;

namespace TaskFlow.Api.Models;

/// <summary>Estado de una actividad dentro del proyecto.</summary>
[JsonConverter(typeof(EstadoTareaJsonConverter))]
public enum EstadoTarea
{
    Pendiente = 0,
    EnProgreso = 1,
    Completada = 2,
    Bloqueada = 3
}

/// <summary>Rol del usuario dentro de la plataforma.</summary>
public enum UserRole
{
    Usuario = 0,
    Administrador = 1
}

/// <summary>
/// Proyecto: agrupa un conjunto de actividades con un responsable y unas fechas.
/// </summary>
public class Project
{
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [Required]
    public int OwnerId { get; set; }

    public User Owner { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<TaskItem> Tasks { get; set; } = new();
}

/// <summary>
/// Actividad o tarea asignada a un miembro del equipo, con su estado y su avance.
/// </summary>
public class TaskItem
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public int ProjectId { get; set; }

    public Project Project { get; set; } = null!;

    /// <summary>Miembro asignado. Puede ser nulo si la actividad no se ha asignado todavía.</summary>
    public int? AssigneeId { get; set; }

    public User? Assignee { get; set; }

    public EstadoTarea Status { get; set; } = EstadoTarea.Pendiente;

    /// <summary>Avance en porcentaje, entre 0 y 100.</summary>
    [Range(0, 100)]
    public int Progress { get; set; }

    public DateTime DueDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Comment> Comments { get; set; } = new();
}

/// <summary>Comentario registrado por un miembro sobre una actividad.</summary>
public class Comment
{
    public int Id { get; set; }

    [Required, MaxLength(1500)]
    public string Body { get; set; } = string.Empty;

    [Required]
    public int TaskId { get; set; }

    public TaskItem Task { get; set; } = null!;

    [Required]
    public int AuthorId { get; set; }

    public User Author { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>Usuario de la plataforma, con su rol y sus credenciales.</summary>
public class User
{
    public int Id { get; set; }

    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Usuario;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Project> OwnedProjects { get; set; } = new();

    public List<TaskItem> AssignedTasks { get; set; } = new();
}
