# Escaneo de vulnerabilidades · TaskFlow

Material de la tarea de Calidad y Pruebas de Software: artículos sobre
análisis de vulnerabilidades con herramientas distintas a las usadas en los
laboratorios, más el guion del video.

## Qué hay aquí

| Archivo | Qué es |
|---|---|
| `articulo/01-dependency-check.md` | Artículo sobre OWASP Dependency-Check |
| `articulo/02-gosec.md` | Artículo sobre gosec |
| `guion-video.md` | Guion del video de máximo 5 minutos |
| `informe-gosec-vulnerable.json` | Hallazgos de gosec sobre la app insegura |
| `banco-vulnerable/` | App Go deliberadamente insegura, para demostrar gosec |
| `Dockerfile.dependency-check` | Imagen para ejecutar Dependency-Check |

## Herramientas y por qué estas

Los laboratorios ya usaron SonarCloud, Semgrep, Snyk y tfsec. Estas dos
cubren terreno distinto:

| Herramienta | Qué analiza | Fuente |
|---|---|---|
| OWASP Dependency-Check | Vulnerabilidades conocidas (CVE) en dependencias | Base de datos de la OWASP Foundation |
| gosec | Patrones peligrosos en el código, con reglas CWE | securego/gosec |

## Resultados obtenidos

### TaskFlow (código real)

| Escaneo | Resultado |
|---|---|
| gosec sobre `TaskFlow.Api` | **0 hallazgos** |
| Dependency-Check sobre el grafo de dependencias | **0 vulnerabilidades conocidas** |
| `dotnet list package --vulnerable` | **0 paquetes vulnerables** |

### App deliberadamente insegura

Para comprobar que las herramientas detectan de verdad, `banco-vulnerable/`
contiene los fallos comunes de código Go:

```
[HIGH  ] G101   L23     Credencial escrita en el código
[HIGH  ] G101   L26-28  Clave privada RSA embebida
[MEDIUM] G114   L97     ListenAndServe sin timeouts
[MEDIUM] G204   L37     Subproceso con variable
[MEDIUM] G304   L32     Inclusión de archivo vía variable
[MEDIUM] G401   L57     Primitiva criptográfica débil
[MEDIUM] G401   L58     Primitiva criptográfica débil
[MEDIUM] G501   L8      Import bloqueado crypto/md5
[MEDIUM] G505   L9      Import bloqueado crypto/sha1
[MEDIUM] G710   L90     Redirección abierta por análisis de taint
```

**10 hallazgos.** G710 es el interesante: no busca un patrón, sigue el valor
de `nombre` desde la petición hasta la redirección.

## Reproducir los escaneos

### gosec

```bash
curl -sSfL https://raw.githubusercontent.com/securego/gosec/master/install.sh | sh

# sobre el codigo real
./bin/gosec -no-fail ./TaskFlow.Api/...

# sobre la app insegura
cd banco-vulnerable && ../bin/gosec -no-fail ./...
```

**Nota:** gosec necesita el toolchain de Go instalado. Sin él reporta
`Files: 0` y termina sin errores, lo que parece un escaneo limpio cuando en
realidad no analysó nada.

### OWASP Dependency-Check

```bash
java -jar dependency-check.zip \
  --project "TaskFlow API" \
  --scan TaskFlow.Api \
  --out informes/dependencias \
  --format HTML --format SARIF \
  --failOnCVSS 7
```

La primera ejecución descarga la base de datos de la OWASP. Las siguientes
usan la copia en caché.

## Automatización

`.github/workflows/security.yml` ejecuta los tres escaneos:

- en cada `push` a `main` y en cada pull request
- y una vez por semana (`cron`), porque la base de avisos cambia a diario y
  una vulnerabilidad puede publicarse después del último push

Dependency-Check corta el pipeline en CVSS 7 y sube el resultado en SARIF, que
GitHub muestra anotado en el pull request.

## Advertencia sobre `banco-vulnerable/`

Esa carpeta contiene código inseguro **a propósito**, para la demostración del
artículo. No desplegar, no exponer, no usar con datos reales. El pipeline de
integración continua la escanea, pero no la compila ni la publica.