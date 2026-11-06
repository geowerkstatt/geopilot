import { useCallback, useEffect, useState } from "react";
import { FieldValues } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useParams } from "react-router-dom";
import { Typography } from "@mui/material";
import { UserFormValues } from "../../../api/apiInterfaces.ts";
import { MachineClient, Organisation, User, UserState } from "../../../api/generated";
import { useGeopilotAuth } from "../../../auth";
import AdminDetailForm from "../../../components/adminDetailForm.tsx";
import { useCapabilities } from "../../../components/capabilities/capabilitiesInterface.ts";
import {
  FormAutocomplete,
  FormCheckbox,
  FormContainer,
  FormContainerHalfWidth,
  FormInput,
} from "../../../components/form/form.ts";
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
  const { machineDeliveryEnabled } = useCapabilities();
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

    // Only shown here: who is responsible for a machine client is set on the page of the client.
    delete editedUser.deliveries;
    delete editedUser.responsibleForMachineClients;
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
            {t("general")}
          </Typography>
          <FormContainer>
            <FormInput fieldName={"fullName"} label={"name"} value={editableUser?.fullName} disabled={true} />
            <FormInput fieldName={"email"} label={"email"} value={editableUser?.email} disabled={true} />
          </FormContainer>
          <FormContainer>
            <FormContainerHalfWidth>
              <FormCheckbox
                fieldName={"isAdmin"}
                label={"isAdmin"}
                checked={editableUser?.isAdmin ?? false}
                disabled={isOwnUser}
              />
            </FormContainerHalfWidth>
            <FormContainerHalfWidth>
              <FormCheckbox
                fieldName={"isActive"}
                label={"isActive"}
                checked={editableUser?.state === UserState.Active}
                disabled={isOwnUser}
              />
            </FormContainerHalfWidth>
          </FormContainer>
          <FormContainer>
            <FormAutocomplete<Organisation>
              fieldName={"organisations"}
              label={"organisations"}
              required={false}
              values={organisations}
              selected={editableUser?.organisations}
              linkTo={id => `/admin/organisations/${id}`}
              valueFormatter={org => ({
                id: org.id,
                primaryText: org.name,
                detailText: `${org.name} (ID: ${org.id})`,
              })}
            />
          </FormContainer>
          {machineDeliveryEnabled && (
            <FormContainer>
              <FormAutocomplete<MachineClient>
                fieldName={"responsibleForMachineClients"}
                label={"responsibleForMachineClients"}
                readOnly={true}
                values={editableUser?.responsibleForMachineClients}
                selected={editableUser?.responsibleForMachineClients}
                linkTo={id => `/admin/machine-clients/${id}`}
                valueFormatter={client => ({ id: client.id, primaryText: client.name })}
              />
            </FormContainer>
          )}
        </GeopilotBox>
      </AdminDetailForm>
    )
  );
};

export default UserDetail;
