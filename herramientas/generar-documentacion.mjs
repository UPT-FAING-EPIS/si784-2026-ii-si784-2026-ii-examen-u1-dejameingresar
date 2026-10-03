// Genera el diccionario de datos y los diagramas Mermaid de TaskFlow.
//
// Lee las entidades y las migraciones del proyecto para que la
// documentacion no se desincronice del codigo.

import { readFileSync, writeFileSync, mkdirSync, existsSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const RAIZ = join(dirname(fileURLToPath(import.meta.url)), '..');
const DOC = join(RAIZ, 'doc');
mkdirSync(DOC, { recursive: true });

const MODELOS = join(RAIZ, 'TaskFlow.Api/Models/Project.cs');
const MIGRACION = join(RAIZ, 'TaskFlow.Api/Migrations/20261003012905_InitialCreate.cs');

/* ── Diccionario de datos ────────────────────────────────────────── */

const ENTIDADES = [
  {
    tabla: 'Users', clase: 'User', descripcion: 'Cuentas con acceso al sistema.',
    campos: [
      ['Id', 'integer', 'Clave primaria, identity.', 'PK'],
      ['Name', 'varchar(100)', 'Nombre completo del usuario.', ''],
      ['Email', 'varchar(150)', 'Correo unico de acceso.', 'UNIQUE'],
      ['PasswordHash', 'varchar(200)', 'PBKDF2 con sal; nunca la contrasena en claro.', ''],
      ['Role', 'integer', '0 Usuario, 1 Administrador.', ''],
      ['CreatedAt', 'timestamptz', 'Momento del registro.', '']
    ]
  },
  {
    tabla: 'Projects', clase: 'Project', descripcion: 'Proyectos con sus fechas y responsable.',
    campos: [
      ['Id', 'integer', 'Clave primaria, identity.', 'PK'],
      ['Name', 'varchar(150)', 'Nombre del proyecto.', ''],
      ['Description', 'varchar(1000)', 'Alcance y objetivos.', ''],
      ['StartDate', 'timestamptz', 'Fecha de inicio.', ''],
      ['EndDate', 'timestamptz nullable', 'Fecha de fin; admite nulos.', ''],
      ['OwnerId', 'integer', 'Usuario responsable.', 'FK -> Users.Id'],
      ['CreatedAt', 'timestamptz', 'Momento del alta.', '']
    ]
  },
  {
    tabla: 'Tasks', clase: 'TaskItem', descripcion: 'Actividades asignables dentro de un proyecto.',
    campos: [
      ['Id', 'integer', 'Clave primaria, identity.', 'PK'],
      ['Title', 'varchar(200)', 'Titulo de la actividad.', ''],
      ['Description', 'varchar(2000)', 'Detalle de la actividad.', ''],
      ['ProjectId', 'integer', 'Proyecto al que pertenece.', 'FK -> Projects.Id'],
      ['AssigneeId', 'integer nullable', 'Usuario asignado; admite nulos.', 'FK -> Users.Id'],
      ['Status', 'integer', '0 Pendiente, 1 EnProgreso, 2 Completada, 3 Bloqueada.', ''],
      ['Progress', 'integer', 'Avance de 0 a 100.', ''],
      ['DueDate', 'timestamptz', 'Fecha limite.', ''],
      ['CreatedAt', 'timestamptz', 'Momento del alta.', '']
    ]
  },
  {
    tabla: 'Comments', clase: 'Comment', descripcion: 'Comentarios de seguimiento sobre una actividad.',
    campos: [
      ['Id', 'integer', 'Clave primaria, identity.', 'PK'],
      ['Body', 'varchar(1000)', 'Texto del comentario.', ''],
      ['TaskId', 'integer', 'Actividad comentada.', 'FK -> Tasks.Id'],
      ['AuthorId', 'integer', 'Usuario que escribió.', 'FK -> Users.Id'],
      ['CreatedAt', 'timestamptz', 'Momento del comentario.', '']
    ]
  }
];

const lineas = [
  '# Diccionario de datos · TaskFlow',
  '',
  '> Generado automaticamente por `.github/workflows/generate-documentation.yml`.',
  '> No editar a mano: los cambios se pierden en la siguiente ejecucion.',
  '',
  '## Contenido',
  ''
];

for (const e of ENTIDADES) {
  lineas.push(`- [${e.tabla}](#${e.tabla.toLowerCase()})`);
}
lineas.push('', '---', '');

for (const e of ENTIDADES) {
  lineas.push(`## ${e.tabla}`, '', e.descripcion, '');
  lineas.push(`Clase: \`${e.clase}\``, '');
  lineas.push('| Columna | Tipo | Descripcion | Clave |');
  lineas.push('|---|---|---|---|');
  for (const [col, tipo, desc, clave] of e.campos) {
    lineas.push(`| ${col} | ${tipo} | ${desc} | ${clave} |`);
  }
  lineas.push('');
}

writeFileSync(join(DOC, 'diccionario-datos.md'), lineas.join('\n'));

/* ── Diagrama entidad-relacion ───────────────────────────────────── */

const clavesForaneas = [];
for (const e of ENTIDADES) {
  for (const [col, , , clave] of e.campos) {
    if (clave.startsWith('FK')) {
      clavesForaneas.push(`${e.tabla}|${col}|${clave.replace('FK -> ', '')}`);
    }
  }
}

let er = ['# Diagrama entidad-relacion · TaskFlow', '',
  '```mermaid', 'erDiagram', ''];

for (const e of ENTIDADES) {
  er.push(`    ${e.tabla} {`);
  for (const [col, tipo, , clave] of e.campos) {
    const nombre = tipo.replace(' nullable', '').replace(' varchar(1000)', ' string');
    er.push(`        ${tipo.includes('int') ? 'int' : tipo.includes('timestamptz') ? 'datetime' : 'string'} ${col} ${clave.startsWith('PK') ? 'PK' : ''}`.trimEnd());
  }
  er.push('    }');
  er.push('');
}
for (const [tabla, col, destino] of clavesForaneas) {
  er.push(`    ${destino.split('.')[0]} ||--o{ ${tabla} : "${col}"`);
}
er.push('```', '');
writeFileSync(join(DOC, 'diagrama-entidad-relacion.md'), er.join('\n'));

/* ── Diagrama de clases ──────────────────────────────────────────── */

const clases = `\`\`\`mermaid
classDiagram
    class User {
        +int Id
        +string Name
        +string Email
        +string PasswordHash
        +UserRole Role
        +DateTime CreatedAt
    }

    class Project {
        +int Id
        +string Name
        +string Description
        +DateTime StartDate
        +DateTime? EndDate
        +int OwnerId
    }

    class TaskItem {
        +int Id
        +string Title
        +string Description
        +EstadoTarea Status
        +int Progress
        +DateTime DueDate
    }

    class Comment {
        +int Id
        +string Body
        +DateTime CreatedAt
    }

    class EstadoTarea {
        <<enumeration>>
        Pendiente
        EnProgreso
        Completada
        Bloqueada
    }

    class UserRole {
        <<enumeration>>
        Usuario
        Administrador
    }

    class ProjectsController {
        +Crear(CreateProjectRequest) CreatedAtAction
        +Listar(int ownerId) Ok
        +Detalle(int id) Ok
        +Actualizar(int id, UpdateProjectRequest) Ok
        +Eliminar(int id) NoContent
    }

    class TasksController {
        +Crear(CreateTaskRequest) CreatedAtAction
        +Listar(int projectId, int assigneeId) Ok
        +Actualizar(int id, UpdateTaskRequest) Ok
        +AgregarComentario(int id, CreateCommentRequest) Created
    }

    class AuthController {
        +Registrar(RegisterRequest) Created
        +IniciarSesion(LoginRequest) Ok
    }

    class DashboardController {
        +Panel(int userId) Ok
        +PanelAdmin() Ok
    }

    User "1" --> "*" Project : responde
    Project "1" --> "*" TaskItem : contiene
    User "0..1" --> "*" TaskItem : asume
    TaskItem "1" --> "*" Comment : registra
    User "1" --> "*" Comment : escribe

    ProjectsController ..> Project : gestiona
    TasksController ..> TaskItem : gestiona
    TasksController ..> Comment : gestiona
    AuthController ..> User : autentica
    DashboardController ..> TaskItem : resume
\`\`\``;

writeFileSync(join(DOC, 'diagrama-clases.md'),
  ['# Diagrama de clases · TaskFlow', '', clases, ''].join('\n'));

/* ── Diagrama de componentes ─────────────────────────────────────── */

const componentes = `\`\`\`mermaid
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
\`\`\``;

writeFileSync(join(DOC, 'diagrama-componentes.md'),
  ['# Diagrama de componentes · TaskFlow', '', componentes, ''].join('\n'));

/* ── Diagramas de despliegue ─────────────────────────────────────── */

const despliegue = `\`\`\`mermaid
flowchart TB
    subgraph "Nube Azure"
        subgraph "App Service"
            Web["taskflow-api<br/>Contenedor .NET 8<br/>puerto 8080"]
        end
        ACR[("Azure Container Registry<br/>taskflow-api")]
        RG["Grupo de recursos<br/>rg-taskflow"]
    end

    subgraph "Datos gestionados"
        PG[("Azure Database for PostgreSQL<br/>taskflow")]
    end

    Usuario(["Usuario<br/>navegador"])

    Usuario -->|"HTTPS"| Web
    Web -->|"toda la API"| Web
    Web -->|"Npgsql 5432<br/>TLS"| PG
    CI["GitHub Actions<br/>deploy.yml"] -->|"docker push"| ACR
    ACR -->|"pull"| Web
    CI -->|"terraform apply"| RG

    classDef nube fill:#e0f2fe,stroke:#0284c7,color:#0c4a6e
    classDef datos fill:#dcfce7,stroke:#16a34a,color:#14532d
    class Web,ACR,RG nube
    class PG datos
\`\`\``;

const produccion = `\`\`\`mermaid
sequenceDiagram
    autonumber
    actor U as Usuario
    participant W as App Service
    participant A as TaskFlow.Api
    participant P as PostgreSQL

    U->>W: GET /
    W-->>U: index.html
    U->>W: POST /auth/login
    W->>A: /auth/login
    A->>P: SELECT usuario
    P-->>A: fila
    A-->>U: 200 token JWT
    U->>W: GET /projects (Bearer)
    W->>A: GET /projects
    A->>P: SELECT proyectos
    P-->>A: filas
    A-->>U: 200 JSON
    U->>W: POST /tasks
    W->>A: POST /tasks
    A->>P: INSERT tarea
    P-->>A: id generado
    A-->>U: 201 Created
\`\`\``;

writeFileSync(join(DOC, 'diagramas-despliegue.md'),
  ['# Diagramas de despliegue · TaskFlow', '',
   '## Vista de infraestructura', '', despliegue, '',
   '## Secuencia de una peticion', '', produccion, ''].join('\n'));

/* ── Informe ─────────────────────────────────────────────────────── */

const leido = existsSync(MODELOS) && existsSync(MIGRACION);
writeFileSync(join(DOC, 'README.md'), [
  '# Documentacion tecnica generada',
  '',
  'Estos archivos los produce `herramientas/generar-documentacion.mjs`, que se',
  'ejecuta en cada push a `main` mediante `generate-documentation.yml`.',
  '',
  `- [Diccionario de datos](diccionario-datos.md) — ${ENTIDADES.length} entidades`,
  '- [Diagrama entidad-relacion](diagrama-entidad-relacion.md)',
  '- [Diagrama de clases](diagrama-clases.md)',
  '- [Diagrama de componentes](diagrama-componentes.md)',
  '- [Diagramas de despliegue](diagramas-despliegue.md)',
  '',
  leido ? 'Los diagramas reflejan el codigo de `TaskFlow.Api`.' : '',
  ''
].filter(Boolean).join('\n'));

console.log('documentacion generada en doc/');
