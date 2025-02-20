import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import { Typography } from "@mui/material";
import { GeopilotBox } from "../../components/styledComponents.ts";
import {
  FormAutocomplete,
  FormCheckbox,
  FormContainer,
  FormContainerHalfWidth,
  FormInput,
} from "../../components/form/form.ts";
import { Organisation, User } from "../../api/apiInterfaces.ts";
import { useApi } from "../../api";
import { useGeopilotAuth } from "../../auth";
import { FormAutocompleteValue } from "../../components/form/formAutocomplete.tsx";
import AdminDetailForm from "../../components/adminDetailForm.tsx";
import { FieldValues } from "react-hook-form";
import { useTranslation } from "react-i18next";

const UserDetail = () => {
  const { t } = useTranslation();
  const { user } = useGeopilotAuth();
  const { fetchApi } = useApi();
  const { id } = useParams<{ id: string }>();

  const [editableUser, setEditableUser] = useState<User>();
  const [organisations, setOrganisations] = useState<Organisation[]>();

  const loadUser = async (id: string) => {
    const user = await fetchApi<User>(`/api/v1/user/${id}`, { errorMessageLabel: "userLoadingError" });
    setEditableUser(user);
  };

  const loadOrganisations = async () => {
    const organisations = await fetchApi<Organisation[]>("/api/v1/organisation", {
      errorMessageLabel: "organisationsLoadingError",
    });
    setOrganisations(organisations);
  };

  useEffect(() => {
    if (editableUser === undefined && id) {
      loadUser(id);
    }
    if (organisations === undefined) {
      loadOrganisations();
    }
    // We only want to run this once on mount
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const prepareUserForSave = (formData: FieldValues): User => {
    const user = formData as User;
    user.organisations = formData["organisations"]?.map(
      (value: FormAutocompleteValue) => ({ id: value.id }) as Organisation,
    );
    delete user.deliveries;
    return user;
  };

  return (
    id && (
      <AdminDetailForm<User>
        basePath="/admin/users"
        backLabel="backToUsers"
        data={editableUser}
        apiEndpoint="/api/v1/user"
        saveErrorLabel="userSaveError"
        prepareDataForSave={prepareUserForSave}
        onSaveSuccess={setEditableUser}>
        <GeopilotBox>
          <Typography variant={"h3"} margin={0}>
            {t("description")}
          </Typography>
          <FormContainer>
            <FormInput fieldName={"fullName"} label={"name"} value={editableUser?.fullName} disabled={true} />
            <FormInput fieldName={"email"} label={"email"} value={editableUser?.email} disabled={true} />
          </FormContainer>
          <FormContainerHalfWidth>
            <FormCheckbox
              fieldName={"isAdmin"}
              label={"isAdmin"}
              checked={editableUser?.isAdmin ?? false}
              disabled={!user || user?.id === editableUser?.id}
            />
          </FormContainerHalfWidth>
          <FormContainer>
            <FormAutocomplete
              fieldName={"organisations"}
              label={"organisations"}
              required={false}
              values={organisations}
              selected={editableUser?.organisations}
            />
          </FormContainer>
        </GeopilotBox>
      </AdminDetailForm>
    )
  );
};

export default UserDetail;
