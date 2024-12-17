import { useCallback, useContext, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { CircularProgress, Stack } from "@mui/material";
import { useApi } from "../../api";
import { Organisation, User } from "../../api/apiInterfaces";
import { useGeopilotAuth } from "../../auth";
import { AdminGrid } from "../../components/adminGrid/adminGrid";
import { DataRow, GridColDef } from "../../components/adminGrid/adminGridInterfaces";
import { PromptContext } from "../../components/prompt/promptContext";

export const Users = () => {
  const { t } = useTranslation();
  const { user } = useGeopilotAuth();
  const [users, setUsers] = useState<User[]>();
  const [organisations, setOrganisations] = useState<Organisation[]>();
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const { showPrompt } = useContext(PromptContext);
  const { fetchApi } = useApi();

  useEffect(() => {
    if (users && organisations) {
      setIsLoading(false);
    }
  }, [users, organisations]);

  const loadUsers = useCallback(() => {
    fetchApi<User[]>("/api/v1/user", { errorMessageLabel: "usersLoadingError" }).then(setUsers);
  }, [fetchApi]);

  const loadOrganisations = useCallback(() => {
    fetchApi<Organisation[]>("/api/v1/organisation", { errorMessageLabel: "organisationsLoadingError" }).then(
      setOrganisations,
    );
  }, [fetchApi]);

  async function saveUser(user: User) {
    user.organisations = user.organisations?.map(value =>
      typeof value === "number"
        ? ({ id: value } as Organisation)
        : ({ id: (value as Organisation).id } as Organisation),
    );
    delete user.deliveries;
    fetchApi("/api/v1/user", {
      method: "PUT",
      body: JSON.stringify(user),
      errorMessageLabel: "userSaveError",
    }).then(loadUsers);
  }

  async function onSave(row: DataRow) {
    await saveUser(row as User);
  }

  async function onDisconnect(row: DataRow) {
    showPrompt(t("userDisconnect"), [
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
  }, [loadOrganisations, loadUsers, organisations, user?.isAdmin, users]);

  const columns: GridColDef[] = [
    {
      field: "fullName",
      headerName: t("name"),
      type: "string",
      editable: false,
      flex: 1,
      minWidth: 200,
    },
    {
      field: "email",
      headerName: t("email"),
      type: "string",
      editable: false,
      flex: 1,
      minWidth: 280,
    },
    {
      field: "isAdmin",
      headerName: t("isAdmin"),
      editable: true,
      width: 160,
      type: "boolean",
    },
    {
      field: "organisations",
      headerName: t("organisations"),
      editable: true,
      flex: 1,
      minWidth: 400,
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
