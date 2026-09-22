terraform {
  required_providers {
    zitadel = {
      source = "zitadel/zitadel"
      # Production pins ~>2.2.0, this needs at least 2.4: the user_id argument on
      # zitadel_human_user, which pins the sub so the seeded administrator matches, does not
      # exist before v2.4.0. The attribute names that matter for the applications are
      # identical in both versions.
      version = "~>2.4"
    }
  }

  # No cloud backend on purpose. This instance is disposable and its state belongs to the
  # developer machine, not to a shared workspace. The state file lives in a named volume,
  # see the zitadel-provision service in docker-compose.zitadel.yml.
  required_version = ">=1.9.0"
}
