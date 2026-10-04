# Guion del video · máximo 5 minutos

Grabación de pantalla con voz en off. El texto es lo que se dice; las acciones
son lo que se ve.

---

## 0:00 — Apertura

**Voz:**
> En los laboratorios anteriores analizamos el código de una aplicación con
> SonarCloud, Semgrep y Snyk. En este ejercicio cambiamos el punto de mira:
> analizamos las dependencias y el código con dos herramientas que no habíamos
> usado, para comprobar que el resultado limpio no dependía de una sola
> herramienta.

**Pantalla:** el repositorio de TaskFlow en GitHub.

---

## 0:25 — El problema

**Voz:**
> El código que escribimos se apoya en cientos de librerías que no escribió
> nuestro equipo. Una dependencia vulnerable no se ve leyendo el repositorio:
> se ve consultando qué versiones publicadas tienen avisos de seguridad.

**Pantalla:** `TaskFlow.Api.csproj` con los paquetes.

---

## 0:50 — OWASP Dependency-Check sobre TaskFlow

**Voz:**
> OWASP Dependency-Check cruza los archivos de proyecto contra la base de
> datos de avisos que mantiene la propia fundación.

**Pantalla:** terminal ejecutando el escaneo.

```bash
dependency-check --project "TaskFlow API" --scan TaskFlow.Api \
                 --out informes/dependencias --format HTML
```

**Voz:**
> El resultado: cero vulnerabilidades conocidas en el grafo completo de
> dependencias. Son veinte paquetes, contando los transitivos.

**Pantalla:** el informe HTML con el resumen en cero.

---

## 1:30 — gosec sobre TaskFlow

**Voz:**
> gosec es el analizador estático estándar del ecosistema de Go. Cada regla
> tiene un identificador y un CWE asociado.

**Pantalla:** terminal.

```bash
gosec -no-fail ./TaskFlow.Api/...
```

**Voz:**
> gosec está escrito en Go, así que sobre una API en C# no tiene nada que
> decir. No es que el código esté limpio: es que la herramienta no aplica.
> Por eso el caso de estudio es una aplicación Go.

**Pantalla:** el resumen con `Files: 0`.

---

## 2:05 — Demostrar que la herramienta funciona

**Voz:**
> Un escaneo en verde no demuestra nada. Para comprobar que gosec detecta de
> verdad, escribimos una aplicación a propósito con los fallos más comunes.

**Pantalla:** `banco-vulnerable/main.go`,recorriendo el código.

**Voz:**
> Y ejecutamos el mismo comando:

```bash
gosec -no-fail ./banco-vulnerable/...
```

**Pantalla:** la salida con los 10 hallazgos.

**Voz:**
> Diecisiete hallazgos: cinco de severidad alta, once medios y uno bajo.
> Entre los altos hay una credencial en el código, una clave privada RSA
> embebida, y tres encontrados por análisis de taint.

**Pantalla:** resaltar las filas HIGH de la tabla.

**Voz:**
> Esto último es lo interesante. G204 dice "subproceso con variable", que es
> cierto pero genérico. G702, sobre la misma línea, dice inyección de
> comandos, porque sabe que el valor viene de la petición sin validar.

**Pantalla:** recorriendo la tabla, señalando G204 y G702 en la misma línea.

---

## 3:00 — Lo que la herramienta no ve

**Voz:**
> Es importante decir lo que estas herramientas no detectan. gosec no ve
> lógica de negocio incorrecta si no hay ningún patrón peligroso. Y un paquete
> sin CVE no significa que su uso sea correcto.

**Pantalla:** el código del proyecto, con calma.

---

## 3:30 — La automatización

**Voz:**
> Lo que agrega valor no es el primer resultado, sino que el control se repite
> sin que nadie tenga que acordarse.

**Pantalla:** `.github/workflows/security.yml`.

**Voz:**
> Tres trabajos: Dependency-Check con corte en CVSS 7, gosec, y tfsec para la
> infraestructura. El primero además sube el resultado en formato SARIF, que
> GitHub muestra anotado en el pull request.

**Pantalla:** la pestaña Security del repositorio.

---

## 4:10 — Cierre

**Voz:**
> Resultado: TaskFlow pasa limpio en código, en dependencias y en
> infraestructura, con cuatro herramientas distintas. Y una aplicación
> deliberadamente insegura produce los hallazgos esperados.

> El valor de esto no es decir que el proyecto está libre de
> vulnerabilidades. Es que ahora el mismo control se ejecuta en cada push, y
> alguien más puede reproducirlo.

**Pantalla:** los enlaces del repositorio y del artículo.

---

## Notas de producción

- **Duración estimada:** 4 min 40 s. Margen bajo el límite de 5.
- **Sin audio de fondo** en los segmentos de terminal: distraer.
- **Subtítulos** en el segmento de la tabla de hallazgos, porque se lee rápido.
- **Plano:** la captura de terminal en 1080p y escalada al 80 %, para que el
  texto se lea sin ampliar.