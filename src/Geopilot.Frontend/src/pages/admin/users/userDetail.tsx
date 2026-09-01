import { useCallback, useEffect, useState } from "react";
import { FieldValues } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useParams } from "react-router-dom";
import { Typography } from "@mui/material";
import { UserFormValues } from "../../../api/apiInterfaces.ts";
import { Organisation, User, UserState } from "../../../api/generated";
import { useGeopilotAuth } from "../../../auth";
import AdminDetailForm from "../../../components/adminDetailForm.tsx";
import { FormAutocomplete, FormCheckbox, FormContainer, FormInput } from "../../../components/form/form.ts";
import { FormAutocompleteValue } from "../../../components/form/formAutocomplete.tsx";
import { GeopilotBox } from "../../../components/styledComponents.ts";
import useFetch from "../../../hooks/useFetch.ts";

const prepareUserForForm = (user: User): UserFormValues => {
  return {
    ...user,
    isActive: user.state === UserState.Active,
  } satisfies UserFormValues;
};

const UserDetail = () => {
  const { t } = useTranslation();
  const { user } = useGeopilotAuth();
  const { fetchApi } = useFetch();
  const { id } = useParams<{ id: string }>();

  const [editableUser, setEditableUser] = useState<UserFormValues>();
  const [organisations, setOrganisations] = useState<Organisation[]>();

  // Admin rights and active state cannot be edited for your own account (would risk locking yourself out).
  const isOwnUser = !user || user?.id === editableUser?.id;

  const loadUser = useCallback(
    async (id: string) => {
      const user = await fetchApi<User>(`/api/v1/user/${id}`, { errorMessageLabel: "userLoadingError" });
      const userFormValues = prepareUserForForm(user);
      setEditableUser(userFormValues);
    },
    [fetchApi],
  );

  const loadOrganisations = useCallback(async () => {
    const organisations = await fetchApi<Organisation[]>("/api/v1/organisation", {
      errorMessageLabel: "organisationsLoadingError",
    });
    setOrganisations(organisations);
  }, [fetchApi]);

  useEffect(() => {
    if (id) {
      loadUser(id);
    }
    loadOrganisations();
  }, [id, loadOrganisations, loadUser]);

  const prepareUserForSave = (formData: FieldValues): UserFormValues => {
    const editedUser = formData as UserFormValues;
    editedUser.organisations = formData["organisations"]?.map(
      (value: FormAutocompleteValue) => ({ id: value.id }) as Organisation,
    );
    editedUser.state = formData["isActive"] ? UserState.Active : UserState.Inactive;

    // The admin and active fields are disabled for your own account and are not submitted, so keep the
    // current values instead of the empty ones (the backend enforces this too).
    if (isOwnUser) {
      editedUser.isAdmin = editableUser?.isAdmin ?? false;
      editedUser.state = editableUser?.state ?? UserState.Inactive;
    }

    delete editedUser.deliveries;
    return editedUser;
  };

  return (
    id && (
      <AdminDetailForm<UserFormValues>
        basePath="/admin/users"
        backLabel="backToUsers"
        data={editableUser}
        apiEndpoint="/api/v1/user"
        saveErrorLabel="userSaveError"
        prepareDataForSave={prepareUserForSave}>
        <GeopilotBox>
          <Typography variant={"h3"} sx={{ margin: 0 }}>
            {t("description")}
          </Typography>
          <FormContainer>
            <FormInput fieldName={"fullName"} label={"name"} value={editableUser?.fullName} disabled={true} />
            <FormInput fieldName={"email"} label={"email"} value={editableUser?.email} disabled={true} />
          </FormContainer>
          <FormContainer>
            <FormCheckbox
              fieldName={"isAdmin"}
              label={"isAdmin"}
              checked={editableUser?.isAdmin ?? false}
              disabled={isOwnUser}
            />
            <FormCheckbox
              fieldName={"isActive"}
              label={"active"}
              checked={editableUser?.state === UserState.Active}
              disabled={isOwnUser}
            />
          </FormContainer>
          <FormContainer>
            <FormAutocomplete<Organisation>
              fieldName={"organisations"}
              label={"organisations"}
              required={false}
              values={organisations}
              selected={editableUser?.organisations}
              valueFormatter={org => ({
                id: org.id,
                primaryText: org.name,
                detailText: `${org.name} (ID: ${org.id})`,
              })}
            />
          </FormContainer>
        </GeopilotBox>
      </AdminDetailForm>
    )
  );
};

export default UserDetail;
