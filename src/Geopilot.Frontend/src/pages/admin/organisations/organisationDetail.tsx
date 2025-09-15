import { useCallback, useEffect, useState } from "react";
import { Typography } from "@mui/material";
import { GeopilotBox } from "../../../components/styledComponents.ts";
import { FormAutocomplete, FormContainer, FormInput } from "../../../components/form/form.ts";
import { Mandate, Organisation, User } from "../../../api/apiInterfaces.ts";
import AdminDetailForm from "../../../components/adminDetailForm.tsx";
import { FieldValues } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useParams } from "react-router-dom";
import { FormAutocompleteValue } from "../../../components/form/formAutocomplete.tsx";
import useFetch from "../../../hooks/useFetch.ts";

const OrganisationDetail = () => {
  const { t } = useTranslation();
  const { fetchApi } = useFetch();
  const { id = "0" } = useParams<{ id: string }>();

  const [organisation, setOrganisation] = useState<Organisation>();
  const [mandates, setMandates] = useState<Mandate[]>();
  const [users, setUsers] = useState<User[]>();

  const loadOrganisation = useCallback(
    async (id: string) => {
      const organisation = await fetchApi<Organisation>(`/api/v1/organisation/${id}`, {
        errorMessageLabel: "organisationLoadingError",
      });
      setOrganisation(organisation);
    },
    [fetchApi],
  );

  const loadMandates = useCallback(async () => {
    const mandates = await fetchApi<Mandate[]>("/api/v1/mandate", { errorMessageLabel: "mandatesLoadingError" });
    setMandates(mandates);
  }, [fetchApi]);

  const loadUsers = useCallback(async () => {
    const users = await fetchApi<User[]>("/api/v1/user", { errorMessageLabel: "usersLoadingError" });
    setUsers(users);
  }, [fetchApi]);

  useEffect(() => {
    if (id !== "0") {
      loadOrganisation(id);
    } else {
      setOrganisation({
        id: 0,
        name: "",
        mandates: [],
        users: [],
      });
    }
    loadMandates();
    loadUsers();
  }, [id, loadMandates, loadOrganisation, loadUsers]);

  const prepareOrganisationForSave = (formData: FieldValues): Organisation => {
    const organisation = formData as Organisation;
    organisation.mandates = formData["mandates"]?.map((value: FormAutocompleteValue) => ({ id: value.id }) as Mandate);
    organisation.users = formData["users"]?.map((value: FormAutocompleteValue) => ({ id: value.id }) as User);
    return organisation;
  };

  return (
    <AdminDetailForm<Organisation>
      basePath="/admin/organisations"
      backLabel="backToOrganisations"
      data={organisation}
      apiEndpoint="/api/v1/organisation"
      saveErrorLabel="organisationSaveError"
      prepareDataForSave={prepareOrganisationForSave}
      onSaveSuccess={setOrganisation}>
      <GeopilotBox>
        <Typography variant={"h3"} margin={0}>
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
              primaryText: man.name,
              detailText: `${man.name} (ID: ${man.id})`,
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
      </GeopilotBox>
    </AdminDetailForm>
  );
};

export default OrganisationDetail;
