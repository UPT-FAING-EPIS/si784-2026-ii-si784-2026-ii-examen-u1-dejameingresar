#!/usr/bin/env python3
"""Servidor auxiliar para las capturas de verificacion.

Entrega el frontend en el puerto 8090 y responde en el puerto 8091 con una
imagen que tarda unos segundos. Esa espera mantiene pendiente el evento
`load` de la pagina, de modo que la captura del navegador_headless caiga
despues de que la aplicacion haya terminado sus peticiones a la API.

No forma parte de la aplicacion: solo se usa para las capturas de prueba.
"""
import base64
import http.server
import socketserver
import threading
import time
import os

RAIZ = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'web')

# GIF transparente de 1x1, codificado en base64.
PIXEL_B64 = 'R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7'


class Frontend(http.server.SimpleHTTPRequestHandler):
    """Sirve los archivos estaticos del frontend."""

    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=os.path.abspath(RAIZ), **kwargs)

    def end_headers(self):
        # Sin cache, para que cada captura vea el estado mas reciente.
        self.send_header('Cache-Control', 'no-store')
        super().end_headers()

    def log_message(self, format, *args):
        pass


class Lento(http.server.BaseHTTPRequestHandler):
    """Responde con un pixel tras una espera, para delaying el evento load."""

    def do_GET(self):
        segundos = 6
        if '?ms=' in self.path:
            try:
                segundos = int(self.path.split('ms=')[1].split('&')[0]) / 1000
            except ValueError:
                segundos = 6
        time.sleep(segundos)
        cuerpo = base64.b64decode(PIXEL_B64)
        self.send_response(200)
        self.send_header('Content-Type', 'image/gif')
        self.send_header('Content-Length', str(len(cuerpo)))
        self.send_header('Access-Control-Allow-Origin', '*')
        self.end_headers()
        self.wfile.write(cuerpo)

    def log_message(self, format, *args):
        pass


def servir(puerto, handler):
    socketserver.TCPServer.allow_reuse_address = True
    with socketserver.TCPServer(('127.0.0.1', puerto), handler) as servidor:
        servidor.serve_forever()


if __name__ == '__main__':
    threading.Thread(target=servir, args=(8091, Lento), daemon=True).start()
    print('frontend en 8090 · servidor lento en 8091', flush=True)
    servir(8090, Frontend)