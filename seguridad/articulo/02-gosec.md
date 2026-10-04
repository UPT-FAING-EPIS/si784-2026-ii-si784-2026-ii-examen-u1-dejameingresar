# Detectar vulnerabilidades en Go con gosec

En los laboratorios analizamos el código de una aplicación con SonarCloud,
Snyk y Semgrep. En este ejercicio el punto de mira es el mismo código fuente,
pero con una herramienta que no usamos en los labs: **gosec**, el analizador
estático estándar del ecosistema de Go, que publica reglas mapeadas a CWE de
la OWASP.

Para demostrar que la herramienta funciona de verdad, la aplicamos a una
aplicación escrita a propósito con los fallos más comunes. El resultado son
**17 hallazgos**, cinco de ellos de severidad alta.

<!-- more -->

## La idea detrás de gosec

gosec analiza el código buscando patrones que se sabe que son peligrosos:

- credenciales o claves privadas escritas en el fuente
- comandos del sistema construidos con variables
- rutas de archivo tomadas de entrada externa
- algoritmos de hash débiles
- consultas SQL por concatenación de cadenas
- redirecciones y plantillas construidas con datos del usuario

Cada regla tiene un identificador (`G###`), una severidad y un CWE asociado,
así que el resultado se puede mapear a lo que exige la norma y no quedarse en
un "algo está mal aquí".

```bash
curl -sSfL https://raw.githubusercontent.com/securego/gosec/master/install.sh | sh
gosec -no-fail -fmt=json -out=informe.json ./...
```

Un detalle que puede arruinar el resultado: gosec necesita el compilador de Go
instalado. Sin él termina sin errores y reporta `Files: 0`, lo que parece un
escaneo limpio cuando en realidad no analysó nada.

## La aplicación de prueba

Es un servidor web pequeño, en un solo archivo, con seis rutas. Cada una contiene
un fallo deliberado:

| Ruta | Fallo introducido |
|---|---|
| `/saludo` | plantilla HTML sin escapar y redirección con datos del usuario |
| `/descarga` | escritura en ruta construida sin restringir |
| `/archivo` | lectura de archivo con ruta de la petición |
| `/token` | secreto concatenado sin validar |
| `/tipo` | comando del sistema con valor del usuario |
| `/hash` | MD5 y SHA1 para derivar contraseñas |

No está publicada en ningún servicio y no debe usarse con datos reales. Es un
caso de estudio, no una aplicación de producción.

## Los 17 hallazgos

Resultado real de `gosec -no-fail -fmt=json`:

| Severidad | Regla | Línea | Hallazgo |
|---|---|---|---|
| HIGH | G101 | 30 | Credencial escrita en el código |
| HIGH | G101 | 33-35 | Clave privada RSA embebida |
| HIGH | G702 | 39 | Inyección de comandos por análisis de taint |
| HIGH | G703 | 45 | Recorrido de rutas por análisis de taint |
| HIGH | G703 | 51 | Recorrido de rutas por análisis de taint |
| MEDIUM | G112 | 138-143 | Slowloris: falta `ReadHeaderTimeout` |
| MEDIUM | G202 | 56 | Concatenación de cadenas en SQL |
| MEDIUM | G204 | 39 | Subproceso lanzado con variable |
| MEDIUM | G304 | 45 | Inclusión de archivo vía variable |
| MEDIUM | G401 | 70-71 | Primitiva criptográfica débil |
| MEDIUM | G401 | 70 | Primitiva criptográfica débil |
| MEDIUM | G501 | 14 | Import bloqueado `crypto/md5` |
| MEDIUM | G505 | 15 | Import bloqueado `crypto/sha1` |
| MEDIUM | G705 | 117 | XSS por análisis de taint |
| MEDIUM | G705 | 122 | XSS por análisis de taint |
| MEDIUM | G710 | 77 | Redirección abierta por análisis de taint |
| LOW | G104 | 117 | Errores sin manejar |

**Resumen:** 5 HIGH, 11 MEDIUM, 1 LOW. 17 en total.

## Lo que las reglas de taint aportan

Cuatro de los hallazgos (G702, G703, G705, G710) no vienen de buscar una palabra
clave: gosec sigue el valor desde que entra en la función hasta donde se usa.

En `/tipo`, el valor de `mime` llega de `r.URL.Query().Get("mime")` y acaba como
argumento de `exec.Command`. G204 lo marca como "subproceso con variable", que es
correcto pero genérico. **G702 lo marca como inyección de comandos**, porque
sabe que ese valor no se ha validado. La misma línea, dos reglas, y la segunda
es la que dice qué hacer.

Lo mismo con G703: `G304` dice "inclusión de archivo vía variable", y **G703 dice
recorrido de rutas**, siguiendo que `ruta` viene de la petición sin comprobar.

Es la diferencia entre un filtro por patrón y un análisis de flujo de datos.

## Integrarlo en la automatización

```yaml
- name: gosec
  run: |
    gosec -no-fail -fmt=json -out=gosec.json ./...
```

`-no-fail` deja que el escaneo termine y muestre el informe completo. Para que
el pipeline falle:

```bash
gosec ./... -severity=HIGH           # falla si hay hallazgos altos
gosec -no-fail ./... -severity=HIGH  # solo informa
```

Para un error justificado hay una directiva en la misma línea:

```go
ruta := validarComoInterno(ruta) // #nosec G304 -- ya sanitizada arriba
```

La convención es no abusar de ella: cada `#nosec` es una excepción que alguien
tiene que leer y justificar.

## Sobre los falsos negativos

gosec analiza el flujo de información dentro del archivo y entre archivos del
mismo paquete. Hay casos que se le escapan:

- **Lógica de negocio incorrecta** sin ningún patrón peligroso.
- **Vulnerabilidades en dependencias**, que es otro problema: para eso está
  OWASP Dependency-Check.
- **Código generado automáticamente**, que normalmente no se revisa.
- **Otro lenguaje**: gosec solo analiza Go.

El informe incluye el CWE de cada hallazgo, que permite priorizar por lo que la
norma exige y no por la severidad que le asigne la herramienta.

## Conclusión

El ejercicio deja un resultado concreto: 17 hallazgos correctamente
clasificados, cinco de ellos de severidad alta, sobre una aplicación escrita
para contenerlos.

Lo que demuestra el análisis de taint es lo más aprovechable. Una herramienta
que dice "aquí hay un `exec.Command` con variable" obliga a revisar; una que
dice "el valor llega desde la petición sin validar" dice cuál es el problema.

Ningún análisis estático sustituye a revisar el diseño, y un resultado limpio
no significa que el código sea correcto: significa que esta herramienta no
encontró nada. La diferencia entre las dos afirmaciones es la que hace que un
informe sirva para algo.

## Enlaces

- Repositorio con el código y el informe: https://github.com/UPT-FAING-EPIS/si784-2026-ii-si784-2026-ii-examen-u1-dejameingresar
- gosec: https://github.com/securego/gosec
- CWE de la OWASP: https://cwe.mitre.org/
