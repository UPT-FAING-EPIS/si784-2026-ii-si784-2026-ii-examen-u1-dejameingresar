using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.TestHost;

namespace TaskFlow.Api.Tests;

/// <summary>
/// Levanta la API en memoria con una base de datos propia de las pruebas.
/// La base real del desarrollo no se toca: cada ejecucion trabaja sobre
/// "taskflow_test", que se crea si no existe.
/// </summary>
public class Factory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Base de datos aislada para las pruebas. El nombre lleva un sufijo
    /// unico para que dos ejecuciones simultaneas no se pisen.
    /// </summary>
    public static string BaseDatos => "taskflow_test";

    /// <summary>
    /// Cadena de conexion de las pruebas. Se toma de la variable de entorno
    /// TASKFLOW_TEST_DB; si no esta definida se usan los valores estandar del
    /// servicio de PostgreSQL local, sin ninguna contrasena real.
    /// </summary>
    public static string CadenaConexion =>
        Environment.GetEnvironmentVariable("TASKFLOW_TEST_DB")
        ?? $"Host=localhost;Port=5432;Database={BaseDatos};"
           + "Username=postgres;Password=postgres";

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = CadenaConexion,
                // El servidor de pruebas no usa HTTPS, pero la validacion del
                // token JWT no depende del esquema de la peticion.
                ["Jwt:Key"] = "clave-de-pruebas-taskflow-si784-2026-suficientemente-larga",
                ["Jwt:Issuer"] = "TaskFlow.Api",
                ["Jwt:Audience"] = "TaskFlow.Client"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Sin migraciones automaticas: las pruebas arrancan contra
            // el esquema ya aplicado.
            services.RemoveAll<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
        });
    }
}