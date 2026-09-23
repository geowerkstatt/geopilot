terraform {
  required_providers {
    zitadel = {
      source = "zitadel/zitadel"
      # Patch-level like production (~>2.2.0 there). At least 2.4 is needed for the user_id
      # argument on zitadel_human_user, which pins the sub so the seeded administrator matches.
      # .terraform.lock.hcl is committed, so every run uses the same provider build.
      version = "~>2.4.0"
    }
  }

  # No cloud backend on purpose. This instance is disposable and its state belongs to the
  # developer machine, not to a shared workspace.
  required_version = ">=1.9.0"
}
