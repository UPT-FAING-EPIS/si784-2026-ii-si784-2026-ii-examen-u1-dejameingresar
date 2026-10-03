# Diagrama de componentes · TaskFlow

```mermaid
flowchart LR
    Cliente["Cliente web<br/>(HTML + JS)"]
    API["TaskFlow.Api<br/>ASP.NET Core 8"]
    Auth["AuthController<br/>JWT + roles"]
    Proy["ProjectsController"]
    Tar["TasksController"]
    Dash["DashboardController"]
    Ctx["TaskFlowContext<br/>EF Core"]
    PG[("PostgreSQL 16")]
    Token["TokenService<br/>PBKDF2"]

    Cliente -->|"HTTPS + Bearer"| API
    API --> Auth
    API --> Proy
    API --> Tar
    API --> Dash
    Auth --> Token
    Proy --> Ctx
    Tar --> Ctx
    Dash --> Ctx
    Ctx -->|"Npgsql"| PG

    subgraph "Presentacion"
        Cliente
    end
    subgraph "API"
        API
        Auth
        Proy
        Tar
        Dash
        Token
    end
    subgraph "Datos"
        Ctx
        PG
    end
```
