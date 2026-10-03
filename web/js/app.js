/* TaskFlow · logica de la interfaz
   Controla el estado de la sesion, las cuatro vistas del panel y los
   formularios. No usa framework: el estado vive en el objeto Estado y cada
   vista se vuelve a pintar desde los datos que devuelve la API. */

const Estado = {
  vista: 'proyectos',
  proyectos: [],
  tareas: [],
  usuarios: [],
  proyectoEnEdicion: null,
  tareaEnEdicion: null
};

/* ── Utilidades de presentacion ─────────────────────────────────── */

/** Escapa el texto antes de insertarlo en el HTML. */
function esc(valor) {
  const div = document.createElement('div');
  div.textContent = valor ?? '';
  return div.innerHTML;
}

/** Formatea una fecha ISO como dd/mm/aaaa. */
function fecha(iso) {
  if (!iso) return 'sin fecha';
  const d = new Date(iso);
  return d.toLocaleDateString('es-PE', { day: '2-digit', month: '2-digit', year: 'numeric' });
}

/** Devuelve la clase CSS segun el estado de la actividad. */
function claseEstado(estado) {
  return {
    Pendiente: 'pendiente',
    EnProgreso: 'enprogreso',
    Completada: 'completada',
    Bloqueada: 'bloqueada'
  }[estado] ?? 'pendiente';
}

/** Muestra un aviso flotante. */
function aviso(mensaje, tipo = '') {
  const caja = document.getElementById('aviso');
  caja.textContent = mensaje;
  caja.className = tipo;
  document.body.appendChild(caja);
  clearTimeout(caja._t);
  caja._t = setTimeout(() => caja.remove(), 3200);
}

/* ── Sesion ─────────────────────────────────────────────────────── */

/** Dibuja la sesion actual y decide que pantalla mostrar. */
async function pintarSesion() {
  const barra = document.getElementById('sesion');
  const usuario = API.usuarioGuardado();

  if (!API.token || !usuario) {
    barra.innerHTML = '';
    document.getElementById('acceso').classList.remove('oculto');
    document.getElementById('panel').classList.add('oculto');
    return;
  }

  barra.innerHTML = `
    <span>${esc(usuario.name)}</span>
    <span class="rol">${esc(usuario.role)}</span>
    <button class="boton boton--secundario boton--mini" id="btn-salir">Salir</button>`;

  document.getElementById('acceso').classList.add('oculto');
  document.getElementById('panel').classList.remove('oculto');

  document.getElementById('btn-salir').onclick = () => {
    API.cerrarSesion();
    pintarSesion();
    aviso('Sesion cerrada.');
  };

  // El panel de administracion solo se muestra a quien tiene el rol.
  document.getElementById('tab-admin').classList.toggle(
    'oculto', usuario.role !== 'Administrador');

  await cargarTodo();
}

/* ── Carga de datos ─────────────────────────────────────────────── */

async function cargarTodo() {
  try {
    Estado.proyectos = await API.proyectos();
    Estado.usuarios = [API.usuarioGuardado()].filter(Boolean);
  } catch (e) {
    aviso(e.message, 'error');
    if (e.message.includes('sesion')) API.cerrarSesion(), pintarSesion();
    return;
  }

  pintarProyectos();
  llenarSelectores();
  await cargarActividades();
}

/* ── Vista: proyectos ───────────────────────────────────────────── */

function pintarProyectos() {
  const caja = document.getElementById('lista-proyectos');

  if (!Estado.proyectos.length) {
    caja.innerHTML = `<div class="vacio">
      Todavia no hay proyectos. Crea el primero con el boton superior.</div>`;
    return;
  }

  caja.innerHTML = Estado.proyectos.map((p) => `
    <article class="proyecto">
      <h3>${esc(p.name)}</h3>
      <p class="proyecto__desc">${esc(p.description || 'Sin descripcion.')}</p>
      <div class="proyecto__meta">
        <span>Responsable: <strong>${esc(p.ownerName)}</strong></span>
        <span>Inicio: <strong>${fecha(p.startDate)}</strong></span>
        <span>Fin: <strong>${fecha(p.endDate)}</strong></span>
        <span>Actividades: <strong>${p.totalTasks}</strong></span>
      </div>
      <div class="barra-progreso"><span style="width:${p.progress}%"></span></div>
      <p class="proyecto__meta"><span>${p.completedTasks} de ${p.totalTasks} completadas (${p.progress}%)</span></p>
      <div class="proyecto__acciones">
        <button class="boton boton--secundario boton--mini" data-editar="${p.id}">Editar</button>
        <button class="boton boton--peligro boton--mini" data-borrar="${p.id}">Eliminar</button>
      </div>
    </article>`).join('');

  caja.querySelectorAll('[data-editar]').forEach((b) => {
    b.onclick = () => abrirProyecto(Number(b.dataset.editar));
  });
  caja.querySelectorAll('[data-borrar]').forEach((b) => {
    b.onclick = () => borrarProyecto(Number(b.dataset.borrar));
  });
}

async function abrirProyecto(id) {
  const proyecto = Estado.proyectos.find((p) => p.id === id);
  if (!proyecto) return;

  Estado.proyectoEnEdicion = id;
  document.getElementById('titulo-proyecto').textContent = 'Editar proyecto';
  document.getElementById('p-nombre').value = proyecto.name;
  document.getElementById('p-descripcion').value = proyecto.description ?? '';
  document.getElementById('p-inicio').value = (proyecto.startDate ?? '').slice(0, 10);
  document.getElementById('p-fin').value = (proyecto.endDate ?? '').slice(0, 10);
  document.getElementById('p-responsable').value = proyecto.ownerId;
  document.getElementById('dlg-proyecto').showModal();
}

async function borrarProyecto(id) {
  const proyecto = Estado.proyectos.find((p) => p.id === id);
  if (!confirm(`Eliminar el proyecto "${proyecto.name}"? Tambien se eliminaran sus actividades.`)) {
    return;
  }

  try {
    await API.borrarProyecto(id);
    aviso('Proyecto eliminado.', 'exito');
    await cargarTodo();
  } catch (e) {
    aviso(e.message, 'error');
  }
}

/* ── Vista: actividades ─────────────────────────────────────────── */

async function cargarActividades() {
  const proyectoId = document.getElementById('filtro-proyecto').value;
  try {
    Estado.tareas = await API.tareas(proyectoId ? { projectId: proyectoId } : {});
    pintarActividades();
  } catch (e) {
    aviso(e.message, 'error');
  }
}

function pintarActividades() {
  const caja = document.getElementById('lista-actividades');

  if (!Estado.tareas.length) {
    caja.innerHTML = `<div class="vacio">
      No hay actividades${Estado.proyectos.length ? '' : ' porque aun no hay proyectos'}.</div>`;
    return;
  }

  caja.innerHTML = Estado.tareas.map((t) => `
    <article class="actividad ${claseEstado(t.status).replace('enprogreso', 'en-progreso')}">
      <div class="actividad__marca"></div>
      <div>
        <h4>${esc(t.title)}</h4>
        <div class="actividad__meta">
          <span>Proyecto: <strong>${esc(t.projectName)}</strong></span>
          <span>Asignada a: <strong>${esc(t.assigneeName ?? 'sin asignar')}</strong></span>
          <span>Limite: <strong>${fecha(t.dueDate)}</strong></span>
          ${t.commentCount ? `<span>${t.commentCount} comentario(s)</span>` : ''}
        </div>
      </div>
      <div class="actividad__avance">
        <span class="etiqueta etiqueta--${claseEstado(t.status)}">${esc(t.status)}</span>
        <div class="barra-progreso"><span style="width:${t.progress}%"></span></div>
        <output>${t.progress}%</output>
        <button class="boton boton--secundario boton--mini" data-editar-t="${t.id}">Abrir</button>
      </div>
    </article>`).join('');

  caja.querySelectorAll('[data-editar-t]').forEach((b) => {
    b.onclick = () => abrirTarea(Number(b.dataset.editarT));
  });
}

function abrirTarea(id) {
  const tarea = Estado.tareas.find((t) => t.id === id);
  if (!tarea) return;

  Estado.tareaEnEdicion = tarea;
  document.getElementById('titulo-editar').textContent = tarea.title;
  document.getElementById('e-titulo').value = tarea.title;
  document.getElementById('e-estado').value = tarea.status;
  document.getElementById('e-avance').value = tarea.progress;
  document.getElementById('e-avance-valor').textContent = `${tarea.progress}%`;
  document.getElementById('t-comentario').value = '';
  document.getElementById('dlg-editar').showModal();
}

/* ── Vista: seguimiento ─────────────────────────────────────────── */

async function pintarSeguimiento() {
  const caja = document.getElementById('panel-seguimiento');
  const usuario = API.usuarioGuardado();
  caja.innerHTML = '<div class="vacio">Cargando…</div>';

  try {
    const d = await API.seguimiento(usuario.id);
    caja.innerHTML = `
      <div class="tarjetas">
        <div class="tarjeta-dato"><strong>${d.totalTareasAsignadas}</strong><span>Actividades asignadas</span></div>
        <div class="tarjeta-dato"><strong>${d.completadas}</strong><span>Completadas</span></div>
        <div class="tarjeta-dato"><strong>${d.enProgreso}</strong><span>En progreso</span></div>
        <div class="tarjeta-dato"><strong>${d.pendientes}</strong><span>Pendientes</span></div>
        <div class="tarjeta-dato"><strong>${d.bloqueadas}</strong><span>Bloqueadas</span></div>
        <div class="tarjeta-dato"><strong>${d.progresoPersonal}%</strong><span>Mi progreso</span></div>
      </div>
      <h3>Proyectos en los que participo</h3>
      ${d.proyectos.map((p) => `
        <div class="vacio-panel">
          <strong>${esc(p.name)}</strong>
          <span> · ${p.completedTasks}/${p.totalTasks} completadas (${p.progress}%)</span>
          <div class="barra-progreso"><span style="width:${p.progress}%"></span></div>
        </div>`).join('') || '<div class="vacio">Aun no participas en ningun proyecto.</div>'}`;
  } catch (e) {
    caja.innerHTML = `<div class="vacio">${esc(e.message)}</div>`;
  }
}

async function pintarAdmin() {
  const caja = document.getElementById('panel-admin');
  caja.innerHTML = '<div class="vacio">Cargando…</div>';

  try {
    const d = await API.administracion();
    caja.innerHTML = `
      <div class="tarjetas">
        <div class="tarjeta-dato"><strong>${d.totalUsuarios}</strong><span>Usuarios</span></div>
        <div class="tarjeta-dato"><strong>${d.totalProyectos}</strong><span>Proyectos</span></div>
        <div class="tarjeta-dato"><strong>${d.totalTareas}</strong><span>Actividades</span></div>
        <div class="tarjeta-dato"><strong>${d.completadas}</strong><span>Completadas</span></div>
        <div class="tarjeta-dato"><strong>${d.avanceGlobal}%</strong><span>Avance global</span></div>
      </div>`;
  } catch (e) {
    caja.innerHTML = `<div class="vacio">${esc(e.message)}</div>`;
  }
}

/* ── Selectores ─────────────────────────────────────────────────── */

/** Rellena los desplegables con los datos cargados. */
function llenarSelectores() {
  const opciones = Estado.proyectos
    .map((p) => `<option value="${p.id}">${esc(p.name)}</option>`)
    .join('');

  const filtro = document.getElementById('filtro-proyecto');
  const previo = filtro.value;
  filtro.innerHTML = `<option value="">Todos los proyectos</option>${opciones}`;
  filtro.value = previo;

  document.getElementById('t-proyecto').innerHTML =
    `<option value="">Selecciona un proyecto</option>${opciones}`;

  const usuarios = Estado.usuarios
    .map((u) => `<option value="${u.id}">${esc(u.name)}</option>`)
    .join('');

  document.getElementById('p-responsable').innerHTML =
    `<option value="">Selecciona un responsable</option>${usuarios}`;

  document.getElementById('t-asignado').innerHTML =
    `<option value="">Sin asignar</option>${usuarios}`;
}

/* ── Navegacion ─────────────────────────────────────────────────── */

async function cambiarVista(vista) {
  Estado.vista = vista;

  document.querySelectorAll('.panel__nav button').forEach((b) => {
    b.classList.toggle('activo', b.dataset.vista === vista);
  });

  for (const nombre of ['proyectos', 'actividades', 'seguimiento', 'admin']) {
    document.getElementById(`vista-${nombre}`)
      .classList.toggle('oculto', nombre !== vista);
  }

  if (vista === 'seguimiento') await pintarSeguimiento();
  if (vista === 'admin') await pintarAdmin();
}

/* ── Formularios ────────────────────────────────────────────────── */

function conectarFormularios() {
  // Acceso
  document.getElementById('form-acceso').onsubmit = async (e) => {
    e.preventDefault();
    if (!Validacion.acceso()) return;

    try {
      const r = await API.iniciar({
        email: document.getElementById('correo').value.trim(),
        password: document.getElementById('clave').value
      });
      API.iniciarSesion(r.token, r.user);
      e.target.reset();
      await pintarSesion();
      aviso(`Bienvenido, ${r.user.name}.`, 'exito');
    } catch (err) {
      document.getElementById('error-acceso').textContent = err.message;
    }
  };

  // Registro
  document.getElementById('form-registro').onsubmit = async (e) => {
    e.preventDefault();
    if (!Validacion.registro()) return;

    try {
      await API.registrar({
        name: document.getElementById('nombre').value.trim(),
        email: document.getElementById('correo-reg').value.trim(),
        password: document.getElementById('clave-reg').value
      });
      await API.iniciar({
        email: document.getElementById('correo-reg').value.trim(),
        password: document.getElementById('clave-reg').value
      }).then((r) => API.iniciarSesion(r.token, r.user));

      e.target.reset();
      await pintarSesion();
      aviso('Cuenta creada correctamente.', 'exito');
    } catch (err) {
      document.getElementById('error-correo-reg').textContent = err.message;
    }
  };

  // Proyecto
  document.getElementById('btn-nuevo-proyecto').onclick = () => {
    Estado.proyectoEnEdicion = null;
    document.getElementById('form-proyecto').reset();
    document.getElementById('titulo-proyecto').textContent = 'Nuevo proyecto';
    document.getElementById('dlg-proyecto').showModal();
  };

  document.getElementById('btn-cancelar-proyecto').onclick =
    () => document.getElementById('dlg-proyecto').close();

  document.getElementById('form-proyecto').onsubmit = async (e) => {
    e.preventDefault();
    const datos = Validacion.proyecto();
    if (!datos) return;

    try {
      if (Estado.proyectoEnEdicion) {
        await API.editarProyecto(Estado.proyectoEnEdicion, datos);
        aviso('Proyecto actualizado.', 'exito');
      } else {
        await API.crearProyecto(datos);
        aviso('Proyecto creado.', 'exito');
      }
      document.getElementById('dlg-proyecto').close();
      await cargarTodo();
    } catch (err) {
      aviso(err.message, 'error');
    }
  };

  // Actividad
  document.getElementById('btn-nueva-tarea').onclick = () => {
    document.getElementById('form-tarea').reset();
    document.getElementById('titulo-tarea').textContent = 'Nueva actividad';
    document.getElementById('dlg-tarea').showModal();
  };

  document.getElementById('btn-cancelar-tarea').onclick =
    () => document.getElementById('dlg-tarea').close();

  document.getElementById('form-tarea').onsubmit = async (e) => {
    e.preventDefault();
    const datos = Validacion.tarea();
    if (!datos) return;

    try {
      await API.crearTarea(datos);
      document.getElementById('dlg-tarea').close();
      aviso('Actividad creada.', 'exito');
      await cargarTodo();
    } catch (err) {
      aviso(err.message, 'error');
    }
  };

  // Edicion de actividad
  document.getElementById('btn-cancelar-editar').onclick =
    () => document.getElementById('dlg-editar').close();

  document.getElementById('e-avance').oninput = (e) => {
    document.getElementById('e-avance-valor').textContent = `${e.target.value}%`;
  };

  document.getElementById('form-editar').onsubmit = async (e) => {
    e.preventDefault();
    const datos = Validacion.edicion();
    if (!datos) return;

    const tarea = Estado.tareaEnEdicion;
    const comentario = document.getElementById('t-comentario').value.trim();

    try {
      await API.editarTarea(tarea.id, {
        title: datos.title,
        description: tarea.description,
        assigneeId: tarea.assigneeId,
        status: datos.status,
        progress: datos.progress,
        dueDate: (tarea.dueDate ?? '').slice(0, 10)
      });

      if (comentario) {
        await API.comentar(tarea.id, comentario, API.usuarioGuardado().id);
      }

      document.getElementById('dlg-editar').close();
      aviso('Actividad actualizada.', 'exito');
      await cargarActividades();
      if (Estado.vista === 'seguimiento') await pintarSeguimiento();
    } catch (err) {
      aviso(err.message, 'error');
    }
  };

  // Navegacion
  document.querySelectorAll('.panel__nav button').forEach((b) => {
    b.onclick = () => cambiarVista(b.dataset.vista);
  });

  document.getElementById('filtro-proyecto').onchange = cargarActividades;
}

/* ── Arranque ───────────────────────────────────────────────────── */

document.addEventListener('DOMContentLoaded', () => {
  conectarFormularios();
  pintarSesion();
});