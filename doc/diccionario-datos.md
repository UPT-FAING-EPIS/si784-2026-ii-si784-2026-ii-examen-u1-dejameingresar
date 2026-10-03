# Diccionario de datos · TaskFlow

> Generado automaticamente por `.github/workflows/generate-documentation.yml`.
> No editar a mano: los cambios se pierden en la siguiente ejecucion.

## Contenido

- [Users](#users)
- [Projects](#projects)
- [Tasks](#tasks)
- [Comments](#comments)

---

## Users

Cuentas con acceso al sistema.

Clase: `User`

| Columna | Tipo | Descripcion | Clave |
|---|---|---|---|
| Id | integer | Clave primaria, identity. | PK |
| Name | varchar(100) | Nombre completo del usuario. |  |
| Email | varchar(150) | Correo unico de acceso. | UNIQUE |
| PasswordHash | varchar(200) | PBKDF2 con sal; nunca la contrasena en claro. |  |
| Role | integer | 0 Usuario, 1 Administrador. |  |
| CreatedAt | timestamptz | Momento del registro. |  |

## Projects

Proyectos con sus fechas y responsable.

Clase: `Project`

| Columna | Tipo | Descripcion | Clave |
|---|---|---|---|
| Id | integer | Clave primaria, identity. | PK |
| Name | varchar(150) | Nombre del proyecto. |  |
| Description | varchar(1000) | Alcance y objetivos. |  |
| StartDate | timestamptz | Fecha de inicio. |  |
| EndDate | timestamptz nullable | Fecha de fin; admite nulos. |  |
| OwnerId | integer | Usuario responsable. | FK -> Users.Id |
| CreatedAt | timestamptz | Momento del alta. |  |

## Tasks

Actividades asignables dentro de un proyecto.

Clase: `TaskItem`

| Columna | Tipo | Descripcion | Clave |
|---|---|---|---|
| Id | integer | Clave primaria, identity. | PK |
| Title | varchar(200) | Titulo de la actividad. |  |
| Description | varchar(2000) | Detalle de la actividad. |  |
| ProjectId | integer | Proyecto al que pertenece. | FK -> Projects.Id |
| AssigneeId | integer nullable | Usuario asignado; admite nulos. | FK -> Users.Id |
| Status | integer | 0 Pendiente, 1 EnProgreso, 2 Completada, 3 Bloqueada. |  |
| Progress | integer | Avance de 0 a 100. |  |
| DueDate | timestamptz | Fecha limite. |  |
| CreatedAt | timestamptz | Momento del alta. |  |

## Comments

Comentarios de seguimiento sobre una actividad.

Clase: `Comment`

| Columna | Tipo | Descripcion | Clave |
|---|---|---|---|
| Id | integer | Clave primaria, identity. | PK |
| Body | varchar(1000) | Texto del comentario. |  |
| TaskId | integer | Actividad comentada. | FK -> Tasks.Id |
| AuthorId | integer | Usuario que escribió. | FK -> Users.Id |
| CreatedAt | timestamptz | Momento del comentario. |  |
