import { useCallback, useEffect, useState } from "react";
import { FieldValues } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useParams } from "react-router-dom";
import { Typography } from "@mui/material";
import { MachineClient, Mandate, Organisation, User } from "../../../api/generated";
import AdminDetailForm from "../../../components/adminDetailForm.tsx";
import { useCapabilities } from "../../../components/capabilities/capabilitiesInterface.ts";
import { FormAutocomplete, FormContainer, FormInput } from "../../../components/form/form.ts";
import { FormAutocompleteValue } from "../../../components/form/formAutocomplete.tsx";
import { GeopilotBox } from "../../../components/styledComponents.ts";
import useFetch from "../../../hooks/useFetch.ts";
import { useLocalized } from "../../../hooks/useLocalized.ts";

// What the form starts from when an organisation is created. A constant, so the derived value below stays stable.
const newOrganisation: Organisation = {
  id: 0,
  name: "",
  mandates: [],
  users: [],
  machineClients: [],
};

const OrganisationDetail = () => {
  const { t } = useTranslation();
  const { fetchApi } = useFetch();
  const { localized } = useLocalized();
  const { machineDeliveryEnabled } = useCapabilities();
  const { id = "0" } = useParams<{ id: string }>();

  const [loadedOrganisation, setLoadedOrganisation] = useState<Organisation>();
  const [mandates, setMandates] = useState<Mandate[]>();
  const [users, setUsers] = useState<User[]>();
  const [machineClients, setMachineClients] = useState<MachineClient[]>();

  // Derived rather than set in the effect, and only once the option lists are there: a form that mounts in the
  // very first render starts out dirty, so a new organisation waits for the requests it needs anyway.
  const organisation = id === "0" ? (mandates && users ? newOrganisation : undefined) : loadedOrganisation;

  const loadOrganisation = useCallback(
    (id: string) => {
      fetchApi<Organisation>(`/api/v1/organisation/${id}`, { errorMessageLabel: "organisationLoadingError" }).then(
        setLoadedOrganisation,
      );
    },
    [fetchApi],
  );

  const loadMandates = useCallback(() => {
    fetchApi<Mandate[]>("/api/v1/mandate", { errorMessageLabel: "mandatesLoadingError" }).then(setMandates);
  }, [fetchApi]);

  const loadUsers = useCallback(() => {
    fetchApi<User[]>("/api/v1/user", { errorMessageLabel: "usersLoadingError" }).then(setUsers);
  }, [fetchApi]);

  const loadMachineClients = useCallback(() => {
    fetchApi<MachineClient[]>("/api/v1/machineclient", { errorMessageLabel: "machineClientsLoadingError" }).then(
      setMachineClients,
    );
  }, [fetchApi]);

  useEffect(() => {
    if (id !== "0") {
      loadOrganisation(id);
    }
    loadMandates();
    loadUsers();
    // Without machine delivery the resource is not routed, and the field that would show them is not rendered.
    if (machineDeliveryEnabled) {
      loadMachineClients();
    }
  }, [id, loadMachineClients, loadMandates, loadOrganisation, loadUsers, machineDeliveryEnabled]);

  const prepareOrganisationForSave = (formData: FieldValues): Organisation => {
    const organisation = formData as Organisation;
    organisation.mandates = formData["mandates"]?.map((value: FormAutocompleteValue) => ({ id: value.id }) as Mandate);
    organisation.users = formData["users"]?.map((value: FormAutocompleteValue) => ({ id: value.id }) as User);
    organisation.machineClients = formData["machineClients"]?.map(
      (value: FormAutocompleteValue) => ({ id: value.id }) as MachineClient,
    );
    return organisation;
  };

  return (
    <AdminDetailForm<Organisation>
      basePath="/admin/organisations"
      backLabel="backToOrganisations"
      data={organisation}
      apiEndpoint="/api/v1/organisation"
      saveErrorLabel="organisationSaveError"
      prepareDataForSave={prepareOrganisationForSave}>
      <GeopilotBox>
        <Typography variant={"h3"} sx={{ margin: 0 }}>
          {t("description")}
        </Typography>
        <FormContainer>
          <FormInput fieldName={"name"} label={"name"} value={organisation?.name} required={true} />
        </FormContainer>
        <FormContainer>
          <FormAutocomplete<Mandate>
            fieldName={"mandates"}
            label={"mandates"}
            required={false}
            values={mandates}
            selected={organisation?.mandates}
            valueFormatter={man => ({
              id: man.id,
              primaryText: localized(man.name),
              detailText: `${localized(man.name)} (ID: ${man.id})`,
            })}
          />
        </FormContainer>
        <FormContainer>
          <FormAutocomplete<User>
            fieldName={"users"}
            label={"users"}
            required={false}
            values={users}
            selected={organisation?.users}
            valueFormatter={user => ({
              id: user.id,
              primaryText: user.fullName,
              detailText: `${user.fullName} (${user.email})`,
            })}
          />
        </FormContainer>
        {machineDeliveryEnabled && (
          <FormContainer>
            <FormAutocomplete<MachineClient>
              fieldName={"machineClients"}
              label={"machineClients"}
              required={false}
              values={machineClients}
              selected={organisation?.machineClients}
              valueFormatter={client => ({
                id: client.id,
                primaryText: client.name,
                detailText: `${client.name} (${client.authIdentifier})`,
              })}
            />
          </FormContainer>
        )}
      </GeopilotBox>
    </AdminDetailForm>
  );
};

export default OrganisationDetail;
