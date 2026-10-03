// Carga la sesion en el navegador de pruebas: registra un usuario, inicia
// sesion con el token obtenido y guarda el estado en localStorage antes de
// abrir el panel. Se ejecuta con node.
const BASE = process.env.API ?? 'http://localhost:5080';

async function main() {
  const correo = `demo.${Date.now()}@taskflow.pe`;

  const registro = await fetch(`${BASE}/auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ name: 'Ana Quispe', email: correo, password: 'ClaveSegura123' })
  });
  if (!registro.ok) throw new Error(`registro: ${registro.status} ${await registro.text()}`);

  const login = await fetch(`${BASE}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: correo, password: 'ClaveSegura123' })
  });
  const sesion = await login.json();
  if (!sesion.token) throw new Error(`login: ${JSON.stringify(sesion)}`);

  // datos de ejemplo para que el panel no se vea vacio
  const auth = { Authorization: `Bearer ${sesion.token}`, 'Content-Type': 'application/json' };

  const proyectos = [
    { name: 'Portal de clientes', description: 'Rediseño integral del portal de atencion al cliente.', startDate: '2026-09-01', endDate: '2026-12-15', ownerId: sesion.user.id },
    { name: 'App movil interna', description: 'Aplicacion movil para el equipo de campo.', startDate: '2026-10-01', endDate: '2027-03-30', ownerId: sesion.user.id }
  ];
  const creados = [];
  for (const p of proyectos) {
    const r = await fetch(`${BASE}/projects`, { method: 'POST', headers: auth, body: JSON.stringify(p) });
    creados.push(await r.json());
  }

  const tareas = [
    { title: 'Maqueta del portal', description: 'Wireframes de las pantallas principales.', projectId: creados[0].id, assigneeId: sesion.user.id, dueDate: '2026-10-20' },
    { title: 'Revisar requisitos con el cliente', description: 'Sesion de validacion del alcance.', projectId: creados[0].id, assigneeId: sesion.user.id, dueDate: '2026-10-05' },
    { title: 'Configurar el pipeline de despliegue', description: 'Automatizar la construccion y publicacion.', projectId: creados[1].id, assigneeId: sesion.user.id, dueDate: '2026-11-10' }
  ];
  for (const t of tareas) {
    await fetch(`${BASE}/tasks`, { method: 'POST', headers: auth, body: JSON.stringify(t) });
  }

  // avanza una de las actividades para que se vean los estados
  const lista = await (await fetch(`${BASE}/tasks?projectId=${creados[0].id}`, { headers: auth })).json();
  if (lista[0]) {
    await fetch(`${BASE}/tasks/${lista[0].id}`, {
      method: 'PUT', headers: auth,
      body: JSON.stringify({ title: lista[0].title, description: lista[0].description, assigneeId: sesion.user.id, status: 'Completada', progress: 100, dueDate: '2026-10-20' })
    });
    await fetch(`${BASE}/tasks/${lista[0].id}/comments`, {
      method: 'POST', headers: auth,
      body: JSON.stringify({ body: 'Aprobada por el equipo de diseno.', authorId: sesion.user.id })
    });
  }
  if (lista[1]) {
    await fetch(`${BASE}/tasks/${lista[1].id}`, {
      method: 'PUT', headers: auth,
      body: JSON.stringify({ title: lista[1].title, description: lista[1].description, assigneeId: sesion.user.id, status: 'EnProgreso', progress: 45, dueDate: '2026-10-05' })
    });
  }

  console.log(JSON.stringify({
    token: sesion.token,
    usuario: sesion.user,
    localStorage: {
      taskflow_token: sesion.token,
      taskflow_usuario: JSON.stringify(sesion.user)
    }
  }));
}

main().catch((e) => {
  console.error('ERROR:', e.message);
  process.exit(1);
});