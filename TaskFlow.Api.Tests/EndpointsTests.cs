using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Data;
using TaskFlow.Api.Models;
using Xunit;

namespace TaskFlow.Api.Tests;

/// <summary>
/// Pruebas de integracion sobre la API real.
///
/// Cada prueba levanta su propio servidor en memoria y trabaja contra la base
/// "taskflow_test", que se trunca al empezar. Todos los datos se crean a
/// traves de los endpoints, nunca insertandolos con EF: asi la prueba recorre
/// exactamente el camino que recorre un cliente, incluida la validacion.
/// </summary>
public class EndpointsTests : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory = new Factory();
    private readonly HttpClient _cliente;
    private readonly TaskFlowContext _contexto;

    /// <summary>Crea el cliente y el contexto de la prueba.</summary>
    public EndpointsTests()
    {
        _cliente = _factory.CreateClient();

        var opciones = new DbContextOptionsBuilder<TaskFlowContext>()
            .UseNpgsql(Factory.CadenaConexion)
            .Options;

        _contexto = new TaskFlowContext(opciones);
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        // El esquema debe existir antes de intentar vaciar las tablas.
        await _contexto.Database.MigrateAsync();
        await Vaciar();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await Vaciar();
        _contexto.Dispose();
        _cliente.Dispose();
        await _factory.DisposeAsync();
    }

    /// <summary>
    /// Vacia las cuatro tablas en una sola sentencia. TRUNCATE resuelve el
    /// orden de las claves foraneas por si mismo, cosa que el borrado fila a
    /// fila de EF no puede hacer sin desensamblar antes los hijos.
    /// </summary>
    private async Task Vaciar()
    {
        await _contexto.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE \"Comments\", \"Tasks\", \"Projects\", \"Users\" RESTART IDENTITY CASCADE");
    }

    /* ── Utilidades ─────────────────────────────────────────────── */

    private static StringContent Json(object datos) =>
        new(JsonSerializer.Serialize(datos), System.Text.Encoding.UTF8, "application/json");

    private async Task<(string Token, int UsuarioId)> CrearUsuarioAsync(
        string nombre = "Ana Quispe",
        string rol = "Usuario")
    {
        var correo = $"{Guid.NewGuid()}@taskflow.test";
        var respuesta = await _cliente.PostAsJsonAsync("/auth/register",
            new { name = nombre, email = correo, password = "ClaveSegura123" });
        respuesta.EnsureSuccessStatusCode();

        var login = await _cliente.PostAsJsonAsync("/auth/login",
            new { email = correo, password = "ClaveSegura123" });
        login.EnsureSuccessStatusCode();

        var cuerpo = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = cuerpo.GetProperty("token").GetString()!;
        var id = cuerpo.GetProperty("user").GetProperty("id").GetInt32();
        return (token, id);
    }

    private HttpRequestMessage Req(HttpMethod metodo, string ruta, object? cuerpo = null, string? token = null)
    {
        var peticion = new HttpRequestMessage(metodo, ruta);
        if (token is not null)
        {
            peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (cuerpo is not null)
        {
            peticion.Content = Json(cuerpo);
        }

        return peticion;
    }

    /* ── Salud y documentacion ──────────────────────────────────── */

    [Fact]
    public async Task Health_responde_ok()
    {
        var respuesta = await _cliente.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Swagger_esta_publicado()
    {
        var respuesta = await _cliente.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    /* ── Autenticacion ──────────────────────────────────────────── */

    [Fact]
    public async Task Registro_devuelve_201_con_el_usuario_creado()
    {
        var correo = $"{Guid.NewGuid()}@taskflow.test";
        var respuesta = await _cliente.PostAsJsonAsync("/auth/register",
            new { name = "Luis Diaz", email = correo, password = "ClaveSegura123" });

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Luis Diaz", cuerpo.GetProperty("name").GetString());
        Assert.Equal("Usuario", cuerpo.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Registro_rechaza_correo_duplicado()
    {
        var correo = $"{Guid.NewGuid()}@taskflow.test";
        await _cliente.PostAsJsonAsync("/auth/register",
            new { name = "Ana", email = correo, password = "ClaveSegura123" });

        var segundo = await _cliente.PostAsJsonAsync("/auth/register",
            new { name = "Otra", email = correo, password = "ClaveSegura123" });

        Assert.Equal(HttpStatusCode.BadRequest, segundo.StatusCode);
    }

    [Fact]
    public async Task Registro_rechaza_correo_invalido()
    {
        var respuesta = await _cliente.PostAsJsonAsync("/auth/register",
            new { name = "Ana", email = "esto-no-es-correo", password = "ClaveSegura123" });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Login_devuelve_token_valido()
    {
        var correo = $"{Guid.NewGuid()}@taskflow.test";
        await _cliente.PostAsJsonAsync("/auth/register",
            new { name = "Ana", email = correo, password = "ClaveSegura123" });

        var respuesta = await _cliente.PostAsJsonAsync("/auth/login",
            new { email = correo, password = "ClaveSegura123" });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        var token = cuerpo.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public async Task Login_rechaza_clave_incorrecta()
    {
        var correo = $"{Guid.NewGuid()}@taskflow.test";
        await _cliente.PostAsJsonAsync("/auth/register",
            new { name = "Ana", email = correo, password = "ClaveSegura123" });

        var respuesta = await _cliente.PostAsJsonAsync("/auth/login",
            new { email = correo, password = "ClaveEquivocada1" });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    /* ── Control de acceso ──────────────────────────────────────── */

    [Theory]
    [InlineData("/projects")]
    [InlineData("/tasks")]
    [InlineData("/dashboard/1")]
    public async Task Endpoints_requieren_token(string ruta)
    {
        var respuesta = await _cliente.GetAsync(ruta);
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Admin_requiere_el_rol_administrador()
    {
        var (token, usuarioId) = await CrearUsuarioAsync();

        var peticion = Req(HttpMethod.Get, "/dashboard/admin");
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _cliente.SendAsync(peticion);
        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    /* ── Proyectos ──────────────────────────────────────────────── */

    [Fact]
    public async Task Crear_proyecto_devuelve_201_con_los_datos()
    {
        var (token, usuarioId) = await CrearUsuarioAsync();
        var inicio = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var fin = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        var peticion = Req(HttpMethod.Post, "/projects", new
        {
            name = "Portal de clientes",
            description = "Rediseño del portal",
            startDate = inicio,
            endDate = fin,
            ownerId = usuarioId
        });
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _cliente.SendAsync(peticion);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Portal de clientes", cuerpo.GetProperty("name").GetString());
        Assert.Equal(0, cuerpo.GetProperty("totalTasks").GetInt32());
    }

    [Fact]
    public async Task Crear_proyecto_rechaza_fecha_fin_anterior_al_inicio()
    {
        var (token, usuarioId) = await CrearUsuarioAsync();

        var peticion = Req(HttpMethod.Post, "/projects", new
        {
            name = "Proyecto invalido",
            startDate = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc),
            endDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ownerId = usuarioId
        });
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _cliente.SendAsync(peticion);
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Crear_proyecto_rechaza_responsable_inexistente()
    {
        var (token, usuarioId) = await CrearUsuarioAsync();

        var peticion = Req(HttpMethod.Post, "/projects", new
        {
            name = "Sin dueno",
            startDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            ownerId = 999999
        });
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _cliente.SendAsync(peticion);
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Listar_proyectos_devuelve_200()
    {
        var (token, usuarioId) = await CrearUsuarioAsync();

        var peticion = Req(HttpMethod.Get, "/projects");
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _cliente.SendAsync(peticion);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var lista = await respuesta.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(lista);
    }

    [Fact]
    public async Task Detalle_de_proyecto_inexistente_devuelve_404()
    {
        var (token, usuarioId) = await CrearUsuarioAsync();

        var peticion = Req(HttpMethod.Get, "/projects/999999");
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _cliente.SendAsync(peticion);
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task Editar_proyecto_actualiza_los_datos()
    {
        var (token, usuarioId) = await CrearUsuarioAsync();

        var proyecto = new Project
        {
            Name = "Nombre original",
            Description = "Descripcion original",
            StartDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            OwnerId = usuarioId
        };
        _contexto.Projects.Add(proyecto);
        await _contexto.SaveChangesAsync();

        var peticion = Req(HttpMethod.Put, $"/projects/{proyecto}", new
        {
            name = "Nombre editado",
            description = "Descripcion editada",
            startDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            ownerId = usuarioId
        });
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _cliente.SendAsync(peticion);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Nombre editado", cuerpo.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Borrar_proyecto_devuelve_204_y_el_proyecto_desaparece()
    {
        var (token, usuarioId) = await CrearUsuarioAsync();

        var proyecto = new Project
        {
            Name = "Para borrar",
            Description = "Temporal",
            StartDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            OwnerId = usuarioId
        };
        _contexto.Projects.Add(proyecto);
        await _contexto.SaveChangesAsync();

        var borrar = Req(HttpMethod.Delete, $"/projects/{proyecto}");
        borrar.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var respuesta = await _cliente.SendAsync(borrar);
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);

        Assert.Null(await _contexto.Projects.FindAsync(proyecto.Id));
    }

    [Fact]
    public async Task Borrar_proyecto_inexistente_devuelve_404()
    {
        var (token, usuarioId) = await CrearUsuarioAsync();

        var peticion = Req(HttpMethod.Delete, "/projects/999999");
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _cliente.SendAsync(peticion);
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    /* ── Actividades ────────────────────────────────────────────── */

    /// <summary>
    /// Crea un proyecto mediante la API y devuelve su identificador. Se usa el
    /// endpoint y no el contexto porque la prueba debe recorrer el mismo camino
    /// que un cliente real, incluida la validacion.
    /// </summary>
    private async Task<int> CrearProyectoAsync(string token, int usuarioId)
    {
        var peticion = Req(HttpMethod.Post, "/projects", new
        {
            name = "Proyecto de prueba",
            description = "Para actividades",
            startDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
            ownerId = usuarioId
        });
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _cliente.SendAsync(peticion);
        respuesta.EnsureSuccessStatusCode();

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task Crear_actividad_devuelve_201_en_estado_pendiente()
    {
        var (token, usuarioId) = await CrearUsuarioAsync();
        var proyecto = await CrearProyectoAsync(token, usuarioId);

        var peticion = Req(HttpMethod.Post, "/tasks", new
        {
            title = "Maqueta del portal",
            description = "Wireframes",
            projectId = proyecto,
            dueDate = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc)
        });
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _cliente.SendAsync(peticion);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Pendiente", cuerpo.GetProperty("status").GetString());
        Assert.Equal(0, cuerpo.GetProperty("progress").GetInt32());
    }

    [Fact]
    public async Task Crear_actividad_rechaza_proyecto_inexistente()
    {
        var (token, usuarioId) = await CrearUsuarioAsync();

        var peticion = Req(HttpMethod.Post, "/tasks", new
        {
            title = "Huerfana",
            projectId = 999999,
            dueDate = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc)
        });
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _cliente.SendAsync(peticion);
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Listar_actividades_filtra_por_proyecto()
    {
        var (token, usuarioId) = await CrearUsuarioAsync();
        var proyecto = await CrearProyectoAsync(token, usuarioId);
        var otro = await CrearProyectoAsync(token, usuarioId);

        await CrearTareaAsync(token, proyecto, usuarioId, "Del primer proyecto");
        await CrearTareaAsync(token, otro, usuarioId, "Del segundo proyecto");

        var peticion = Req(HttpMethod.Get, $"/tasks?projectId={proyecto}");
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _cliente.SendAsync(peticion);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var lista = await respuesta.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(lista);
        Assert.Single(lista);
        Assert.Equal("Del primer proyecto", lista![0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task Actualizar_actividad_permite_marcar_completada_con_avance_100()
    {
        var (token, usuarioId) = await CrearUsuarioAsync();
        var proyecto = await CrearProyectoAsync(token, usuarioId);
        var tarea = await CrearTareaAsync(token, proyecto, usuarioId);

        var peticion = Req(HttpMethod.Put, $"/tasks/{tarea}", new
        {
            title = "Maqueta",
            description = "Wireframes",
            status = "Completada",
            progress = 100,
            dueDate = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc)
        });
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _cliente.SendAsync(peticion);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Completada", cuerpo.GetProperty("status").GetString());
        Assert.Equal(100, cuerpo.GetProperty("progress").GetInt32());
    }

    [Fact]
    public async Task Actualizar_actividad_rechaza_completada_con_avance_inferior_a_100()
    {
        var (token, usuarioId) = await CrearUsuarioAsync();
        var proyecto = await CrearProyectoAsync(token, usuarioId);
        var tarea = await CrearTareaAsync(token, proyecto, usuarioId);

        var peticion = Req(HttpMethod.Put, $"/tasks/{tarea}", new
        {
            title = "Maqueta",
            status = "Completada",
            progress = 50,
            dueDate = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc)
        });
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _cliente.SendAsync(peticion);
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Actualizar_actividad_rechaza_avance_fuera_de_rango()
    {
        var (token, usuarioId) = await CrearUsuarioAsync();
        var proyecto = await CrearProyectoAsync(token, usuarioId);
        var tarea = await CrearTareaAsync(token, proyecto, usuarioId);

        var peticion = Req(HttpMethod.Put, $"/tasks/{tarea}", new
        {
            title = "Maqueta",
            status = "EnProgreso",
            progress = 180,
            dueDate = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc)
        });
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _cliente.SendAsync(peticion);
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Actualizar_actividad_rechaza_estado_inexistente()
    {
        var (token, usuarioId) = await CrearUsuarioAsync();
        var proyecto = await CrearProyectoAsync(token, usuarioId);
        var tarea = await CrearTareaAsync(token, proyecto, usuarioId);

        var peticion = Req(HttpMethod.Put, $"/tasks/{tarea}", new
        {
            title = "Maqueta",
            status = "EstadoInventado",
            progress = 50,
            dueDate = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc)
        });
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _cliente.SendAsync(peticion);
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Agregar_comentario_devuelve_201()
    {
        var (token, usuarioId) = await CrearUsuarioAsync();
        var proyecto = await CrearProyectoAsync(token, usuarioId);

        var tarea = await CrearTareaAsync(token, proyecto, usuarioId);

        var peticion = Req(HttpMethod.Post, $"/tasks/{tarea}/comments", new
        {
            body = "Aprobada por el equipo",
            authorId = usuarioId
        });
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _cliente.SendAsync(peticion);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Aprobada por el equipo", cuerpo.GetProperty("body").GetString());
    }

    [Fact]
    public async Task Agregar_comentario_rechaza_autor_inexistente()
    {
        var (token, usuarioId) = await CrearUsuarioAsync();
        var proyecto = await CrearProyectoAsync(token, usuarioId);

        var tarea = await CrearTareaAsync(token, proyecto, usuarioId);

        var peticion = Req(HttpMethod.Post, $"/tasks/{tarea}/comments", new
        {
            body = "Autor inexistente",
            authorId = 999999
        });
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _cliente.SendAsync(peticion);
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Agregar_comentario_a_tarea_inexistente_devuelve_404()
    {
        var (token, usuarioId) = await CrearUsuarioAsync();

        var peticion = Req(HttpMethod.Post, "/tasks/999999/comments", new
        {
            body = "No existe la tarea",
            authorId = usuarioId
        });
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _cliente.SendAsync(peticion);
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    /// <summary>Crea una actividad mediante la API y devuelve su identificador.</summary>
    private async Task<int> CrearTareaAsync(
        string token,
        int proyectoId,
        int? usuarioId = null,
        string titulo = "Maqueta del portal")
    {
        var peticion = Req(HttpMethod.Post, "/tasks", new
        {
            title = titulo,
            description = "Wireframes",
            projectId = proyectoId,
            assigneeId = usuarioId,
            dueDate = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc)
        });
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _cliente.SendAsync(peticion);
        respuesta.EnsureSuccessStatusCode();

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        return cuerpo.GetProperty("id").GetInt32();
    }

    /* ── Panel ──────────────────────────────────────────────────── */

    [Fact]
    public async Task Panel_personal_resume_las_actividades_asignadas()
    {
        var (token, usuarioId) = await CrearUsuarioAsync("Ana Quispe");
        var proyecto = await CrearProyectoAsync(token, usuarioId);

        var primera = await CrearTareaAsync(token, proyecto, usuarioId, "Completada");
        var segunda = await CrearTareaAsync(token, proyecto, usuarioId, "En curso");

        foreach (var (id, estado, avance) in new[]
                 {
                     (primera, "Completada", 100),
                     (segunda, "EnProgreso", 40)
                 })
        {
            await _cliente.SendAsync(Req(HttpMethod.Put, $"/tasks/{id}", new
            {
                title = "Tarea",
                assigneeId = usuarioId,
                status = estado,
                progress = avance,
                dueDate = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc)
            }, token));
        }

        var peticion = Req(HttpMethod.Get, $"/dashboard/{usuarioId}");
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _cliente.SendAsync(peticion);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, cuerpo.GetProperty("totalTareasAsignadas").GetInt32());
        Assert.Equal(1, cuerpo.GetProperty("completadas").GetInt32());
        Assert.Equal(1, cuerpo.GetProperty("enProgreso").GetInt32());
        Assert.Equal(70, cuerpo.GetProperty("progresoPersonal").GetInt32());
    }

    [Fact]
    public async Task Panel_de_usuario_inexistente_devuelve_404()
    {
        var (token, usuarioId) = await CrearUsuarioAsync();

        var peticion = Req(HttpMethod.Get, "/dashboard/999999");
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var respuesta = await _cliente.SendAsync(peticion);
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}