import { useTranslation } from "react-i18next";
import { AdminGrid } from "../../components/adminGrid/adminGrid";
import { DataRow, GridColDef } from "../../components/adminGrid/adminGridInterfaces";
import { useContext, useEffect, useState } from "react";
import { Mandate, Organisation, User } from "../../api/apiInterfaces";
import { ErrorResponse } from "../../appInterfaces";
import { useGeopilotAuth } from "../../auth";
import { AlertContext } from "../../components/alert/alertContext";
import { PromptContext } from "../../components/prompt/promptContext";
import { CircularProgress, Stack } from "@mui/material";

export const Organisations = () => {
  const { t } = useTranslation();
  const { user } = useGeopilotAuth();
  const [organisations, setOrganisations] = useState<Organisation[]>();
  const [mandates, setMandates] = useState<Mandate[]>();
  const [users, setUsers] = useState<User[]>();
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const { showAlert } = useContext(AlertContext);
  const { showPrompt } = useContext(PromptContext);

  useEffect(() => {
    if (organisations && mandates && users) {
      setIsLoading(false);
    }
  }, [organisations, mandates, users]);

  async function loadOrganisations() {
    try {
      const response = await fetch("/api/v1/organisation");
      if (response.ok) {
        const results = await response.json();
        setOrganisations(results);
      } else {
        const errorResponse: ErrorResponse = await response.json();
        showAlert(t("organisationsLoadingError", { error: errorResponse.detail }), "error");
      }
    } catch (error) {
      showAlert(t("organisationsLoadingError", { error: error }), "error");
    }
  }

  async function loadMandates() {
    try {
      const response = await fetch("/api/v1/mandate");
      if (response.ok) {
        const results = await response.json();
        setMandates(results);
      } else {
        const errorResponse: ErrorResponse = await response.json();
        showAlert(t("mandatesLoadingError", { error: errorResponse.detail }), "error");
      }
    } catch (error) {
      showAlert(t("mandatesLoadingError", { error: error }), "error");
    }
  }

  async function loadUsers() {
    try {
      const response = await fetch("/api/v1/user");
      if (response.ok) {
        const results = await response.json();
        setUsers(results);
      } else {
        const errorResponse: ErrorResponse = await response.json();
        showAlert(t("usersLoadingError", { error: errorResponse.detail }), "error");
      }
    } catch (error) {
      showAlert(t("usersLoadingError", { error: error }), "error");
    }
  }

  async function saveOrganisation(organisation: Organisation) {
    try {
      organisation.mandates = organisation.mandates?.map(mandateId => {
        return { id: mandateId as number } as Mandate;
      });
      organisation.users = organisation.users?.map(userId => {
        return { id: userId as number } as User;
      });
      const response = await fetch("/api/v1/organisation", {
        method: organisation.id === 0 ? "POST" : "PUT",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify(organisation),
      });

      if (response.ok) {
        loadOrganisations();
      } else {
        const errorResponse: ErrorResponse = await response.json();
        showAlert(t("organisationSaveError", { error: errorResponse.detail }), "error");
      }
    } catch (error) {
      showAlert(t("organisationSaveError", { error: error }), "error");
    }
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
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const columns: GridColDef[] = [
    {
      field: "name",
      headerName: t("name"),
      type: "string",
      editable: true,
      flex: 1,
    },
    {
      field: "mandates",
      headerName: t("mandates"),
      editable: true,
      flex: 1,
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
