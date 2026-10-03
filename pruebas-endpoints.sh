#!/usr/bin/env bash
# Prueba de extremo a extremo de la API de TaskFlow.
#
# Los identificadores se leen de las respuestas reales, de modo que el
# script funciona contra una base vacia o con datos previos. Requiere la API
# levantada en la direccion indicada (por defecto http://localhost:5080).
set -u
BASE="${1:-http://localhost:5080}"
OK=0
FALLA=0

res() { printf "  %-56s %s\n" "$1" "$2"; }

# peticion: imprime el codigo HTTP y deja el cuerpo en /tmp/tf-body.json
peticion() {
  local metodo="$1" url="$2" datos="${3:-}" token="${4:-}"
  local args=(-s -o /tmp/tf-body.json -w '%{http_code}' -X "$metodo" "$BASE$url"
              -H 'Content-Type: application/json')
  [ -n "$token" ] && args+=(-H "Authorization: Bearer $token")
  [ -n "$datos" ] && args+=(-d "$datos")
  curl "${args[@]}"
}

# espera: verifica el codigo esperado y cuenta el resultado
espera() {
  local nombre="$1" esperado="$2" code="$3"
  if [ "$code" = "$esperado" ]; then
    res "$nombre" "OK ($code)"; OK=$((OK+1))
  else
    res "$nombre" "FALLA (esperado $esperado, hubo $code)"; FALLA=$((FALLA+1))
    python3 -c "
import json
try:
    d = json.load(open('/tmp/tf-body.json'))
    print('     ' + json.dumps(d.get('errors', d), ensure_ascii=False)[:220])
except Exception:
    print('     (sin cuerpo legible)')
"
  fi
}

# crea un recurso y devuelve su Id leyendo la respuesta
crear() {
  local url="$1" datos="$2" token="$3"
  peticion POST "$url" "$datos" "$token" >/dev/null
  python3 -c "import json; print(json.load(open('/tmp/tf-body.json')).get('id',''))"
}

echo "══════════════════════════════════════════════════════════════════════"
echo " TaskFlow API · prueba de extremo a extremo"
echo "══════════════════════════════════════════════════════════════════════"
echo
echo "— Autenticacion —"

CORREO="ana.$(date +%s)@taskflow.pe"
code=$(peticion POST /auth/register \
  "{\"name\":\"Ana Quispe\",\"email\":\"$CORREO\",\"password\":\"ClaveSegura123\"}")
espera "POST /auth/register (crea usuario)" 201 "$code"
USUARIO=$(python3 -c "import json; print(json.load(open('/tmp/tf-body.json')).get('id',''))")

CORREO2="luis.$(date +%s)@taskflow.pe"
code=$(peticion POST /auth/register \
  "{\"name\":\"Luis Diaz\",\"email\":\"$CORREO2\",\"password\":\"ClaveSegura123\"}")
espera "POST /auth/register (segundo usuario)" 201 "$code"
USUARIO2=$(python3 -c "import json; print(json.load(open('/tmp/tf-body.json')).get('id',''))")

code=$(peticion POST /auth/register \
  "{\"name\":\"Duplicado\",\"email\":\"$CORREO\",\"password\":\"ClaveSegura123\"}")
espera "POST /auth/register (correo repetido)" 400 "$code"

code=$(peticion POST /auth/register \
  '{"name":"Sin correo valido","email":"no-es-correo","password":"123"}')
espera "POST /auth/register (datos invalidos)" 400 "$code"

code=$(peticion POST /auth/login \
  "{\"email\":\"$CORREO\",\"password\":\"ClaveSegura123\"}")
espera "POST /auth/login (devuelve token)" 200 "$code"
TOKEN=$(python3 -c "
import json
print(json.load(open('/tmp/tf-body.json')).get('token',''))")

code=$(peticion POST /auth/login \
  "{\"email\":\"$CORREO\",\"password\":\"ClaveEquivocada\"}")
espera "POST /auth/login (clave incorrecta)" 401 "$code"

[ -n "$TOKEN" ] && { res "El token JWT es utilizable" "OK"; OK=$((OK+1)); } \
               || { res "El token JWT es utilizable" "FALLA"; FALLA=$((FALLA+1)); }
echo
echo "— Control de acceso —"

code=$(peticion GET /projects)
espera "GET  /projects sin token" 401 "$code"
code=$(peticion POST /projects '{}')
espera "POST /projects sin token" 401 "$code"
code=$(peticion GET "/tasks?projectId=$USUARIO" '' "$TOKEN")
espera "GET  /tasks con token" 200 "$code"
code=$(peticion GET /dashboard/admin '' "$TOKEN")
espera "GET  /dashboard/admin sin rol admin" 403 "$code"
echo
echo "— Proyectos (5 endpoints del enunciado) —"

code=$(peticion POST /projects \
  "{\"name\":\"Portal de clientes\",\"description\":\"Rediseño del portal\",\"startDate\":\"2026-10-01\",\"endDate\":\"2026-12-31\",\"ownerId\":$USUARIO}" "$TOKEN")
espera "POST /projects" 201 "$code"
PROYECTO=$(python3 -c "import json; print(json.load(open('/tmp/tf-body.json')).get('id',''))")

code=$(peticion POST /projects \
  "{\"name\":\"Proyecto con fechas invalidas\",\"startDate\":\"2026-12-01\",\"endDate\":\"2026-01-01\",\"ownerId\":$USUARIO}" "$TOKEN")
espera "POST /projects (fecha fin anterior al inicio)" 400 "$code"

code=$(peticion POST /projects \
  "{\"name\":\"Sin responsable\",\"startDate\":\"2026-10-01\",\"ownerId\":999999}" "$TOKEN")
espera "POST /projects (responsable inexistente)" 400 "$code"

code=$(peticion POST /projects \
  '{"name":"","startDate":"2026-10-01","ownerId":1}' "$TOKEN")
espera "POST /projects (nombre vacio)" 400 "$code"

code=$(peticion GET /projects '' "$TOKEN")
espera "GET  /projects (listar)" 200 "$code"

code=$(peticion GET "/projects?ownerId=$USUARIO" '' "$TOKEN")
espera "GET  /projects (filtrar por responsable)" 200 "$code"

code=$(peticion GET "/projects/$PROYECTO" '' "$TOKEN")
espera "GET  /projects/{id} (detalle)" 200 "$code"

code=$(peticion GET /projects/999999 '' "$TOKEN")
espera "GET  /projects/{id} (inexistente)" 404 "$code"

code=$(peticion PUT "/projects/$PROYECTO" \
  "{\"name\":\"Portal de clientes v2\",\"description\":\"Alcance ampliado\",\"startDate\":\"2026-10-01\",\"endDate\":\"2027-01-15\",\"ownerId\":$USUARIO}" "$TOKEN")
espera "PUT  /projects/{id} (editar)" 200 "$code"

code=$(peticion PUT "/projects/$PROYECTO" \
  "{\"name\":\"Editado\",\"startDate\":\"2026-12-01\",\"endDate\":\"2026-01-01\",\"ownerId\":$USUARIO}" "$TOKEN")
espera "PUT  /projects/{id} (fecha invalida)" 400 "$code"

code=$(peticion PUT /projects/999999 \
  "{\"name\":\"No existe\",\"startDate\":\"2026-10-01\",\"ownerId\":$USUARIO}" "$TOKEN")
espera "PUT  /projects/{id} (inexistente)" 404 "$code"
echo
echo "— Actividades (4 endpoints del enunciado) —"

code=$(peticion POST /tasks \
  "{\"title\":\"Maqueta del portal\",\"description\":\"Wireframes de las pantallas\",\"projectId\":$PROYECTO,\"assigneeId\":$USUARIO2,\"dueDate\":\"2026-10-20\"}" "$TOKEN")
espera "POST /tasks" 201 "$code"
TAREA=$(python3 -c "import json; print(json.load(open('/tmp/tf-body.json')).get('id',''))")

code=$(peticion POST /tasks \
  "{\"title\":\"Sin proyecto\",\"projectId\":999999,\"dueDate\":\"2026-10-20\"}" "$TOKEN")
espera "POST /tasks (proyecto inexistente)" 400 "$code"

code=$(peticion POST /tasks \
  "{\"title\":\"\",\"projectId\":$PROYECTO,\"dueDate\":\"2026-10-20\"}" "$TOKEN")
espera "POST /tasks (titulo vacio)" 400 "$code"

code=$(peticion GET "/tasks?projectId=$PROYECTO" '' "$TOKEN")
espera "GET  /tasks?projectId={id}" 200 "$code"

code=$(peticion GET "/tasks?projectId=$PROYECTO&assigneeId=$USUARIO2" '' "$TOKEN")
espera "GET  /tasks (filtrar por asignado)" 200 "$code"

code=$(peticion PUT "/tasks/$TAREA" \
  "{\"title\":\"Maqueta del portal\",\"description\":\"Wireframes revisados\",\"assigneeId\":$USUARIO2,\"status\":\"Completada\",\"progress\":100,\"dueDate\":\"2026-10-20\"}" "$TOKEN")
espera "PUT  /tasks/{id} (marcar completada)" 200 "$code"

code=$(peticion PUT "/tasks/$TAREA" \
  "{\"title\":\"Maqueta\",\"status\":\"Completada\",\"progress\":50,\"dueDate\":\"2026-10-20\"}" "$TOKEN")
espera "PUT  /tasks/{id} (completada con avance < 100)" 400 "$code"

code=$(peticion PUT "/tasks/$TAREA" \
  "{\"title\":\"Maqueta\",\"status\":\"EnProgreso\",\"progress\":180,\"dueDate\":\"2026-10-20\"}" "$TOKEN")
espera "PUT  /tasks/{id} (avance fuera de rango)" 400 "$code"

code=$(peticion PUT "/tasks/$TAREA" \
  "{\"title\":\"Maqueta\",\"status\":\"EstadoInventado\",\"progress\":50,\"dueDate\":\"2026-10-20\"}" "$TOKEN")
espera "PUT  /tasks/{id} (estado inexistente)" 400 "$code"

code=$(peticion PUT /tasks/999999 \
  "{\"title\":\"No existe\",\"status\":\"Pendiente\",\"progress\":0,\"dueDate\":\"2026-10-20\"}" "$TOKEN")
espera "PUT  /tasks/{id} (inexistente)" 404 "$code"

code=$(peticion POST "/tasks/$TAREA/comments" \
  "{\"body\":\"Aprobada por el equipo\",\"authorId\":$USUARIO}" "$TOKEN")
espera "POST /tasks/{id}/comments" 201 "$code"

code=$(peticion POST "/tasks/$TAREA/comments" \
  "{\"body\":\"\",\"authorId\":$USUARIO}" "$TOKEN")
espera "POST /tasks/{id}/comments (cuerpo vacio)" 400 "$code"

code=$(peticion POST "/tasks/$TAREA/comments" \
  "{\"body\":\"Autor inexistente\",\"authorId\":999999}" "$TOKEN")
espera "POST /tasks/{id}/comments (autor inexistente)" 400 "$code"

code=$(peticion POST "/tasks/999999/comments" \
  "{\"body\":\"No existe la tarea\",\"authorId\":$USUARIO}" "$TOKEN")
espera "POST /tasks/{id}/comments (tarea inexistente)" 404 "$code"
echo
echo "— Panel de seguimiento —"

code=$(peticion GET "/dashboard/$USUARIO2" '' "$TOKEN")
espera "GET  /dashboard/{userId} (panel personal)" 200 "$code"
code=$(peticion GET "/dashboard/$USUARIO" '' "$TOKEN")
espera "GET  /dashboard/{userId} (panel sin tareas)" 200 "$code"
code=$(peticion GET /dashboard/999999 '' "$TOKEN")
espera "GET  /dashboard/{userId} (usuario inexistente)" 404 "$code"
echo
echo "— Eliminacion en cascada —"

TEMPORAL=$(crear /projects \
  "{\"name\":\"Temporal\",\"description\":\"Se elimina al final\",\"startDate\":\"2026-10-01\",\"ownerId\":$USUARIO}" "$TOKEN")
code=$(peticion DELETE "/projects/$TEMPORAL" '' "$TOKEN")
espera "DELETE /projects/{id}" 204 "$code"
code=$(peticion GET "/projects/$TEMPORAL" '' "$TOKEN")
espera "GET  /projects/{id} (tras eliminar)" 404 "$code"
code=$(peticion DELETE /projects/999999 '' "$TOKEN")
espera "DELETE /projects/{id} (inexistente)" 404 "$code"
echo
echo "══════════════════════════════════════════════════════════════════════"
printf " Resultado: %d correctos · %d fallidos\n" "$OK" "$FALLA"
echo "══════════════════════════════════════════════════════════════════════"
[ "$FALLA" = "0" ]