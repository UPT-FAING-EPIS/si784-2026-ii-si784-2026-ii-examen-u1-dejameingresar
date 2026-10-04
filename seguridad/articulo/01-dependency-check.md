# Auditar dependencias con OWASP Dependency-Check

En los laboratorios anteriores revisamos el código con SonarCloud, Snyk y
Semgrep. Pero ese código se apoya en cientos de librerías de terceros que nadie
del equipo escribió ni revisó. Esta vez cambiamos el punto de mira: en lugar del
código propio, analizamos **las dependencias**.

La herramienta es [OWASP Dependency-Check](https://owasp.org/www-project-dependency-check/),
del OWASP Foundation, y su criterio es la base de datos de avisos que la propia
fundación mantiene.

<!-- more -->

## El problema

Cuando escribes `dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL`, lo
que llega a tu proyecto son decenas de paquetes transitivos. Cada uno es código
de otra persona que se ejecuta con los privilegios de tu aplicación.

Una dependencia vulnerable no se ve leyendo tu repositorio. Se ve consultando
qué versiones publicadas tienen avisos de seguridad asociados.

## Qué revisamos

El proyecto de ejemplo es una API en ASP.NET Core 8 con PostgreSQL. Su grafo de
dependencias tiene **20 paquetes transitivos**:

```
Microsoft.EntityFrameworkCore                8.0.10
Npgsql.EntityFrameworkCore.PostgreSQL        8.0.10
Microsoft.AspNetCore.Authentication.JwtBearer 8.0.10
Swashbuckle.AspNetCore                       6.9.0
Microsoft.IdentityModel.Tokens               7.1.2
Npgsql                                       8.0.5
System.Collections.Immutable                 6.0.0
... (14 más)
```

La herramienta de NuGet consulta esa misma base de vulnerabilidades:

```bash
dotnet list TaskFlow.Api/TaskFlow.Api.csproj package \
                 --include-transitive --vulnerable
```

Resultado:

```
The given project `TaskFlow.Api` has no vulnerable packages
given the current sources.
```

**Cero vulnerabilidades conocidas** en el grafo completo, incluidas las
dependencias transitivas.

## La herramienta de la OWASP

Dependency-Check hace lo mismo pero con su propia base de datos, que es la
Daily CVE Project de la OWASP:

```bash
dependency-check --project "TaskFlow API" \
                 --scan TaskFlow.Api \
                 --out informes/dependencias \
                 --format HTML --format SARIF \
                 --failOnCVSS 7
```

Dos diferencias con la herramienta de NuGet:

**La base es de la OWASP, no de NuGet.** NuGet solo conoce los paquetes que él
publica; Dependency-Check también cubre registros privados y otras fuentes.

**Puede generar SARIF**, que GitHub interpreta y muestra anotado en el pull
request. Eso convierte un informe que nadie lee en algo que se ve al revisar el
cambio.

### Una nota sobre el tiempo de la primera ejecución

La base de avisos de la NVD contiene, a la fecha, más de **400.000 registros**.
Sin una API key de la NVD, la descarga inicial tarda varias horas. Con una key
—gratuita— baja a minutos:

https://nvd.nist.gov/developers/request-an-api-key

Las ejecuciones siguientes usan la copia en caché. Por eso el escaneo va
programado en el pipeline y no como un paso manual.

## Cómo se integra en la automatización

```yaml
- name: OWASP Dependency-Check
  run: |
    dependency-check \
      --project "TaskFlow API" \
      --scan TaskFlow.Api \
      --out ./informes/dependencias \
      --format HTML \
      --format SARIF \
      --data ~/.gradle/caches \
      --failOnCVSS 7
```

Tres decisiones:

**`--failOnCVSS 7`.** El escenario falla solo ante vulnerabilidades altas o
críticas. Un aviso medio en una dependencia transitiva no debe bloquear un
push; uno crítico sí.

**`--format SARIF`.** El resultado se sube a GitHub Security y aparece anotado
en el diff del pull request.

**Ejecución semanal.** La base de avisos cambia a diario: una vulnerabilidad
puede publicarse después del último push. El workflow tiene un `cron` semanal
además del disparo por `push`.

## Limitación honesta

Un escaneo de dependencias solo ve lo que la base de datos conoce. Tres casos se
le escapan:

1. **Paquetes sin CVE.** Una vulnerabilidad nueva o todavía sin CVE publicado
   no aparece. Queda una ventana de días o semanas entre la publicación del
   fallo y su registro.
2. **Vulnerabilidades de lógica.** Que un paquete no tenga CVE no significa que
   su uso sea correcto. Un ORM con inyección SQL es un paquete limpio usado mal.
3. **Resolución distinta a la esperada.** Si el paquete se instala desde un
   registro privado o un mirror, la resolución puede diferir de lo que el
   análisis asume.

Por eso el escaneo de dependencias no reemplaza al análisis de código. Cubren
cosas distintas y ambos hacen falta.

## Conclusión

Cero vulnerabilidades conocidas en 20 paquetes transitivos no significa que el
proyecto esté libre de ellas. Lo que sí sabemos es concreto: se consultó la base
de avisos y no encontró nada, y eso se puede volver a comprobar en cada push.

El valor de la automatización no está en el primer resultado, sino en que el
mismo control se repite sin que nadie tenga que acordarse de ejecutarlo.

## Enlaces

- Repositorio: https://github.com/UPT-FAING-EPIS/si784-2026-ii-si784-2026-ii-examen-u1-dejameingresar
- OWASP Dependency-Check: https://owasp.org/www-project-dependency-check/
- Base de datos OWASP: https://github.com/dependency-check/DependencyCheck