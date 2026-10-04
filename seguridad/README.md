# Escaneo de vulnerabilidades · Actividad Grupal 1

Material de SAST (Static Application Security Testing): dos artículos sobre
análisis de vulnerabilidades con herramientas que **no** se usaron en los
laboratorios, más el guion del video.

## Herramientas y por qué estas

Los laboratorios usaron SonarCloud, Semgrep, Snyk y tfsec. Estas dos cubren
terreno que ninguna de ellas tocó:

| Herramienta | Qué analiza | Fuente |
|---|---|---|
| OWASP Dependency-Check | Vulnerabilidades conocidas (CVE) en dependencias | Base de la OWASP Foundation |
| gosec | Patrones peligrosos en el código, con reglas CWE | securego/gosec |

## Resultados, todos ejecutados

### gosec sobre la app de demostración

17 hallazgos: 5 HIGH, 11 MEDIUM, 1 LOW. Informe en `informe-gosec.json`.

| Regla | Qué detecta |
|---|---|
| G101 | Credencial y clave privada RSA en el código |
| G702 | Inyección de comandos (taint) |
| G703 | Recorrido de rutas (taint) |
| G705 | XSS (taint) |
| G710 | Redirección abierta (taint) |
| G112 | Slowloris por falta de timeouts |
| G202 | SQL por concatenación de cadenas |
| G204 | Subproceso con variable |
| G304 | Inclusión de archivo vía variable |
| G401/G501/G505 | MD5 y SHA1 |
| G104 | Errores sin manejar |

### Dependencias

`dotnet list package --include-transitive --vulnerable` sobre el proyecto de
ejemplo: **0 vulnerabilidades** en 20 paquetes transitivos.

## Advertencia sobre `banco-vulnerable/`

Contiene código inseguro **a propósito**, escrito para la demostración del
artículo. No desplegar, no exponer, no usar con datos reales.

## Reproducir

### gosec

```bash
# necesita el compilador de Go
curl -sSfL https://raw.githubusercontent.com/securego/gosec/master/install.sh | sh

cd banco-vulnerable
gosec -no-fail -fmt=json -out=informe.json ./...
```

**Advertencia:** sin el compilador de Go, gosec termina sin errores y reporta
`Files: 0`. Eso no es un escaneo limpio: es un escaneo que no ocurrió.

### OWASP Dependency-Check

```bash
dependency-check --project "TaskFlow API" --scan TaskFlow.Api \
                 --out informes/dependencias \
                 --format HTML --format SARIF --failOnCVSS 7
```

La primera ejecución descarga la base de la NVD (más de 400.000 registros).
Sin API key tarda horas; con una key gratuita baja a minutos:
https://nvd.nist.gov/developers/request-an-api-key

## Automatización

`.github/workflows/security.yml` corre los tres escaneos en cada `push`, en cada
pull request y una vez por semana, porque la base de avisos cambia a diario.
Dependency-Check corta el pipeline en CVSS 7 y sube el resultado en SARIF, que
GitHub muestra anotado en el pull request.

## Publicar

`PUBLICAR.md` tiene el comando exacto para subir ambos artículos a Dev.to con
una API key. Los artículos ya incluyen los enlaces al repositorio y a la
aplicación, que es lo que pide la consigna.