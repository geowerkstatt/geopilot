import { useCallback, useEffect, useState } from "react";
import { FieldValues } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useParams } from "react-router-dom";
import { Typography } from "@mui/material";
import { MachineClientFormValues } from "../../../api/apiInterfaces.ts";
import { MachineClient, MachineClientState, Organisation, User } from "../../../api/generated";
import AdminDetailForm from "../../../components/adminDetailForm.tsx";
import { FormAutocomplete, FormCheckbox, FormContainer, FormInput } from "../../../components/form/form.ts";
import { FormAutocompleteValue } from "../../../components/form/formAutocomplete.tsx";
import { GeopilotBox } from "../../../components/styledComponents.ts";
import useFetch from "../../../hooks/useFetch.ts";

const prepareMachineClientForForm = (machineClient: MachineClient): MachineClientFormValues => {
  return {
    ...machineClient,
    isActive: machineClient.state === MachineClientState.Active,
  } satisfies MachineClientFormValues;
};

// What the form starts from when a client is registered. A constant, so the derived value below stays stable.
const newMachineClient: MachineClientFormValues = {
  id: 0,
  authIdentifier: "",
  name: "",
  state: MachineClientState.Active,
  organisations: [],
  responsibleUsers: [],
  isActive: true,
};

const MachineClientDetail = () => {
  const { t } = useTranslation();
  const { fetchApi } = useFetch();
  const { id = "0" } = useParams<{ id: string }>();

  const [loadedMachineClient, setLoadedMachineClient] = useState<MachineClientFormValues>();
  const [organisations, setOrganisations] = useState<Organisation[]>();
  const [users, setUsers] = useState<User[]>();

  // Derived rather than set in the effect, and only once the choices are there: a form that mounts in the very
  // first render starts out dirty, so a new client waits for the requests it needs anyway.
  const machineClient = id === "0" ? (organisations && users ? newMachineClient : undefined) : loadedMachineClient;

  const loadMachineClient = useCallback(
    (id: string) => {
      fetchApi<MachineClient>(`/api/v1/machineclient/${id}`, { errorMessageLabel: "machineClientLoadingError" }).then(
        machineClient => setLoadedMachineClient(prepareMachineClientForForm(machineClient)),
      );
    },
    [fetchApi],
  );

  const loadOrganisations = useCallback(() => {
    fetchApi<Organisation[]>("/api/v1/organisation", { errorMessageLabel: "organisationsLoadingError" }).then(
      setOrganisations,
    );
  }, [fetchApi]);

  const loadUsers = useCallback(() => {
    fetchApi<User[]>("/api/v1/user", { errorMessageLabel: "usersLoadingError" }).then(setUsers);
  }, [fetchApi]);

  useEffect(() => {
    if (id !== "0") {
      loadMachineClient(id);
    }
    loadOrganisations();
    loadUsers();
  }, [id, loadMachineClient, loadOrganisations, loadUsers]);

  const prepareMachineClientForSave = (formData: FieldValues): MachineClientFormValues => {
    const editedClient = formData as MachineClientFormValues;
    editedClient.organisations = formData["organisations"]?.map(
      (value: FormAutocompleteValue) => ({ id: value.id }) as Organisation,
    );
    editedClient.responsibleUsers = formData["responsibleUsers"]?.map(
      (value: FormAutocompleteValue) => ({ id: value.id }) as User,
    );
    editedClient.state = formData["isActive"] ? MachineClientState.Active : MachineClientState.Inactive;
    delete editedClient.deliveries;
    return editedClient;
  };

  return (
    <AdminDetailForm<MachineClientFormValues>
      basePath="/admin/machine-clients"
      backLabel="backToMachineClients"
      data={machineClient}
      apiEndpoint="/api/v1/machineclient"
      saveErrorLabel="machineClientSaveError"
      prepareDataForSave={prepareMachineClientForSave}>
      <GeopilotBox>
        <Typography variant={"h3"} sx={{ margin: 0 }}>
          {t("general")}
        </Typography>
        <FormContainer>
          <FormInput fieldName={"name"} label={"name"} value={machineClient?.name} required={true} />
        </FormContainer>
        <FormContainer>
          <FormInput
            fieldName={"authIdentifier"}
            label={"machineClientIdentifier"}
            value={machineClient?.authIdentifier}
            required={true}
            helperText={t("machineClientIdentifierHelperText")}
          />
        </FormContainer>
        <FormContainer>
          <FormAutocomplete<Organisation>
            fieldName={"organisations"}
            label={"organisations"}
            required={false}
            values={organisations}
            selected={machineClient?.organisations}
            linkTo={id => `/admin/organisations/${id}`}
            valueFormatter={org => ({
              id: org.id,
              primaryText: org.name,
              detailText: `${org.name} (ID: ${org.id})`,
            })}
          />
        </FormContainer>
        <FormContainer>
          <FormAutocomplete<User>
            fieldName={"responsibleUsers"}
            label={"responsibleUsers"}
            required={false}
            values={users}
            selected={machineClient?.responsibleUsers}
            linkTo={id => `/admin/users/${id}`}
            valueFormatter={user => ({
              id: user.id,
              primaryText: user.fullName,
              detailText: `${user.fullName} (${user.email})`,
            })}
          />
        </FormContainer>
        <FormContainer>
          <FormCheckbox
            fieldName={"isActive"}
            label={"isActive"}
            checked={machineClient?.state === MachineClientState.Active}
          />
        </FormContainer>
      </GeopilotBox>
    </AdminDetailForm>
  );
};

export default MachineClientDetail;
