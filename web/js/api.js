/* TaskFlow · cliente de la API
   Envuelve fetch con el token JWT y normaliza los errores del servidor en
   un unico formato para que la interfaz no tenga que interpretar respuestas. */

const API = {
  base: window.TASKFLOW_API ?? 'http://localhost:5080',
  token: localStorage.getItem('taskflow_token') ?? null,
  usuario: null,

  /** Guarda la sesion iniciada. */
  iniciarSesion(token, usuario) {
    this.token = token;
    this.usuario = usuario;
    localStorage.setItem('taskflow_token', token);
    localStorage.setItem('taskflow_usuario', JSON.stringify(usuario));
  },

  /** Borra la sesion local. */
  cerrarSesion() {
    this.token = null;
    this.usuario = null;
    localStorage.removeItem('taskflow_token');
    localStorage.removeItem('taskflow_usuario');
  },

  /** Recupera el usuario guardado en la sesion anterior. */
  usuarioGuardado() {
    try {
      return JSON.parse(localStorage.getItem('taskflow_usuario') ?? 'null');
    } catch {
      return null;
    }
  },

  /**
   * Ejecuta una peticion contra la API.
   * @param {string} ruta Ruta relativa, por ejemplo '/projects'.
   * @param {object} opciones Metodo, cuerpo y parametros de consulta.
   * @returns {Promise<any>} La respuesta ya convertida a JSON.
   * @throws {Error} Con el mensaje del servidor cuando la peticion falla.
   */
  async pedir(ruta, { metodo = 'GET', cuerpo = null, consulta = {} } = {}) {
    const url = new URL(this.base + ruta);

    for (const [clave, valor] of Object.entries(consulta)) {
      if (valor !== null && valor !== undefined && valor !== '') {
        url.searchParams.set(clave, valor);
      }
    }

    const cabeceras = { 'Content-Type': 'application/json' };
    if (this.token) {
      cabeceras.Authorization = `Bearer ${this.token}`;
    }

    const respuesta = await fetch(url, {
      method: metodo,
      headers: cabeceras,
      body: cuerpo ? JSON.stringify(cuerpo) : null
    });

    if (respuesta.status === 204) {
      return null;
    }

    let datos = null;
    try {
      datos = await respuesta.json();
    } catch {
      datos = null;
    }

    if (!respuesta.ok) {
      throw new Error(this.explicar(respuesta.status, datos));
    }

    return datos;
  },

  /** Traduce la respuesta de error a un mensaje entendible. */
  explicar(estado, datos) {
    if (estado === 401) {
      return 'La sesion expiro o las credenciales no son correctas.';
    }
    if (estado === 403) {
      return 'No tienes permiso para realizar esta accion.';
    }
    if (datos?.errors) {
      return Object.values(datos.errors)
        .flat()
        .join(' ');
    }
    if (datos?.title) {
      return datos.title;
    }
    return `Error ${estado} al comunicarse con el servidor.`;
  },

  /* ── Autenticacion ───────────────────────────────────────────── */

  registrar(datos) {
    return this.pedir('/auth/register', { metodo: 'POST', cuerpo: datos });
  },

  iniciar(datos) {
    return this.pedir('/auth/login', { metodo: 'POST', cuerpo: datos });
  },

  /* ── Proyectos ──────────────────────────────────────────────── */

  proyectos(filtros = {}) {
    return this.pedir('/projects', { consulta: filtros });
  },

  proyecto(id) {
    return this.pedir(`/projects/${id}`);
  },

  crearProyecto(datos) {
    return this.pedir('/projects', { metodo: 'POST', cuerpo: datos });
  },

  editarProyecto(id, datos) {
    return this.pedir(`/projects/${id}`, { metodo: 'PUT', cuerpo: datos });
  },

  borrarProyecto(id) {
    return this.pedir(`/projects/${id}`, { metodo: 'DELETE' });
  },

  /* ── Actividades ────────────────────────────────────────────── */

  tareas(filtros = {}) {
    return this.pedir('/tasks', { consulta: filtros });
  },

  crearTarea(datos) {
    return this.pedir('/tasks', { metodo: 'POST', cuerpo: datos });
  },

  editarTarea(id, datos) {
    return this.pedir(`/tasks/${id}`, { metodo: 'PUT', cuerpo: datos });
  },

  comentar(id, cuerpoTexto, autorId) {
    return this.pedir(`/tasks/${id}/comments`, {
      metodo: 'POST',
      cuerpo: { body: cuerpoTexto, authorId: autorId }
    });
  },

  /* ── Paneles ────────────────────────────────────────────────── */

  seguimiento(usuarioId) {
    return this.pedir(`/dashboard/${usuarioId}`);
  },

  administracion() {
    return this.pedir('/dashboard/admin');
  }
};