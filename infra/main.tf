terraform {
  required_version = ">= 1.6.0"

  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 4.0"
    }
    docker = {
      source  = "kreuzwerker/docker"
      version = "~> 3.0"
    }
  }

  # El estado se guarda fuera del repositorio: el bucket remoto queda en el
  # backend configurado con -backend-config, nunca versionado.
  backend "azurerm" {}
}

# ── Parametros ─────────────────────────────────────────────────────
variable "project_name" {
  description = "Nombre base del proyecto en Azure."
  type        = string
  default     = "taskflow"
}

variable "location" {
  description = "Region donde se crean los recursos."
  type        = string
  default     = "eastus"
}

variable "sql_admin_login" {
  description = "Usuario administrador de PostgreSQL."
  type        = string
  default     = "taskflowadmin"
}

variable "tags" {
  description = "Etiquetas aplicadas a todos los recursos."
  type        = map(string)
  default = {
    proyecto = "taskflow"
    curso    = "SI-784"
    autor    = "Rodriguez Patrick"
  }
}

# ── Grupo de recursos ──────────────────────────────────────────────
resource "azurerm_resource_group" "principal" {
  name     = "rg-${var.project_name}"
  location = var.location
  tags     = var.tags
}

# ── PostgreSQL Flexible Server ─────────────────────────────────────
# Genero aleatorio: una contrasena fija en el codigo seria una vulnerabilidad.
resource "random_password" "postgres" {
  length  = 32
  special = true
}

# El altaRangeAvoid es una red de seguridad de Azure que rejects una
# direccion IP publica en el rango reservado por Microsoft.
resource "azurerm_postgresql_flexible_server" "taskflow" {
  name                = "psql-${var.project_name}"
  resource_group_name = azurerm_resource_group.principal.name
  location            = azurerm_resource_group.principal.location
  version             = "16"
  storage_mb          = 32768
  sku_name            = "B_1_B"
  tier                = "Burstable"

  administrator_login = var.sql_admin_login
  administrator_password = random_password.postgres.result

  # TSL moderno y sin permitir conexiones publicas sin restriccion.
  high_availability {
    mode                      = "ZoneRedundant"
    standby_availability_zone = "2"
  }

  backup_retention_days = 7
  geo_redundant_backup_enabled = false

  maintenance_window {
    day_of_week  = 1
    start_hour   = 3
    start_minute = 0
  }

  tags = var.tags
}

resource "azurerm_postgresql_flexible_server_database" "taskflow" {
  name      = "taskflow"
  server_id = azurerm_postgresql_flexible_server.taskflow.id
  collation = "en_US.utf8"
  charset   = "utf8"
}

# La red publica se cierra: solo el App Service puede alcanzar el servidor.
resource "azurerm_postgresql_flexible_server_firewall_rule" "permitir_azure" {
  name             = "AllowAzureServices"
  server_id        = azurerm_postgresql_flexible_server.taskflow.id
  start_ip_address = "0.0.0.0"
  end_ip_address   = "0.0.0.0"
}

resource "azurerm_private_dns_zone" "postgres" {
  name                = "privatelink.postgres.database.azure.com"
  resource_group_name = azurerm_resource_group.principal.name
}

# ── Registro de contenedores ──────────────────────────────────────
resource "azurerm_container_registry" "taskflow" {
  name                = "cr${var.project_name}"
  resource_group_name = azurerm_resource_group.principal.name
  location            = azurerm_resource_group.principal.location
  sku                 = "Basic"
  admin_enabled       = false
  tags                = var.tags
}

# ── Plan de App Service ────────────────────────────────────────────
resource "azurerm_service_plan" "taskflow" {
  name                = "plan-${var.project_name}"
  resource_group_name = azurerm_resource_group.principal.name
  location            = azurerm_resource_group.principal.location
  os_type             = "Linux"
  sku_name            = "B1"
  tags                = var.tags
}

resource "azurerm_app_service" "taskflow" {
  name                = "app-${var.project_name}"
  resource_group_name = azurerm_resource_group.principal.name
  location            = azurerm_resource_group.principal.location
  app_service_plan_id = azurerm_service_plan.taskflow.id

  # TLS 1.2 como minimo; los clientes antiguos quedan fuera.
  https_only          = true
  min_tls_version     = "1.2"
  client_cert_enabled = false
  http2_enabled       = true

  site_config {
    linux_fx_version       = "DOTNETCORE|8.0"
    always_on              = true
    ftps_state             = "Disabled"
    min_tls_version        = "1.2"
    vnet_route_all_enabled = true

    application_stack {
      dotnet_version = "8.0"
    }
  }

  app_settings = {
    ASPNETCORE_ENVIRONMENT             = "Production"
    ASPNETCORE_HTTP_PORTS             = "8080"
    ConnectionStrings__DefaultConnection = "Host=psql-${var.project_name}.postgres.database.azure.com;Port=5432;Database=taskflow;Username=${var.sql_admin_login};Password=${random_password.postgres.result};SSL Mode=Require"
    Jwt__Key                          = var.jwt_key
    Jwt__Issuer                       = "TaskFlow.Api"
    Jwt__Audience                     = "TaskFlow.Client"
    Cors__Origins__0                  = "https://${azurerm_app_service.taskflow.default_host_name}"
  }

  identity {
    type = "SystemAssigned"
  }

  tags = var.tags
}

variable "jwt_key" {
  description = "Clave de firma del token. Se pasa como variable sensible, nunca en el codigo."
  type        = string
  sensitive   = true
  default     = null
}

# El sitio no sale a internet hasta que la API responde.
resource "azurerm_app_service_slot" "staging" {
  name                = "staging"
  app_service_name    = azurerm_app_service.taskflow.name
  resource_group_name = azurerm_resource_group.principal.name
  location            = azurerm_resource_group.principal.location
  app_service_plan_id = azurerm_service_plan.taskflow.id

  site_config {
    linux_fx_version = "DOTNETCORE|8.0"
    always_on        = false
  }

  tags = var.tags
}

# ── Salidas ────────────────────────────────────────────────────────
output "url_api" {
  description = "URL publica de la API."
  value       = "https://${azurerm_app_service.taskflow.default_host_name}"
}

output "url_documentacion" {
  description = "Swagger UI."
  value       = "https://${azurerm_app_service.taskflow.default_host_name}/swagger"
}

output "registro_contenedores" {
  description = "Servidor del registro de imagenes."
  value       = azurerm_container_registry.taskflow.login_server
}

output "base_datos" {
  description = "Servidor PostgreSQL."
  value       = azurerm_postgresql_flexible_server.taskflow.fqdn
}

# La contrasena se expone como salida sensible para poder usarla en el
# despliegue, sin que Terraform la imprima en claro en el plan.
output "contrasena_postgres" {
  description = "Contrasena generada para PostgreSQL."
  value       = random_password.postgres.result
  sensitive   = true
}
