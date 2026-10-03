# TaskFlow · Gestion de proyectos y actividades

**Examen Unidad 1** · SI-784 Calidad y Pruebas de Software · 2026-II
Rodriguez Cardenas, Patrick (2022075751)

Plataforma web para planificar proyectos, asignar actividades y seguir el
avance del equipo.

## Enlaces

| | |
|---|---|
| **Aplicacion publicada** | https://taskflow-blue-alpha.vercel.app |
| **Repositorio** | https://github.com/UPT-FAING-EPIS/si784-2026-ii-si784-2026-ii-examen-u1-dejameingresar |
| **SonarCloud** | https://sonarcloud.io/dashboard?id=TaskFlowApi |

> **SonarCloud y el cliente ya estan publicados.** La API .NET todavia no:
> ver [Estado del despliegue](#estado-del-despliegue).

### Como se publico en SonarCloud

El analisis lo genera el propio repositorio, con el workflow `sonar.yml`:

```bash
dotnet sonarscanner begin /k:$SONAR_TOKEN /o:upt-lab-calidad \
  /d:sonar.host.url=https://sonarcloud.io
dotnet build
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage
dotnet sonarscanner end
```

El alcance se acota con `/d:sonar.sources=TaskFlow.Api,TaskFlow.Api.Tests`:
el cliente web y la infraestructura los revisan Semgrep y tfsec, que dan mas
detalle sobre esos lenguajes. El token llega por `/k:` y **nunca** se versiona.

## Que hace

- Proyectos con descripcion, fechas y responsable.
- Actividades asignables a un miembro del equipo.
- Estados: pendiente, en progreso, completada y bloqueada, con avance de 0 a 100.
- Panel personal con las tareas asignadas y el progreso propio.
- Panel de administracion con el resumen global.
- Autenticacion JWT con dos roles: Usuario y Administrador.

## Pila tecnologica

| Capa | Tecnologia |
|---|---|
| API | .NET 8 (ASP.NET Core Web API) |
| Datos | PostgreSQL 16 con Entity Framework Core 8 |
| Autenticacion | JWT (HMAC-SHA256) y PBKDF2 para las contrasenas |
| Frontend | HTML, CSS y JavaScript sin framework |
| Pruebas | xUnit con `WebApplicationFactory` |
| IaC | Terraform sobre Azure |
| Contenedor | Dockerfile multietapa |

## Estructura

```
TaskFlow.Api/          API: modelos, contexto, DTOs, controladores, servicios
  Migrations/          Migraciones de Entity Framework Core
TaskFlow.Api.Tests/    Pruebas de integracion
web/                   Cliente: HTML, CSS y JavaScript
infra/                 Terraform para Azure
herramientas/          Generador de documentacion y de capturas
doc/                   Diccionario de datos y diagramas Mermaid
pruebas-endpoints.sh   Prueba de extremo a extremo de la API
Dockerfile             Imagen de contenedor multietapa
```

## Los 9 endpoints

| Metodo | Ruta | Que hace |
|---|---|---|
| POST | `/projects` | Crear proyecto. |
| GET | `/projects` | Listar proyectos, con filtro por responsable. |
| GET | `/projects/{id}` | Detalle de un proyecto. |
| PUT | `/projects/{id}` | Editar proyecto. |
| DELETE | `/projects/{id}` | Eliminar proyecto y sus actividades. |
| POST | `/tasks` | Crear actividad. |
| GET | `/tasks?projectId={id}` | Listar actividades de un proyecto. |
| PUT | `/tasks/{id}` | Actualizar estado, avance o datos. |
| POST | `/tasks/{id}/comments` | Agregar comentario. |

Ademas: `POST /auth/register`, `POST /auth/login`, `GET /dashboard/{userId}`,
`GET /dashboard/admin`, `GET /health` y la interfaz de Swagger en `/swagger`.

## Validacion

Se aplica en los dos extremos:

- **Cliente** (`web/js/validacion.js`): reglas identicas a las del servidor,
  para avisar sin gastar un viaje de ida y vuelta.
- **Servidor**: anotaciones en los DTO y reglas de negocio en los controladores
  — fecha de fin no anterior al inicio, responsable existente, avance entre 0 y
  100, y una actividad completada debe tener el avance al 100.

## Ejecutar en local

### Requisitos

- .NET SDK 8
- PostgreSQL 16

### Pasos

```bash
# 1. Base de datos
createdb taskflow

# 2. Configuracion local
cp TaskFlow.Api/appsettings.Local.example.json TaskFlow.Api/appsettings.Local.json
# editar con la cadena de conexion real

# 3. Migraciones
dotnet ef database update --project TaskFlow.Api

# 4. API
dotnet run --project TaskFlow.Api

# 5. Cliente, en otra terminal
cd web && python3 -m http.server 8090
```

La API queda en `http://localhost:5080` con Swagger en `/swagger`, y el
cliente en `http://localhost:8090`.

## Pruebas

```bash
# Pruebas de integracion (requiere la base taskflow_test)
createdb taskflow_test
dotnet ef database update --project TaskFlow.Api \
  --connection "Host=localhost;Port=5432;Database=taskflow_test;Username=prodriguez;Password=..."
dotnet test

# Prueba de extremo a extremo (requiere la API levantada)
./pruebas-endpoints.sh
```

## Automatizaciones

| Archivo | Que hace |
|---|---|
| `infra.yml` | Aprovisiona en Azure con Terraform y analiza con tfsec. |
| `sonar.yml` | Escaneo con SonarCloud y quality gate. |
| `snyk-semgrep.yml` | Semgrep sobre el codigo, Snyk sobre codigo e imagen. |
| `deploy.yml` | Construye la imagen, la publica y actualiza el servicio. |
| `generate-documentation.yml` | Regenera el diccionario y los diagramas. |

Los secretos (`SONAR_TOKEN`, `SNYK_TOKEN`, `AZURE_CREDENTIALS`, `AZURE_WEBAPP_NAME`,
entre otros) se configuran en **Settings → Secrets and variables → Actions**.

## Documentacion generada

`doc/` contiene el diccionario de datos y los diagramas en Mermaid:
entidad-relacion, clases, componentes y despliegue. Los produce
`herramientas/generar-documentacion.mjs` en cada push a `main`.

## Despliegue

### Cliente web · Vercel

El cliente es estatico puro, asi que Vercel lo sirve sin build:

```bash
cd web
vercel --prod
```

`web/vercel.json` fija `outputDirectory` y las cabeceras de seguridad
(CSP, `X-Frame-Options`, `X-Content-Type-Options`).

La URL de la API se resuelve en `web/config.js`. Si `TASKFLOW_API` no esta
definida, el cliente usa el origen desde el que se sirvio, de modo que el
mismo build sirve para desarrollo y para produccion.

### API .NET · contenedor

Vercel no tiene runtime de .NET, asi que la API va a un hosting de
contenedores. `Dockerfile` es multietapa y `infra/render.yaml` la describe:

```bash
# Render: lee el repositorio y construye el Dockerfile
```

Las variables de entorno necesarias son `ConnectionStrings__DefaultConnection`,
`Jwt__Key`, `Jwt__Issuer` y `Jwt__Audience`.

### En Azure

`infra/main.tf` crea el grupo de recursos, PostgreSQL, el registro de
contenedores y el App Service. `deploy.yml` construye la imagen, la publica
en GitHub Container Registry y actualiza el servicio. Requiere los secretos
`AZURE_CREDENTIALS` y `AZURE_WEBAPP_NAME`.

## Estado del despliegue

| Parte | Estado |
|---|---|
| API y 9 endpoints | Verificados: 42/42 en la prueba de extremo a extremo. |
| Cliente web | Verificado con datos reales. |
| Pruebas de integracion | Requieren `createdb taskflow_test`. |
| Imagen de contenedor | El `Dockerfile` es multietapa; se construye en la CI. |
| Cliente web | Publicado en Vercel: https://taskflow-blue-alpha.vercel.app |
| API .NET | Requiere un hosting de contenedores con credenciales de Azure. |
| Semgrep | Se ejecuta en cada push; el informe se publica como artefacto. |
| Snyk | Snyk Code exige un plan de pago. Semgrep cubre el analisis del codigo. |
