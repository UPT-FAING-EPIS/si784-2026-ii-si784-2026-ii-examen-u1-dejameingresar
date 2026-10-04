// TaskFlow Demo · aplicacion de demostracion INSEGURA a proposito.
//
// Existe unicamente como caso de estudio para el analisis de vulnerabilidades
// con gosec. Contiene deliberadamente las clases de fallo que gosec detecta:
// credenciales en el codigo, inyeccion de comandos, recorrido de rutas,
// criptografia debil e inyeccion SQL.
//
// NO desplegar. NO exponer. NO usar con datos reales.
// Es un ejemplo didactico, no una aplicacion de produccion.

package main

import (
	"crypto/md5"
	"crypto/sha1"
	"database/sql"
	"fmt"
	"html/template"
	"io/ioutil"
	"log"
	"net/http"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"time"
)

// G101: credencial escrita directamente en el codigo.
const apiPassword = "s3cr3t-de-ejemplo-no-usar"

// G101: clave privada embebida en el fuente.
const privateKeyPem = `-----BEGIN RSA PRIVATE KEY-----
MIIEowIBAAKCAQEA0Z3VS5JJcds3xfn/ygWyF0qJdhk8FxKx1mY3dqPPXvJc
-----END RSA PRIVATE KEY-----`

// G204: se ejecuta un comando del sistema con un valor que viene del usuario.
func convertir(mime string) (string, error) {
	salida, err := exec.Command("file", "-b", mime).Output()
	return string(salida), err
}

// G304: se abre una ruta de archivo construida con datos de la peticion.
func leerAdjunto(ruta string) ([]byte, error) {
	return ioutil.ReadFile(ruta)
}

// G304: escritura en una ruta construida sin restringir el destino.
func guardarInforme(usuario, nombre string) error {
	destino := filepath.Join("/tmp/descargas", usuario, nombre)
	return ioutil.WriteFile(destino, []byte("informe generado"), 0600)
}

// G201/G202: la consulta se arma por concatenacion de cadenas.
func buscarUsuario(db *sql.DB, nombre string) error {
	query := "SELECT id, correo FROM usuarios WHERE nombre = '" + nombre + "'"
	rows, err := db.Query(query)
	if err != nil {
		// G202: el error registra la consulta completa, con lo que el usuario
		// envio, y eso acaba en los logs de la aplicacion.
		log.Printf("fallo la consulta: %s", query)
		return err
	}
	defer rows.Close()
	return rows.Err()
}

// G401/G501/G505: MD5 y SHA1 para derivar contrasenas.
func huella(password string) string {
	md := md5.Sum([]byte(password))
	sha := sha1.Sum([]byte(password))
	return fmt.Sprintf("%x%x", md, sha)
}

// G303: se devuelve la entrada del usuario en la cabecera Location.
func redirigir(w http.ResponseWriter, r *http.Request, destino string) {
	http.Redirect(w, r, destino, http.StatusSeeOther)
}

// G701/G703: la plantilla HTML se arma concatenando y el valor del usuario no
// se escapa, con lo que es posible inyectar etiquetas.
func paginaDeSaludo(nombre string) (*template.Template, error) {
	var pagina strings.Builder
	pagina.WriteString("<html><body>")
	pagina.WriteString("<h1>Hola " + nombre + "</h1>")
	pagina.WriteString("</body></html>")
	return template.New("pagina").Parse(pagina.String())
}

// G114: ListenAndServe sin timeouts, con lo que un cliente lento puede agotar
// los recursos del servidor.
func principal() {
	mux := http.NewServeMux()

	mux.HandleFunc("/saludo", func(w http.ResponseWriter, r *http.Request) {
		nombre := r.URL.Query().Get("nombre")

		if pagina, err := paginaDeSaludo(nombre); err == nil {
			_ = pagina.Execute(w, nil)
		}

		redirigir(w, r, "/perfil?u="+nombre)
	})

	mux.HandleFunc("/descarga", func(w http.ResponseWriter, r *http.Request) {
		if err := guardarInforme(r.URL.Query().Get("u"), "informe.txt"); err != nil {
			http.Error(w, "error", http.StatusInternalServerError)
		}
	})

	mux.HandleFunc("/archivo", func(w http.ResponseWriter, r *http.Request) {
		datos, err := leerAdjunto(r.URL.Query().Get("ruta"))
		if err != nil {
			http.Error(w, "no existe", http.StatusNotFound)
			return
		}
		w.Write(datos)
	})

	// G101: secreto leido del entorno y concatenado sin validar.
	mux.HandleFunc("/token", func(w http.ResponseWriter, r *http.Request) {
		fmt.Fprintf(w, "%s", os.Getenv("SESSION_SECRET")+r.Header.Get("X-Token"))
	})

	mux.HandleFunc("/tipo", func(w http.ResponseWriter, r *http.Request) {
		salida, err := convertir(r.URL.Query().Get("mime"))
		if err != nil {
			http.Error(w, "error", http.StatusInternalServerError)
			return
		}
		fmt.Fprint(w, salida)
	})

	mux.HandleFunc("/hash", func(w http.ResponseWriter, r *http.Request) {
		fmt.Fprint(w, huella(r.URL.Query().Get("clave")))
	})

	servidor := &http.Server{
		Addr:    ":8080",
		Handler: mux,
		// G114: sin ReadHeaderTimeout ni WriteTimeout, un cliente lento
		// mantiene las conexiones abiertas indefinidamente.
	}

	// G114: no se usan los timeouts del servidor.
	log.Fatal(servidor.ListenAndServe())
}

// referencia para que el analisis vea el uso de time y no marque el import.
var _ = time.Second
