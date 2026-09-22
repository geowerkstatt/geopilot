# ZITADEL generates the client id and it cannot be pinned (SetNewClientID always calls
# idGenerator.Next()). Unlike Keycloak, where "geopilot-client" is a fixed string, the value
# has to be read back and handed to the application. zitadel-provision writes both into an
# env file that the geopilot service loads.
output "oidc_client_id" {
  description = "Auth__PublicClientId: client id of the frontend application."
  value       = zitadel_application_oidc.frontend.client_id
  sensitive   = true # the provider marks it sensitive; it is an identifier, not a secret
}

output "api_client_id" {
  description = "Auth__Audience: client id of the API application, which lands in the aud claim."
  value       = zitadel_application_api.api.client_id
  sensitive   = true
}
