import { useCallback, useEffect, useState } from "react";
import { FieldValues } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useParams } from "react-router-dom";
import { Typography } from "@mui/material";
import { MachineClientFormValues } from "../../../api/apiInterfaces.ts";
import { MachineClient, MachineClientState, Organisation } from "../../../api/generated";
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
  isActive: true,
};

const MachineClientDetail = () => {
  const { t } = useTranslation();
  const { fetchApi } = useFetch();
  const { id = "0" } = useParams<{ id: string }>();

  const [loadedMachineClient, setLoadedMachineClient] = useState<MachineClientFormValues>();
  const [organisations, setOrganisations] = useState<Organisation[]>();

  // Derived rather than set in the effect, and only once the organisations are there: a form that mounts in the
  // very first render starts out dirty, so a new client waits for the one request it needs anyway.
  const machineClient = id === "0" ? (organisations ? newMachineClient : undefined) : loadedMachineClient;

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

  useEffect(() => {
    if (id !== "0") {
      loadMachineClient(id);
    }
    loadOrganisations();
  }, [id, loadMachineClient, loadOrganisations]);

  const prepareMachineClientForSave = (formData: FieldValues): MachineClientFormValues => {
    const editedClient = formData as MachineClientFormValues;
    editedClient.organisations = formData["organisations"]?.map(
      (value: FormAutocompleteValue) => ({ id: value.id }) as Organisation,
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
          {t("description")}
        </Typography>
        <FormContainer>
          <FormInput fieldName={"name"} label={"name"} value={machineClient?.name} required={true} />
          <FormInput
            fieldName={"authIdentifier"}
            label={"machineClientIdentifier"}
            value={machineClient?.authIdentifier}
            required={true}
            helperText={t("machineClientIdentifierHelperText")}
          />
        </FormContainer>
        <FormContainer>
          <FormCheckbox
            fieldName={"isActive"}
            label={"active"}
            checked={machineClient?.state === MachineClientState.Active}
          />
        </FormContainer>
        <FormContainer>
          <FormAutocomplete<Organisation>
            fieldName={"organisations"}
            label={"organisations"}
            required={false}
            values={organisations}
            selected={machineClient?.organisations}
            valueFormatter={org => ({
              id: org.id,
              primaryText: org.name,
              detailText: `${org.name} (ID: ${org.id})`,
            })}
          />
        </FormContainer>
      </GeopilotBox>
    </AdminDetailForm>
  );
};

export default MachineClientDetail;
