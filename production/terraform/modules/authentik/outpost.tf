# Create a dedicated Proxy outpost for applications
resource "authentik_outpost" "proxy_outpost" {
  name               = "terraform-managed-outpost"
  type               = "proxy"
  service_connection = authentik_service_connection_kubernetes.local_cluster.id

  protocol_providers = [
    for app_key in keys(var.applications) :
    authentik_provider_proxy.proxy_providers[app_key].id
  ]

  config = jsonencode({
    log_level                      = "info"
    docker_labels                  = null
    authentik_host                 = "http://authentik-server:80"
    docker_network                 = null
    container_image                = null
    docker_map_ports               = true
    refresh_interval               = "minutes=5"
    kubernetes_replicas            = 1
    kubernetes_namespace           = "authentik"
    authentik_host_browser         = "https://sso.your.domain"
    object_naming_template         = "ak-outpost-%(name)s"
    authentik_host_insecure        = true
    kubernetes_json_patches        = null
    kubernetes_service_type        = "ClusterIP"
    kubernetes_image_pull_secrets  = []
    kubernetes_ingress_class_name  = "nginx"
    kubernetes_disabled_components = []
    kubernetes_ingress_annotations = {
      "cert-manager.io/cluster-issuer" = "ca-issuer"
    }
    kubernetes_ingress_secret_name   = "terraform-outpost-tls" # pragma: allowlist secret
    kubernetes_httproute_annotations = {}
    kubernetes_httproute_parent_refs = []
  })
}

# Kubernetes service connection for the outpost
resource "authentik_service_connection_kubernetes" "local_cluster" {
  name  = "terraform-k8s-connection"
  local = true
}
