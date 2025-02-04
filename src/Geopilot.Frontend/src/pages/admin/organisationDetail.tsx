import { useCallback, useEffect, useState } from "react";
import { Typography } from "@mui/material";
import { GeopilotBox } from "../../components/styledComponents.ts";
import { FormAutocomplete, FormContainer, FormInput } from "../../components/form/form.ts";
import { Mandate, Organisation, User } from "../../api/apiInterfaces.ts";
import { useApi } from "../../api";
import AdminDetailForm from "../../components/adminDetailForm.tsx";
import { FieldValues } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useParams } from "react-router-dom";
import { FormAutocompleteValue } from "../../components/form/formAutocomplete.tsx";

export const OrganisationDetail = () => {
  const { t } = useTranslation();
  const { fetchApi } = useApi();
  const { id = "0" } = useParams<{ id: string }>();

  const [organisation, setOrganisation] = useState<Organisation>();
  const [mandates, setMandates] = useState<Mandate[]>();
  const [users, setUsers] = useState<User[]>();

  const loadOrganisation = useCallback(() => {
    if (id !== "0") {
      fetchApi<Organisation>(`/api/v1/organisation/${id}`, { errorMessageLabel: "organisationLoadingError" }).then(
        setOrganisation,
      );
    } else {
      setOrganisation({
        id: 0,
        name: "",
        mandates: [],
        users: [],
      });
    }
  }, [fetchApi, id]);

  const loadMandates = useCallback(() => {
    fetchApi<Mandate[]>("/api/v1/mandate", { errorMessageLabel: "mandatesLoadingError" }).then(setMandates);
  }, [fetchApi]);

  const loadUsers = useCallback(() => {
    fetchApi<User[]>("/api/v1/user", { errorMessageLabel: "usersLoadingError" }).then(setUsers);
  }, [fetchApi]);

  useEffect(() => {
    if (organisation === undefined) {
      loadOrganisation();
    }
    if (mandates === undefined) {
      loadMandates();
    }
    if (users === undefined) {
      loadUsers();
    }
  }, [organisation, mandates, users, loadOrganisation, loadMandates, loadUsers]);

  const prepareOrganisationForSave = (formData: FieldValues): Organisation => {
    const organisation = formData as Organisation;
    organisation.mandates = formData["mandates"]?.map((value: FormAutocompleteValue) => ({ id: value.id }) as Mandate);
    organisation.users = formData["users"]?.map((value: FormAutocompleteValue) => ({ id: value.id }) as User);
    organisation.id = parseInt(id);
    return organisation;
  };

  return (
    <AdminDetailForm<Organisation>
      id={id}
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
          <FormAutocomplete
            fieldName={"mandates"}
            label={"mandates"}
            required={false}
            values={mandates}
            selected={organisation?.mandates}
          />
        </FormContainer>
        <FormContainer>
          <FormAutocomplete
            fieldName={"users"}
            label={"users"}
            required={false}
            values={users}
            selected={organisation?.users}
          />
        </FormContainer>
      </GeopilotBox>
    </AdminDetailForm>
  );
};

export default OrganisationDetail;
