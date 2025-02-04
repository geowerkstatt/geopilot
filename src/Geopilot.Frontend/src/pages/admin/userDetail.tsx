import { useCallback, useEffect, useState } from "react";
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

export const UserDetail = () => {
  const { t } = useTranslation();
  const { user } = useGeopilotAuth();
  const { fetchApi } = useApi();
  const { id = "0" } = useParams<{ id: string }>();

  const [editableUser, setEditableUser] = useState<User>();
  const [organisations, setOrganisations] = useState<Organisation[]>();

  const loadUser = useCallback(() => {
    if (id !== "0") {
      fetchApi<User>(`/api/v1/user/${id}`, { errorMessageLabel: "userLoadingError" }).then(setEditableUser);
    } else {
      setEditableUser({
        id: 0,
        fullName: "",
        email: "",
        isAdmin: false,
        organisations: [],
      });
    }
  }, [fetchApi, id]);

  const loadOrganisations = useCallback(() => {
    fetchApi<Organisation[]>("/api/v1/organisation", { errorMessageLabel: "organisationsLoadingError" }).then(
      setOrganisations,
    );
  }, [fetchApi]);

  useEffect(() => {
    if (editableUser === undefined) {
      loadUser();
    }
    if (organisations === undefined) {
      loadOrganisations();
    }
  }, [editableUser, organisations, loadUser, loadOrganisations]);

  const prepareUserForSave = (formData: FieldValues): User => {
    const user = formData as User;
    user.organisations = formData["organisations"]?.map(
      (value: FormAutocompleteValue) => ({ id: value.id }) as Organisation,
    );
    user.id = parseInt(id);
    return user;
  };

  return (
    id !== "0" && (
      <AdminDetailForm<User>
        id={id}
        basePath="/admin/users"
        backLabel="backToUsers"
        data={editableUser}
        apiEndpoint="/api/v1/user"
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
