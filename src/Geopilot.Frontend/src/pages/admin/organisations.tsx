import { useTranslation } from "react-i18next";
import { AdminGrid } from "../../components/adminGrid/adminGrid";
import { DataRow, GridColDef } from "../../components/adminGrid/adminGridInterfaces";
import { useCallback, useContext, useEffect, useState } from "react";
import { Mandate, Organisation, User } from "../../api/apiInterfaces";
import { useGeopilotAuth } from "../../auth";
import { PromptContext } from "../../components/prompt/promptContext";
import { CircularProgress, Stack } from "@mui/material";
import { useApi } from "../../api";

export const Organisations = () => {
  const { t } = useTranslation();
  const { user } = useGeopilotAuth();
  const [organisations, setOrganisations] = useState<Organisation[]>();
  const [mandates, setMandates] = useState<Mandate[]>();
  const [users, setUsers] = useState<User[]>();
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const { showPrompt } = useContext(PromptContext);
  const { fetchApi } = useApi();

  useEffect(() => {
    if (organisations && mandates && users) {
      setIsLoading(false);
    }
  }, [organisations, mandates, users]);

  const loadOrganisations = useCallback(() => {
    fetchApi<Organisation[]>("/api/v1/organisation", { errorMessageLabel: "organisationsLoadingError" }).then(
      setOrganisations,
    );
  }, [fetchApi]);

  const loadMandates = useCallback(() => {
    fetchApi<Mandate[]>("/api/v1/mandate", { errorMessageLabel: "mandatesLoadingError" }).then(setMandates);
  }, [fetchApi]);

  const loadUsers = useCallback(() => {
    fetchApi<User[]>("/api/v1/user", { errorMessageLabel: "usersLoadingError" }).then(setUsers);
  }, [fetchApi]);

  async function saveOrganisation(organisation: Organisation) {
    organisation.mandates = organisation.mandates?.map(mandateId => {
      return { id: mandateId as number } as Mandate;
    });
    organisation.users = organisation.users?.map(userId => {
      return { id: userId as number } as User;
    });

    fetchApi("/api/v1/organisation", {
      method: organisation.id === 0 ? "POST" : "PUT",
      body: JSON.stringify(organisation),
      errorMessageLabel: "organisationSaveError",
    }).then(loadOrganisations);
  }

  async function onSave(row: DataRow) {
    await saveOrganisation(row as Organisation);
  }

  async function onDisconnect(row: DataRow) {
    showPrompt(t("organisationDisconnectTitle"), t("organisationDisconnectMessage"), [
      { label: t("cancel") },
      {
        label: t("disconnect"),
        action: () => {
          const organisation = row as unknown as Organisation;
          organisation.mandates = [];
          organisation.users = [];
          saveOrganisation(organisation);
        },
        color: "error",
        variant: "contained",
      },
    ]);
  }

  useEffect(() => {
    if (user?.isAdmin) {
      if (organisations === undefined) {
        loadOrganisations();
      }

      if (mandates === undefined) {
        loadMandates();
      }

      if (users === undefined) {
        loadUsers();
      }
    }
  }, [loadMandates, loadOrganisations, loadUsers, mandates, organisations, user?.isAdmin, users]);

  const columns: GridColDef[] = [
    {
      field: "name",
      headerName: t("name"),
      type: "string",
      editable: true,
      flex: 0.5,
      minWidth: 200,
    },
    {
      field: "mandates",
      headerName: t("mandates"),
      editable: true,
      flex: 1,
      minWidth: 400,
      type: "custom",
      valueOptions: mandates,
      getOptionLabel: (value: DataRow | string) => (value as Mandate).name,
      getOptionValue: (value: DataRow | string) => (value as Mandate).id,
    },
    {
      field: "users",
      headerName: t("users"),
      editable: true,
      flex: 1,
      minWidth: 400,
      type: "custom",
      valueOptions: users,
      getOptionLabel: (value: DataRow | string) => (value as User).fullName,
      getOptionValue: (value: DataRow | string) => (value as User).id,
    },
  ];

  return isLoading ? (
    <Stack sx={{ flex: "1 0 0", justifyContent: "center", alignItems: "center", height: "100%" }}>
      <CircularProgress />
    </Stack>
  ) : (
    <AdminGrid
      addLabel="addOrganisation"
      data={organisations as unknown as DataRow[]}
      columns={columns}
      onSave={onSave}
      onDisconnect={onDisconnect}
    />
  );
};

export default Organisations;
