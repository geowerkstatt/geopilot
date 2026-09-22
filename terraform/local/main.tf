# Default configuration for the local ZITADEL instance: project, both applications and the
# development users. This is the counterpart to
# config/realms/keycloak-geopilot.json, and it is applied by the zitadel-provision service
# on first start so nobody has to click it together in the console.
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

  # Key of the "terraform" machine user with IAM_OWNER. ZITADEL mints it on first start
  # (ZITADEL_FIRSTINSTANCE_MACHINEKEYPATH) and shares it through the bootstrap volume, so no
  # credential is ever created by hand or committed. A path, not the value, so the credential
  # never enters the terraform state. Production authenticates the same way, via
  # jwt_profile_json.
  jwt_profile_file = "/zitadel/bootstrap/admin-key.json"
}

# No organization is created here on purpose: everything goes into the one FirstInstance
# created. A separate organization would work, but the console shows the organization of the
# signed-in admin, so the provisioned users would appear to be missing until one switches
# organization. Not worth the confusion locally.
#
# Its id is resolved rather than omitted. The documentation claims org_id defaults to the
# organization of the authenticated service account, but zitadel_human_user does not
# implement that: it sends an empty OrganizationId and ZITADEL rejects the request. Passing
# the id to every resource also removes any reliance on implicit provider behaviour.
data "zitadel_orgs" "default" {
  name        = "geopilot" # ZITADEL_FIRSTINSTANCE_ORG_NAME in docker-compose.zitadel.yml
  name_method = "TEXT_QUERY_METHOD_EQUALS"
  state       = "ORG_STATE_ACTIVE"
}

locals {
  # one() fails loudly unless the search matched exactly one organization, which beats
  # silently provisioning into the wrong one.
  org_id = one(data.zitadel_orgs.default.ids)
}

# The password policy is NOT managed here. The development users share one simple password
# that the default ZITADEL policy would reject, but zitadel_password_complexity_policy is
# broken in the provider: it creates the policy and then reports "Root object was present, but
# now absent", which aborts the apply. The policy is therefore relaxed at instance level via
# ZITADEL_DEFAULTINSTANCE_PASSWORDCOMPLEXITYPOLICY_* in docker-compose.zitadel.yml, which is
# where the rest of the instance configuration lives anyway.

resource "zitadel_project" "geopilot" {
  org_id = local.org_id
  name   = "geopilot"
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
    # Without this ZITADEL issues no refresh token even when offline_access is requested.
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

# Same project as the frontend client on purpose: ZITADEL then puts this application's id
# into the aud claim without an extra scope, which is how production works as well.
resource "zitadel_application_api" "api" {
  org_id           = local.org_id
  project_id       = zitadel_project.geopilot.id
  name             = "geopilot-api"
  auth_method_type = "API_AUTH_METHOD_TYPE_PRIVATE_KEY_JWT"
}

locals {
  # These ids are the sub values, and they are the same ones the Keycloak realm pins. The
  # seed data references the first two as AuthIdentifier (ContextSeedExtensions.cs:58, :67),
  # so a developer gets the seeded administrator on first login without touching the
  # database, no matter which identity provider is running. newuser is intentionally absent
  # from the seed data: it is the test case for a user registering for the first time.
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
