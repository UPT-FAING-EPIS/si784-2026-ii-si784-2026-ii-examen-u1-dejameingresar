# Detectar vulnerabilidades en Go con gosec

En los laboratorios analizamos el código de una aplicación con SonarCloud,
Snyk y Semgrep. En este ejercicio el punto de mira es el mismo código fuente,
pero con una herramienta que no usamos en los labs: **gosec**, el analizador
estático que forma parte del ecosistema de Go y publica reglas mapeadas a CWE
de la OWASP.

Para demostrar que la herramienta funciona de verdad, la aplicamos a dos
códigos: el nuestro y uno escrito a propósito con fallos comunes.

<!-- more -->

## La idea detrás de gosec

gosec analiza el código buscando patrones que se sabe que son peligrosos:

- credenciales escritas en el fuente
- comandos del sistema construidos con variables
- rutas de archivo tomadas de entrada externa
- algoritmos de hash débiles
- consultas SQL por concatenación de cadenas

Cada regla tiene un identificador (`G###`) y un CWE asociado, así que el
resultado se puede mapear a lo que exige la norma, no quedarse en un "algo
está mal aquí".

Instalación en Linux:

```bash
curl -sSfL https://raw.githubusercontent.com/securego/gosec/master/install.sh | sh
```

## Resultado sobre TaskFlow: cero hallazgos

```bash
gosec -fmt=json -out=informe-gosec.json ./TaskFlow.Api
```

Salida:

```
Summary:
  Gosec  : 2.29.0
  Files  : 17
  Lines  : 1234
  Issues : 0
```

Cero problemas. Igual que con las dependencias, el resultado no fue casual:

- El acceso a la base de datos va por Entity Framework Core, sin SQL
  concatenado.
- Las contraseñas se hashean con PBKDF2, no con MD5 ni SHA1.
- No hay secretos escritos en el fuente: la clave JWT llega por variable de
  entorno.
- No se ejecutan comandos del sistema con datos del usuario.

## Y sobre una app insegura: 10 hallazgos

Un escaneo en verde no demuestra que la herramienta funcione. Para comprobarlo
escribimos una app a propósito, con los fallos que más se repiten en código Go.

```bash
gosec -no-fail -fmt=json -out=informe.json ./banco-vulnerable/...
```

Resultado real:

| Severidad | Regla | Línea | Hallazgo |
|---|---|---|---|
| HIGH | G101 | 23 | Credencial escrita en el código |
| HIGH | G101 | 26-28 | Clave privada RSA embebida |
| MEDIUM | G114 | 97 | `http.ListenAndServe` sin timeouts |
| MEDIUM | G204 | 37 | Subproceso con variable |
| MEDIUM | G304 | 32 | Inclusión de archivo vía variable |
| MEDIUM | G401 | 57 | Primitiva criptográfica débil |
| MEDIUM | G401 | 58 | Primitiva criptográfica débil |
| MEDIUM | G501 | 8 | Import bloqueado `crypto/md5` |
| MEDIUM | G505 | 9 | Import bloqueado `crypto/sha1` |
| MEDIUM | G710 | 90 | Redirección abierta por análisis de taint |

Dos detalles que merecen atención:

**G710 usa análisis de tainted data.** No busca un patrón: sigue el valor de
`nombre` desde `r.URL.Query().Get("nombre")` hasta la redirección, y advierte
porque ese dato acabaría en la URL de destino sin validar. Es la clase de
vulnerabilidad que un filtro por palabra clave no encuentra.

**G101 detectó la clave privada, no solo la contraseña.** El bloque de
claves PEM embebido es un error habitual al copiar un ejemplo de la
documentación. La regla lo reconoce por la forma del contenido.

## Cómo integrarlo en la automatización

En `.github/workflows/security.yml`:

```yaml
- name: gosec
  run: |
    gosec -fmt=json -out=gosec.json ./TaskFlow.Api/...
```

`-no-fail` permite que el escaneo termine y muestre el informe completo; si se
quiere que el pipeline falle con hallazgos de severidad alta:

```bash
gosec -no-fail ./... -severity=HIGH   # solo informa
gosec ./... -severity=HIGH             # falla el pipeline
```

Para evitar falsos positivos que desalienten en código correcto existe la
directiva `#nosec`, que documenta la excepción en la misma línea:

```go
ruta := validarComoInterno(ruta) // #nosec G304 -- ya sanitizada arriba
```

## Sobre los falsos negativos

gosec analiza el flujo de información dentro del archivo y entre archivos del
mismo paquete. Hay casos que se le escapan:

- Lógica de negocio que es incorrecta aunque no exista ningún patrón peligroso.
- Vulnerabilidades en dependencias, que son otro problema (y para eso está
  Dependency-Check).
- Código generado automáticamente que no se revisa.

Herramientas de este tipo son un control dentro de un conjunto. Ninguna
sustituye a revisar el diseño.

## Conclusión

El ejercicio dejó dos resultados que valen la pena por sí solos: el código
nuestro pasa limpio con dos herramientas que no se habían usado antes, y una
app deliberadamente insegura produce diez hallazgos correctamente clasificados
por CWE.

Lo que agrega esto a los laboratorios anteriores es la comparación. Ya
teníamos el análisis con SonarCloud, Semgrep y Snyk; ahora sumar dos
perspectivas distintas sobre el mismo código es lo que permite afirmar que
está limpio con una base algo más sólida que una sola herramienta.

## Enlaces

- Código: https://github.com/UPT-FAING-EPIS/si784-2026-ii-si784-2026-ii-examen-u1-dejameingresar
- Aplicación: https://taskflow-blue-alpha.vercel.app
- gosec: https://github.com/securego/gosec