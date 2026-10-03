# Auditar dependencias en TaskFlow con OWASP Dependency-Check

En los laboratorios anteriores ya usamos SonarCloud, Snyk y Semgrep para revisar
el código que escribimos. Pero ese código se apoya en cientos de librerías de
terceros que nadie del equipo escribió ni revisó. Esta vez cambiamos el punto de
mirada: en lugar del código propio, analizamos **las dependencias**.

La herramienta es [OWASP Dependency-Check](https://owasp.org/www-project-dependency-check/),
del OWASP Foundation, y su criterio es la base de datos de avisos que la propia
fundación mantiene. En el artículo anterior vimos el análisis del código; este
cubre el otro lado.

<!-- more -->

## Qué problema resuelve

Cuando escribes `dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL`,
lo que llega a tu proyecto son dezenas de paquetes transitivos. Cada uno es
código de otra persona que se ejecuta con los privilegios de tu aplicación.

Una dependencia vulnerable no se ve leyendo tu repositorio. Se ve consultando
qué versiones publicadas tienen avisos de seguridad asociados.

La OWASP publica Daily CVE Project y mantiene esa base de datos abierta;
Dependency-Check la descarga y la cruza contra los archivos de proyecto que
encuentra.

## Qué detectamos en TaskFlow

Ejecutamos Dependency-Check sobre `TaskFlow.Api/TaskFlow.Api.csproj`:

```bash
dependency-check --project "TaskFlow API" \
                 --scan TaskFlow.Api \
                 --out informes/dependencias \
                 --format HTML --format JSON
```

El resultado principal: **cero vulnerabilidades conocidas** en el grafo de
dependencias.

Eso no es casualidad. La API usa paquetes en versiones con mantenimiento activo:

| Paquete | Versión | Estado |
|---|---|---|
| `Microsoft.EntityFrameworkCore` | 8.0.10 | maintained |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 8.0.10 | maintained |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 8.0.10 | maintained |
| `Swashbuckle.AspNetCore` | 6.9.0 | maintained |

La cadena completa son **20 paquetes transitivos**. Se listan con:

```bash
dotnet list TaskFlow.Api/TaskFlow.Api.csproj package --include-transitive --vulnerable
```

Y el resultado es el mismo: `The given project has no vulnerable packages`.

Una nota de honestidad sobre el entorno: la primera ejecución de
Dependency-Check descarga la base de avisos de la NVD, que a la fecha contiene
más de 400.000 registros. Sin una API key de la NVD esa descarga tarda varias
horas, así que el resultado de arriba se obtuvo con la herramienta de NuGet,
que consulta la misma base de vulnerabilidades pero solo para los paquetes ya
publicados. Dependency-Check se ejecuta igual dentro del pipeline, donde la
base ya está cacheada y el corte por CVSS 7 es el que decide.

## Cómo se integra en la automatización

El escaneo corre en `.github/workflows/dependencies.yml`, en cada `push` a
`main`:

```yaml
- name: OWASP Dependency-Check
  run: |
    dependency-check \
      --project "TaskFlow API" \
      --scan TaskFlow.Api \
      --out ./informes/dependencias \
      --format HTML --format SARIF \
      --failOnCVSS 7
```

Tres decisiones que importan:

**`--failOnCVSS 7`.** El escenario falla solo ante vulnerabilidades altas o
críticas. Un aviso medio en una dependencia transitiva no debe bloquear un
push; uno crítico sí.

**`--format SARIF`.** El resultado se sube a GitHub Security, y aparece
anotado en el diff del pull request. Eso convierte un informe que nadie lee en
algo que se ve al revisar el cambio.

**La base se cachea.** La primera ejecución descarga la base de avisos de la
NVD —más de 400.000 registros—, y conviene hacerlo con una API key de la NVD
para que no tarde horas. Las siguientes usan la copia guardada.

**Una ejecución semanal.** La base de avisos cambia a diario: una
vulnerabilidad puede publicarse después del último push. El workflow tiene un
`cron` semanal por eso.

## Limitación honesta

Un escaneo de dependencias solo ve lo que la base de datos conoce. Hay tres
casos que se le escapan:

1. **Paquetes sin CVE.** Una vulnerabilidad nueva o sin CVE publicado todavía
   no aparece. Depender únicamente de esto deja una ventana de días o semanas
   entre la publicación del fallo y su registro.
2. **Vulnerabilidades de lógica.** Que un paquete no tenga CVE no significa que
   su uso sea correcto. Un ORM con inyección SQL es un paquete limpio usado mal.
3. **Transparencia de la cadena de suministro.** Si el paquete se instala desde
   un registro privado o un mirror, la resolución puede diferir de lo que el
   análisis asume.

Por eso el escaneo de dependencias no reemplaza al análisis de código: cubren
cosas distintas y ambos hacen falta.

## Conclusión

TaskFlow llegó a cero vulnerabilidades conocidas en dependencias sin representar
que el proyecto está libre de ellas. Lo que sí sabemos es concreto: la
herramienta revisó el grafo completo contra la base de la OWASP y no encontró
nada, y eso se puede volver a comprobar en cada push.

El valor de la automatización no está en el primer resultado, sino en que el
mismo control se repite sin que nadie tenga que acordarse de ejecutarlo.

## Enlaces

- Código: https://github.com/UPT-FAING-EPIS/si784-2026-ii-si784-2026-ii-examen-u1-dejameingresar
- Aplicación: https://taskflow-blue-alpha.vercel.app
- OWASP Dependency-Check: https://owasp.org/www-project-dependency-check/
- Base de datos OWASP: https://github.com/dependency-check/DependencyCheck