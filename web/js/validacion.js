/* TaskFlow · validacion en el cliente
   Las mismas reglas que aplica el servidor, comprobadas antes de enviar.
   No sustituyen la validacion del backend: evitan un viaje de ida y vuelta
   para avisar al usuario de un error que ya se conoce. */

const Validacion = {

  /** Marca un campo como erroneo y escribe el mensaje. */
  marcar(campo, mensaje) {
    const entrada = document.getElementById(campo);
    const error = document.getElementById(`error-${campo}`);
    if (entrada) entrada.classList.add('invalido');
    if (error) error.textContent = mensaje ?? '';
    return !mensaje;
  },

  /** Limpia el estado de error de un campo. */
  limpiar(campo) {
    return this.marcar(campo, '');
  },

  /** Limpia todos los errores de un formulario. */
  limpiarTodo(ids) {
    ids.forEach((id) => this.limpiar(id));
  },

  /**
   * Valida un correo.
   * @param {string} valor Texto introducido.
   * @returns {boolean} Si el correo tiene un formato utilizable.
   */
  correo(valor) {
    return /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(valor.trim());
  },

  /**
   * Valida una contrasena.
   * @param {string} valor Texto introducido.
   * @param {number} minimo Longitud exigida.
   * @returns {boolean} Si cumple la longitud y tiene variety de caracteres.
   */
  clave(valor, minimo = 8) {
    return valor.length >= minimo && /[a-zA-Z]/.test(valor) && /[0-9]/.test(valor);
  },

  /**
   * Valida los datos del formulario de acceso.
   * @returns {boolean} Si todo el formulario es valido.
   */
  acceso() {
    this.limpiarTodo(['correo', 'clave']);
    const correo = document.getElementById('correo').value;
    const clave = document.getElementById('clave').value;
    let ok = true;

    if (!correo.trim()) {
      ok = this.marcar('correo', 'Escribe tu correo electronico.') && ok;
    } else if (!this.correo(correo)) {
      ok = this.marcar('correo', 'El correo no tiene un formato valido.') && ok;
    }

    if (!clave) {
      ok = this.marcar('clave', 'Escribe tu contrasena.') && ok;
    }

    return ok;
  },

  /**
   * Valida los datos del formulario de registro.
   * @returns {boolean} Si todo el formulario es valido.
   */
  registro() {
    this.limpiarTodo(['nombre', 'correo-reg', 'clave-reg']);
    const nombre = document.getElementById('nombre').value;
    const correo = document.getElementById('correo-reg').value;
    const clave = document.getElementById('clave-reg').value;
    let ok = true;

    if (nombre.trim().length < 3) {
      ok = this.marcar('nombre', 'El nombre debe tener al menos 3 caracteres.') && ok;
    }

    if (!this.correo(correo)) {
      ok = this.marcar('correo-reg', 'El correo no tiene un formato valido.') && ok;
    }

    if (!this.clave(clave)) {
      ok = this.marcar('clave-reg',
        'Minimo 8 caracteres, con al menos una letra y un numero.') && ok;
    }

    return ok;
  },

  /**
   * Valida el formulario de proyecto.
   * @returns {object|null} Los datos limpios, o null si algo falla.
   */
  proyecto() {
    this.limpiarTodo(['p-nombre', 'p-descripcion', 'p-inicio', 'p-fin', 'p-responsable']);

    const nombre = document.getElementById('p-nombre').value.trim();
    const descripcion = document.getElementById('p-descripcion').value.trim();
    const inicio = document.getElementById('p-inicio').value;
    const fin = document.getElementById('p-fin').value;
    const responsable = document.getElementById('p-responsable').value;
    let ok = true;

    if (nombre.length < 3) {
      ok = this.marcar('p-nombre', 'El nombre debe tener al menos 3 caracteres.') && ok;
    }

    if (descripcion.length > 1000) {
      ok = this.marcar('p-descripcion', 'La descripcion admite 1000 caracteres.') && ok;
    }

    if (!inicio) {
      ok = this.marcar('p-inicio', 'Indica la fecha de inicio.') && ok;
    }

    if (fin && inicio && fin < inicio) {
      ok = this.marcar('p-fin', 'La fecha de fin no puede ser anterior al inicio.') && ok;
    }

    if (!responsable) {
      ok = this.marcar('p-responsable', 'Selecciona un responsable.') && ok;
    }

    if (!ok) {
      return null;
    }

    return {
      name: nombre,
      description: descripcion,
      startDate: inicio,
      endDate: fin || null,
      ownerId: Number(responsable)
    };
  },

  /**
   * Valida el formulario de actividad.
   * @returns {object|null} Los datos limpios, o null si algo falla.
   */
  tarea() {
    this.limpiarTodo(['t-titulo', 't-descripcion', 't-proyecto', 't-vencimiento']);

    const titulo = document.getElementById('t-titulo').value.trim();
    const descripcion = document.getElementById('t-descripcion').value.trim();
    const proyecto = document.getElementById('t-proyecto').value;
    const asignado = document.getElementById('t-asignado').value;
    const vencimiento = document.getElementById('t-vencimiento').value;
    let ok = true;

    if (titulo.length < 3) {
      ok = this.marcar('t-titulo', 'El titulo debe tener al menos 3 caracteres.') && ok;
    }

    if (descripcion.length > 2000) {
      ok = this.marcar('t-descripcion', 'La descripcion admite 2000 caracteres.') && ok;
    }

    if (!proyecto) {
      ok = this.marcar('t-proyecto', 'Selecciona un proyecto.') && ok;
    }

    if (!vencimiento) {
      ok = this.marcar('t-vencimiento', 'Indica la fecha limite.') && ok;
    }

    if (!ok) {
      return null;
    }

    return {
      title: titulo,
      description: descripcion,
      projectId: Number(proyecto),
      assigneeId: asignado ? Number(asignado) : null,
      dueDate: vencimiento
    };
  },

  /**
   * Valida el formulario de edicion de una actividad.
   * @returns {object|null} Los datos limpios, o null si algo falla.
   */
  edicion() {
    this.limpiarTodo(['e-titulo', 'e-estado', 'e-avance']);

    const titulo = document.getElementById('e-titulo').value.trim();
    const estado = document.getElementById('e-estado').value;
    const avance = Number(document.getElementById('e-avance').value);
    let ok = true;

    if (titulo.length < 3) {
      ok = this.marcar('e-titulo', 'El titulo debe tener al menos 3 caracteres.') && ok;
    }

    if (estado === 'Completada' && avance !== 100) {
      ok = this.marcar('e-avance',
        'Una actividad completada debe tener el avance al 100 por ciento.') && ok;
    }

    if (!ok) {
      return null;
    }

    return { title: titulo, status: estado, progress: avance };
  }
};