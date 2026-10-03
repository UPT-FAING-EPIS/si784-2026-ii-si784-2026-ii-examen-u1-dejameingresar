using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TaskFlow.Api.Data;
using TaskFlow.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Base de datos relacional --------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Falta configurar la cadena de conexion");

builder.Services.AddDbContext<TaskFlowContext>(options =>
    options.UseNpgsql(connectionString));

// Las fechas se normalizan a UTC en DateTimeConverter antes de guardarse:
// PostgreSQL usa timestamptz, que exige una zona definida.

// --- Autenticacion JWT ----------------------------------------------------
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Falta configurar Jwt:Key");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(5)
        };
    });

builder.Services.AddAuthorization();

// --- Servicios ------------------------------------------------------------
builder.Services.AddScoped<ITokenService, TokenService>();

// --- Controladores y validacion ------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// El cliente web se sirve desde otro origen que la API, y usa el token JWT en
// la cabecera Authorization. Sin esta politica el navegador bloquea cada
// llamada antes de que salga, y el panel se queda vacio sin avisar.
string[] origenesPorDefecto =
[
    "http://localhost:8090",
    "http://localhost:5173"
];

var origenesPermitidos = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
    ?? origenesPorDefecto;

builder.Services.AddCors(options =>
{
    options.AddPolicy("ClienteWeb", policy => policy
        .WithOrigins(origenesPermitidos)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "TaskFlow API",
        Version = "v1",
        Description = "Gestion de proyectos y actividades del equipo."
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "TaskFlow API v1");
    options.RoutePrefix = "swagger";
});

app.UseCors("ClienteWeb");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { estado = "ok" }))
   .AllowAnonymous();

app.Run();

/// <summary>Punto de entrada de la API, expuesto para las pruebas de integracion.</summary>
public partial class Program
{
}
