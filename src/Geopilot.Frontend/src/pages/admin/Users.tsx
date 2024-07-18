import { useTranslation } from "react-i18next";
import { CircularProgress, Stack } from "@mui/material";
import { AdminGrid } from "../../components/adminGrid/AdminGrid.tsx";
import { DataRow, GridColDef } from "../../components/adminGrid/AdminGridInterfaces.ts";
import { ErrorResponse, Organisation, User } from "../../AppInterfaces.ts";
import { useContext, useEffect, useState } from "react";
import { useAuth } from "../../auth";
import { AlertContext } from "../../components/alert/AlertContext.tsx";
import { PromptContext } from "../../components/prompt/PromptContext.tsx";

export const Users = () => {
  const { t } = useTranslation();
  const { user } = useAuth();
  const [users, setUsers] = useState<User[]>();
  const [organisations, setOrganisations] = useState<Organisation[]>();
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const { showAlert } = useContext(AlertContext);
  const { showPrompt } = useContext(PromptContext);

  useEffect(() => {
    if (users && organisations) {
      setIsLoading(false);
    }
  }, [users, organisations]);

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

  async function saveUser(user: User) {
    try {
      user.organisations = user.organisations?.map(organisationId => {
        return { id: organisationId as number } as Organisation;
      });
      const response = await fetch("/api/v1/user", {
        method: "PUT",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify(user),
      });

      if (response.ok) {
        loadUsers();
      } else {
        const errorResponse: ErrorResponse = await response.json();
        showAlert(t("userSaveError", { error: errorResponse.detail }), "error");
      }
    } catch (error) {
      showAlert(t("userSaveError", { error: error }), "error");
    }
  }

  async function onSave(row: DataRow) {
    await saveUser(row as User);
  }

  async function onDisconnect(row: DataRow) {
    showPrompt(t("userDisconnectTitle"), t("userDisconnectMessage"), [
      { label: t("cancel") },
      {
        label: t("disconnect"),
        action: () => {
          const user = row as unknown as User;
          user.organisations = [];
          saveUser(user);
        },
        color: "error",
        variant: "contained",
      },
    ]);
  }

  useEffect(() => {
    if (user?.isAdmin) {
      if (users === undefined) {
        loadUsers();
      }
      if (organisations === undefined) {
        loadOrganisations();
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const columns: GridColDef[] = [
    {
      field: "fullName",
      headerName: t("name"),
      type: "string",
      editable: false,
      flex: 1,
    },
    {
      field: "email",
      headerName: t("email"),
      type: "string",
      editable: false,
      flex: 1,
    },
    {
      field: "isAdmin",
      headerName: t("isAdmin"),
      editable: true,
      flex: 1,
      type: "boolean",
    },
    {
      field: "organisations",
      headerName: t("organisations"),
      editable: true,
      flex: 1,
      type: "custom",
      valueOptions: organisations,
      getOptionLabel: (value: DataRow | string) => (value as Organisation).name,
      getOptionValue: (value: DataRow | string) => (value as Organisation).id,
    },
  ];

  return isLoading ? (
    <Stack sx={{ flex: "1 0 0", justifyContent: "center", alignItems: "center", height: "100%" }}>
      <CircularProgress />
    </Stack>
  ) : (
    <AdminGrid
      data={users as unknown as DataRow[]}
      columns={columns}
      onSave={onSave}
      onDisconnect={onDisconnect}
      disableRow={user?.id}
    />
  );
};

export default Users;
