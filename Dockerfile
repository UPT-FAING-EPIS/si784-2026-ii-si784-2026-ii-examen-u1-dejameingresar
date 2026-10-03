# TaskFlow API · imagen de contenedor
#
# Compilacion en dos etapas: la primera produce el binario autocontenido,
# la segunda solo lo copia. La imagen final no incluye el SDK, por lo que
# ocupa bastante menos y reduce la superficie de ataque.
#
# Construir:  docker build -t taskflow-api:1.0.0 .
# Ejecutar:   docker run -p 8080:8080 -e ConnectionStrings__DefaultConnection="..." taskflow-api:1.0.0

# ── Etapa 1: compilacion ───────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS compilacion

WORKDIR /fuente

# Copiar primero los archivos de proyecto aprovecha la cache de capas:
# mientras el codigo cambie, no se vuelve a restaurar los paquetes.
COPY TaskFlow.Api/TaskFlow.Api.csproj TaskFlow.Api/
RUN dotnet restore TaskFlow.Api/TaskFlow.Api.csproj

COPY TaskFlow.Api/ TaskFlow.Api/
RUN dotnet publish TaskFlow.Api/TaskFlow.Api.csproj \
        --configuration Release \
        --output /publicacion \
        --no-restore

# ── Etapa 2: ejecucion minima ──────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS ejecucion

# El usuario nobody evita que el proceso corra con privilegios.
USER $APP_UID

WORKDIR /app
COPY --from=compilacion /publicacion .

# Puerto interno del contenedor.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080



ENTRYPOINT ["dotnet", "TaskFlow.Api.dll"]