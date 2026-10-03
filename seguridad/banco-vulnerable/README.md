# App de ejemplo insegura

Existe **solo para la demostración del artículo**. Contiene a propósito las
clases de vulnerabilidad que gosec detecta:

| Regla | Qué demuestra |
|---|---|
| G101 | Credencial o clave privada en el código |
| G204 | Comando del sistema con entrada del usuario |
| G304 | Ruta de archivo tomada de la petición |
| G401/G501 | MD5 y SHA1 para contraseñas |
| G201/G202 | SQL por concatenación de cadenas |

No es la aplicación del proyecto. TaskFlow no tiene estos defectos: para
comparar, ejecuta gosec sobre `TaskFlow.Api` y verás **cero hallazgos**.

No desplegar. No exponer. No usar con datos reales.
