# Default configuration for the local ZITADEL instance: project, both applications and the
# development users. This is the counterpart to config/realms/keycloak-geopilot.json, applied
# by the zitadel-provision service. See "ZITADEL lokal" in README.md for the background.
#
# Deliberately self-contained: it does NOT reuse the modules in the geopilot-hosting
# repository, because those are referenced by relative path and geopilot must stay
# provisionable from a single checkout. The resource types and the attribute names are the
# same ones those modules wrap, so a change like app_type/auth_method_type is expressed here
# exactly as it would be in production.

provider "zitadel" {
  domain   = "localhost"
  port     = "4012"
  insecure = "true" # plain http locally, see docker-compose.zitadel.yml

  # Key of the "terraform" machine user with IAM_OWNER, minted by ZITADEL on first start and
  # shared through the bootstrap volume, so no credential is created by hand or committed.
  # A path, not the value, so the credential never enters the terraform state. Production
  # authenticates the same way, via jwt_profile_json.
  jwt_profile_file = "/zitadel/bootstrap/admin-key.json"
}

# No organization is created here on purpose: everything goes into the FirstInstance
# organization. The console shows the organization of the signed-in admin, so users in a
# separate organization would appear to be missing until one switches.
#
# Its id is resolved rather than omitted: the documented org_id default (the organization of
# the authenticated service account) is not implemented by zitadel_human_user, which sends an
# empty OrganizationId that ZITADEL rejects.
data "zitadel_orgs" "default" {
  name        = "geopilot" # ZITADEL_FIRSTINSTANCE_ORG_NAME in docker-compose.zitadel.yml
  name_method = "TEXT_QUERY_METHOD_EQUALS"
  state       = "ORG_STATE_ACTIVE"
}

locals {
  # one() returns null for an empty collection, so the count is checked separately in the
  # precondition below. Applying with a null org_id would fail deep inside ZITADEL instead.
  org_id = one(data.zitadel_orgs.default.ids)
}

# The password policy is NOT managed here, see the comment on
# ZITADEL_DEFAULTINSTANCE_PASSWORDCOMPLEXITYPOLICY_* in docker-compose.zitadel.yml.

resource "zitadel_project" "geopilot" {
  org_id = local.org_id
  name   = "geopilot"

  # Checked at plan time, so a failure aborts before anything is applied. Without it a renamed
  # ZITADEL_FIRSTINSTANCE_ORG_NAME silently yields org_id = null for every resource below.
  lifecycle {
    precondition {
      condition     = length(data.zitadel_orgs.default.ids) == 1
      error_message = "Expected exactly one organization named \"geopilot\", found ${length(data.zitadel_orgs.default.ids)}. It must match ZITADEL_FIRSTINSTANCE_ORG_NAME in docker-compose.zitadel.yml."
    }
  }
}

# The frontend client. Today a public client, which is exactly what the "no public clients"
# feature will change: app_type becomes OIDC_APP_TYPE_WEB and auth_method_type becomes
# OIDC_AUTH_METHOD_TYPE_BASIC or OIDC_AUTH_METHOD_TYPE_PRIVATE_KEY_JWT.
resource "zitadel_application_oidc" "frontend" {
  org_id     = local.org_id
  project_id = zitadel_project.geopilot.id
  name       = "geopilot-client"

  redirect_uris = [
    "https://localhost:5173",
    "https://localhost:5173/swagger/oauth2-redirect.html",
    "https://localhost:7443/swagger/oauth2-redirect.html",
  ]
  post_logout_redirect_uris = ["https://localhost:5173"]

  response_types = ["OIDC_RESPONSE_TYPE_CODE"]
  grant_types = [
    "OIDC_GRANT_TYPE_AUTHORIZATION_CODE",
    # Deliberately more than production, which keeps the module default (authorization code
    # only). Without this grant ZITADEL issues no refresh token even when offline_access is
    # requested, and the refresh behaviour is the very thing this setup exists to exercise.
    # Align both sides once the confidential-client change lands.
    "OIDC_GRANT_TYPE_REFRESH_TOKEN",
  ]

  app_type         = "OIDC_APP_TYPE_USER_AGENT"
  auth_method_type = "OIDC_AUTH_METHOD_TYPE_NONE"

  access_token_type           = "OIDC_TOKEN_TYPE_JWT"
  access_token_role_assertion = true
  id_token_userinfo_assertion = true

  # Permits the localhost redirect URIs above, which a compliant configuration would reject.
  dev_mode = true
}

# Same project as the frontend client on purpose: ZITADEL then puts this application's id into
# the aud claim without an extra scope, which is how production works as well.
resource "zitadel_application_api" "api" {
  org_id     = local.org_id
  project_id = zitadel_project.geopilot.id
  name       = "geopilot-api"

  # BASIC, the same default the geopilot-hosting module applies. It yields a client secret,
  # which the API needs to authenticate against the introspection endpoint when
  # Auth__AccessTokenFormat is set to Opaque. PRIVATE_KEY_JWT would leave that path
  # unreachable locally while it works against Keycloak.
  auth_method_type = "API_AUTH_METHOD_TYPE_BASIC"
}

locals {
  # These ids are the sub values, and they are the same ones the Keycloak realm pins. The seed
  # data references the first two as AuthIdentifier (ContextSeedExtensions.cs:58, :67), so a
  # developer gets the seeded administrator on first login without touching the database, no
  # matter which identity provider is running. newuser is intentionally absent from the seed
  # data: it is the test case for a user registering for the first time.
  users = {
    admin = {
      user_id    = "1f9f9000-c651-4b04-b6ae-9ce1e7f45c15"
      first_name = "Andreas"
      last_name  = "Admin"
      email      = "admin@geopilot.ch"
    }
    uploader = {
      user_id    = "1ed45832-2880-4fd4-a274-bbcc101c3307"
      first_name = "Ursula"
      last_name  = "User"
      email      = "uploader@geopilot.ch"
    }
    newuser = {
      user_id    = "ceab20b3-2e6a-41e7-bd77-3d96e04098ab"
      first_name = "Nina"
      last_name  = "Neu"
      email      = "newuser@geopilot.ch"
    }
  }
}

resource "zitadel_human_user" "dev" {
  for_each = local.users

  org_id  = local.org_id
  user_id = each.value.user_id

  user_name  = each.value.email
  first_name = each.value.first_name
  last_name  = each.value.last_name
  email      = each.value.email

  is_email_verified            = true
  initial_password             = "geopilot_password"
  initial_skip_password_change = true
}
