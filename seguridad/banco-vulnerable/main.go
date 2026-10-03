package main

// Aplicacion de ejemplo INSEGURA a proposito. Existe unicamente para
// demostrar que gosec detecta las clases de vulnerabilidad que el codigo de
// TaskFlow ya evita. No desplegar, no exponer, no usar con datos reales.

import (
	"crypto/md5"
	"crypto/sha1"
	"fmt"
	"html/template"
	"io/ioutil"
	"log"
	"net/http"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
)

// G101: el nombre de la constante sugiere que es una credencial, y su valor
// esta escrito en el codigo.
const apiPassword = "s3cr3t-de-ejemplo-no-usar"

// G101: clave privada embebida en el fuente.
const privateKeyPem = `-----BEGIN RSA PRIVATE KEY-----
MIIEowIBAAKCAQEA0Z3VS5JJcds3xfn/ygWyF0qJdhk8FxKx1mY3dqPPXvJc
-----END RSA PRIVATE KEY-----`

// G304: la ruta viene de la peticion y se abre sin validar.
func leerArchivo(ruta string) ([]byte, error) {
	return ioutil.ReadFile(ruta)
}

// G204: se ejecuta un comando del sistema con entrada del usuario.
func convertir(mime string) (string, error) {
	salida, err := exec.Command("file", "-b", mime).Output()
	return string(salida), err
}

// G304: ruta construida a mano y escrita sin restringir donde va.
func guardarInforme(usuario, nombre string) error {
	destino := filepath.Join("/tmp/descargas", usuario, nombre)
	return ioutil.WriteFile(destino, []byte("informe"), 0600)
}

// G201/G202: la consulta se arma por concatenacion, y ademas el error que se
// registra incluye la consulta completa.
func buscarUsuario(nombre string) (string, error) {
	query := "SELECT * FROM usuarios WHERE nombre = '" + nombre + "'"
	log.Printf("fallo la consulta: %s", query)
	return query, nil
}

// G401/G501: MD5 y SHA1 para derivar contrasenas.
func huella(password string) string {
	md := md5.Sum([]byte(password))
	sha := sha1.Sum([]byte(password))
	return fmt.Sprintf("%x%x", md, sha)
}

// G101 de nuevo: se lee un secreto del entorno sin comprobarlo.
func tokenDeSesion(r *http.Request) string {
	return os.Getenv("SESSION_SECRET") + r.Header.Get("X-Token")
}

// G701: la plantilla HTML se arma concatenando, y el valor del usuario no se
// escapa -> XSS.
func paginaDeSaludo(nombre string) (string, error) {
	var pagina strings.Builder
	pagina.WriteString("<html><body>")
	pagina.WriteString("<h1>Hola " + nombre + "</h1>")
	pagina.WriteString("</body></html>")

	t, err := template.New("pagina").Parse(pagina.String())
	if err != nil {
		return "", err
	}
	return t.Name(), nil
}

func principal(w http.ResponseWriter, r *http.Request) {
	nombre := r.URL.Query().Get("nombre")

	if _, err := paginaDeSaludo(nombre); err != nil {
		log.Println(err)
	}

	// G107: la redireccion se construye con datos del usuario.
	http.Redirect(w, r, "/perfil?u="+nombre, http.StatusFound)
}

func main() {
	http.HandleFunc("/", principal)
	log.Println(huella(apiPassword))
	log.Println(tokenDeSesion(nil))
	log.Fatal(http.ListenAndServe(":8080", nil))
}
