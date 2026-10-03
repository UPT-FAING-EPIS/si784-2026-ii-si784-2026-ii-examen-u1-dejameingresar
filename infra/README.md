# Infraestructura · TaskFlow

Terraform para el despliegue en Azure.

## Recursos que crea

| Recurso | Para que sirve |
|---|---|
| `azurerm_resource_group` | Agrupa todos los recursos del proyecto. |
| `azurerm_postgresql_flexible_server` | PostgreSQL 16, alta disponibilidad por zona. |
| `azurerm_postgresql_flexible_server_database` | Base `taskflow`. |
| `azurerm_postgresql_flexible_server_firewall_rule` | Solo `AllowAzureServices`: el puerto no esta abierto a internet. |
| `azurerm_container_registry` | Registro donde se publica la imagen del contenedor. |
| `azurerm_service_plan` | Plan B1 de Linux. |
| `azurerm_app_service` | Servicio donde corre la API, con TLS 1.2 y `always_on`. |
| `azurerm_app_service_slot` | Entorno de staging. |
| `random_password` | La contrasena de PostgreSQL se genera, no se escribe. |

## Uso

```bash
cp terraform.tfvars.example terraform.tfvars
cp backend.tf.example backend.tf
# completar ambos con los datos reales

terraform init
terraform validate
terraform plan
terraform apply
```

## Notas de seguridad aplicadas

- La contrasena de PostgreSQL se genera con `random_password`, nunca en el codigo.
- `admin_enabled = false` en el registro: la autenticacion es por token.
- `https_only` y `min_tls_version = "1.2"` en el servicio.
- El estado de Terraform va en un backend remoto, no en el repositorio.
- El firewall de PostgreSQL solo admite servicios de Azure.
