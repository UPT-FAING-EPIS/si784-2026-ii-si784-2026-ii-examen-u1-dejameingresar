using System.ComponentModel.DataAnnotations;
using TaskFlow.Api.Models;

// EstadoTarea evita la colision de nombre con System.Threading.Tasks.TaskStatus.
namespace TaskFlow.Api.Dtos;

/// <summary>Datos necesarios para crear un proyecto.</summary>
public class CreateProjectRequest
{
    [Required(ErrorMessage = "El nombre del proyecto es obligatorio.")]
    [MaxLength(150, ErrorMessage = "El nombre no puede superar los 150 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000, ErrorMessage = "La descripcion no puede superar los 1000 caracteres.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "La fecha de inicio es obligatoria.")]
    public DateTime StartDate { get; set; }

    /// <summary>Fecha de fin. Si se indica, no puede ser anterior a la de inicio.</summary>
    public DateTime? EndDate { get; set; }

    [Required(ErrorMessage = "Debe indicar el responsable del proyecto.")]
    [Range(1, int.MaxValue, ErrorMessage = "El responsable indicado no es valido.")]
    public int OwnerId { get; set; }
}

/// <summary>Datos para modificar un proyecto existente.</summary>
public class UpdateProjectRequest
{
    [Required(ErrorMessage = "El nombre del proyecto es obligatorio.")]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    [Range(1, int.MaxValue)]
    public int OwnerId { get; set; }
}

/// <summary>Datos necesarios para crear una actividad.</summary>
public class CreateTaskRequest
{
    [Required(ErrorMessage = "El titulo de la actividad es obligatorio.")]
    [MaxLength(200, ErrorMessage = "El titulo no puede superar los 200 caracteres.")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Debe indicar el proyecto al que pertenece la actividad.")]
    [Range(1, int.MaxValue, ErrorMessage = "El proyecto indicado no es valido.")]
    public int ProjectId { get; set; }

    /// <summary>Miembro asignado. Opcional: una actividad puede empezar sin asignar.</summary>
    public int? AssigneeId { get; set; }

    [Required(ErrorMessage = "La fecha limite es obligatoria.")]
    public DateTime DueDate { get; set; }
}

/// <summary>Datos para modificar una actividad.</summary>
public class UpdateTaskRequest
{
    [Required(ErrorMessage = "El titulo de la actividad es obligatorio.")]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Miembro asignado. Puede ser nulo para dejar la actividad sin asignar.</summary>
    public int? AssigneeId { get; set; }

    /// <summary>Estado nuevo de la actividad.</summary>
    public EstadoTarea Status { get; set; }

    /// <summary>Avance en porcentaje. Se valida contra el rango 0 a 100.</summary>
    [Range(0, 100, ErrorMessage = "El avance debe estar entre 0 y 100.")]
    public int Progress { get; set; }

    [Required]
    public DateTime DueDate { get; set; }
}

/// <summary>Datos para registrar un comentario en una actividad.</summary>
public class CreateCommentRequest
{
    [Required(ErrorMessage = "El comentario no puede estar vacio.")]
    [MinLength(1, ErrorMessage = "El comentario no puede estar vacio.")]
    [MaxLength(1500, ErrorMessage = "El comentario no puede superar los 1500 caracteres.")]
    public string Body { get; set; } = string.Empty;

    [Required(ErrorMessage = "Debe indicar el autor del comentario.")]
    [Range(1, int.MaxValue)]
    public int AuthorId { get; set; }
}

/// <summary>Credenciales para iniciar sesion.</summary>
public class LoginRequest
{
    [Required, EmailAddress(ErrorMessage = "El correo no tiene un formato valido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contrasena es obligatoria.")]
    [MinLength(6, ErrorMessage = "La contrasena debe tener al menos 6 caracteres.")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Datos para registrar un usuario nuevo.</summary>
public class RegisterRequest
{
    [Required, MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress(ErrorMessage = "El correo no tiene un formato valido.")]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8, ErrorMessage = "La contrasena debe tener al menos 8 caracteres.")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Respuesta del inicio de sesion: token y datos del usuario.</summary>
public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public UserDto User { get; set; } = null!;
}

/// <summary>Vista publica de un usuario, sin su hash de contrasena.</summary>
public class UserDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

/// <summary>Vista de un proyecto con su avance calculado.</summary>
public class ProjectDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }

    /// <summary>Avance global del proyecto en porcentaje, de 0 a 100.</summary>
    public int Progress { get; set; }
}

/// <summary>Vista de una actividad.</summary>
public class TaskDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public int? AssigneeId { get; set; }
    public string? AssigneeName { get; set; }
    public string Status { get; set; } = string.Empty;
    public int Progress { get; set; }
    public DateTime DueDate { get; set; }
    public int CommentCount { get; set; }
}

/// <summary>Vista de un comentario.</summary>
public class CommentDto
{
    public int Id { get; set; }
    public string Body { get; set; } = string.Empty;
    public int TaskId { get; set; }
    public int AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
