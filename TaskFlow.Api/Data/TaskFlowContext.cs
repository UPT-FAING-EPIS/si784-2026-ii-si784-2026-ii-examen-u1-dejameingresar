using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Models;

namespace TaskFlow.Api.Data;

/// <summary>Contexto de datos de TaskFlow, sobre PostgreSQL.</summary>
public class TaskFlowContext : DbContext
{
    /// <summary>Crea el contexto con las opciones recibidas por inyeccion de dependencias.</summary>
    public TaskFlowContext(DbContextOptions<TaskFlowContext> options) : base(options)
    {
    }

    /// <summary>Proyectos del equipo.</summary>
    public DbSet<Project> Projects => Set<Project>();

    /// <summary>Actividades de los proyectos.</summary>
    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    /// <summary>Comentarios de las actividades.</summary>
    public DbSet<Comment> Comments => Set<Comment>();

    /// <summary>Usuarios de la plataforma.</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>Define las relaciones y las restricciones del modelo.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(150);
            entity.Property(p => p.Description).HasMaxLength(1000);
            entity.HasOne(p => p.Owner)
                  .WithMany(u => u.OwnedProjects)
                  .HasForeignKey(p => p.OwnerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Title).IsRequired().HasMaxLength(200);
            entity.Property(t => t.Description).HasMaxLength(2000);
            entity.Property(t => t.Status).HasConversion<string>();
            entity.HasOne(t => t.Project)
                  .WithMany(p => p.Tasks)
                  .HasForeignKey(t => t.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(t => t.Assignee)
                  .WithMany(u => u.AssignedTasks)
                  .HasForeignKey(t => t.AssigneeId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Comment>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Body).IsRequired().HasMaxLength(1500);
            entity.HasOne(c => c.Task)
                  .WithMany(t => t.Comments)
                  .HasForeignKey(c => c.TaskId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(c => c.Author)
                  .WithMany()
                  .HasForeignKey(c => c.AuthorId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Name).IsRequired().HasMaxLength(80);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(150);
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Role).HasConversion<string>();
        });
    }
}
