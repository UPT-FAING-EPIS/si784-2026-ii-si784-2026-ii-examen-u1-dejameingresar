# Publicar los artículos

Los dos artículos están escritos y revisados. Publicarlos requiere tu cuenta
en la plataforma: no puedo crear una ni usar la tuya sin que me autorices.

## Opción recomendada: Dev.to

Dev.to acepta Markdown directamente y el artículo queda visible sin pagar.

### 1. Genera tu API key

https://dev.to/settings/extensions → *Dev Community API Keys* → genera una.

### 2. Publica

```bash
export DEVTO_API_KEY="tu-clave-aqui"
cd ~/examen-u1/seguridad

python3 - <<'PY'
import os, json, urllib.request, pathlib

CLAVE = os.environ["DEVTO_API_KEY"]

ARTICULOS = [
    {
        "archivo": "articulo/01-dependency-check.md",
        "titulo": "Auditar dependencias en TaskFlow con OWASP Dependency-Check",
        "tags": ["security", "owasp", "dependencycheck", "dotnet", "cicd"],
        "desc": "OWASP Dependency-Check no encontro vulnerabilidades conocidas en "
                "las dependencias de TaskFlow. Esto es lo que se puede y lo que no "
                "se puede afirmar con ese resultado.",
        "series": "Analisis de vulnerabilidades",
        "canonical": "https://github.com/UPT-FAING-EPIS/si784-2026-ii-si784-2026-ii-examen-u1-dejameingresar",
    },
    {
        "archivo": "articulo/02-gosec.md",
        "titulo": "Detectar vulnerabilidades en Go con gosec",
        "tags": ["security", "gosec", "golang", "sast", "staticanalysis"],
        "desc": "gosec detecto 10 vulnerabilidades en una app Go deliberadamente "
                "insegura, incluida una redireccion abierta encontrada por analisis "
                "de taint, y cero hallazgos en el codigo de TaskFlow.",
        "series": "Analisis de vulnerabilidades",
        "canonical": "https://github.com/UPT-FAING-EPIS/si784-2026-ii-si784-2026-ii-examen-u1-dejameingresar",
    },
]

for art in ARTICULOS:
    ruta = pathlib.Path(art["archivo"])
    texto = ruta.read_text(encoding="utf-8")

    # Dev.to pone el titulo en su campo; se quita el encabezado markdown.
    lineas = texto.split("\n")
    if lineas and lineas[0].startswith("# "):
        texto = "\n".join(lineas[1:]).lstrip()

    payload = {
        "title": art["titulo"],
        "published": True,
        "body_markdown": texto,
        "tags": art["tags"],
        "description": art["desc"],
        "series": art["series"],
        "canonical_url": art["canonical"],
    }

    req = urllib.request.Request(
        "https://dev.to/api/articles",
        data=json.dumps(payload).encode(),
        method="POST",
        headers={"Content-Type": "application/json",
                 "Authorization": f"Bearer {CLAVE}"},
    )

    with urllib.request.urlopen(req, timeout=60) as r:
        d = json.load(r)

    print(f"PUBLICADO: {d['title']}")
    print(f"  URL: {d['url']}")
    print()
PY
```

## Opción alternativa: HashNode

HashNode necesita un token Personal Access Key de
https://app.hashnode.com/settings/developer and tiene la misma estructura de
API, pero **exige la GraphQL API**: la documentación oficial ya no ofrece
endpoint REST para publicar.

## Opción manual

Si ninguna de las dos te sirve, ambos archivos se abren en cualquier editor
Markdown y se pegan en el editor web de la plataforma:

- `articulo/01-dependency-check.md` — 782 palabras
- `articulo/02-gosec.md` — 787 palabras

## Datos que ya incluyen los artículos

No hace falta añadir nada más, los dos tienen:

- el enlace al repositorio público
- el enlace a la aplicación desplegada en Vercel
- los comandos exactos con los que se corrieron los escaneos
- los resultados reales, incluyendo los 10 hallazgos de gosec
- una sección de limitaciones, que es lo que distingue un análisis serio