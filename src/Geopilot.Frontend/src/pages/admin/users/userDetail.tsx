import { useCallback, useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import { Typography } from "@mui/material";
import { GeopilotBox } from "../../../components/styledComponents.ts";
import { FormAutocomplete, FormCheckbox, FormContainer, FormInput } from "../../../components/form/form.ts";
import { Organisation, User, UserState } from "../../../api/apiInterfaces.ts";
import { useGeopilotAuth } from "../../../auth/index.ts";
import { FormAutocompleteValue } from "../../../components/form/formAutocomplete.tsx";
import AdminDetailForm from "../../../components/adminDetailForm.tsx";
import { FieldValues } from "react-hook-form";
import { useTranslation } from "react-i18next";
import useFetch from "../../../hooks/useFetch.ts";

const UserDetail = () => {
  const { t } = useTranslation();
  const { user } = useGeopilotAuth();
  const { fetchApi } = useFetch();
  const { id } = useParams<{ id: string }>();

  const [editableUser, setEditableUser] = useState<User>();
  const [organisations, setOrganisations] = useState<Organisation[]>();

  const loadUser = useCallback(
    async (id: string) => {
      const user = await fetchApi<User>(`/api/v1/user/${id}`, { errorMessageLabel: "userLoadingError" });
      setEditableUser(user);
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

  const prepareUserForSave = (formData: FieldValues): User => {
    const user = formData as User;
    user.organisations = formData["organisations"]?.map(
      (value: FormAutocompleteValue) => ({ id: value.id }) as Organisation,
    );
    user.state = formData["isActive"] ? UserState.Active : UserState.Inactive;
    delete user.deliveries;
    return user;
  };

  const prepareUserForForm = (user: User): User => {
    return {
      ...user,
      isActive: user.state === UserState.Active,
    } as User;
  };

  return (
    id && (
      <AdminDetailForm<User>
        basePath="/admin/users"
        backLabel="backToUsers"
        data={editableUser ? prepareUserForForm(editableUser) : undefined}
        apiEndpoint="/api/v1/user"
        saveErrorLabel="userSaveError"
        prepareDataForSave={prepareUserForSave}
        prepareDataAfterSave={prepareUserForForm}
        onSaveSuccess={setEditableUser}>
        <GeopilotBox>
          <Typography variant={"h3"} margin={0}>
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
              disabled={!user || user?.id === editableUser?.id}
            />
            <FormCheckbox
              fieldName={"isActive"}
              label={"active"}
              checked={editableUser?.state === UserState.Active}
              disabled={!user || user?.id === editableUser?.id}
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
