# ZITADEL generates the client ids and the API client secret, and none of them can be pinned,
# so they have to be read back. See "ZITADEL lokal" in README.md for how they reach the
# application.
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

output "api_client_secret" {
  description = "Auth__ConfidentialClientSecret: secret of the API application, used for token introspection."
  value       = zitadel_application_api.api.client_secret
  sensitive   = true
}
