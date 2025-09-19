import { useTranslation } from "react-i18next";
import { Tooltip } from "@mui/material";
import { Organisation, User } from "../../../api/apiInterfaces.ts";
import { useCallback, useEffect, useState } from "react";
import { useGeopilotAuth } from "../../../auth/index.ts";
import { GridActionsCellItem, GridColDef, GridRowId } from "@mui/x-data-grid";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import { useControlledNavigate } from "../../../components/controlledNavigate/index.ts";
import GeopilotDataGrid from "../../../components/geopilotDataGrid.tsx";
import useFetch from "../../../hooks/useFetch.ts";

export const Users = () => {
  const { t } = useTranslation();
  const { user } = useGeopilotAuth();
  const { navigateTo } = useControlledNavigate();
  const [users, setUsers] = useState<User[]>();
  const [isLoading, setIsLoading] = useState(true);
  const { fetchApi } = useFetch();

  const loadUsers = useCallback(() => {
    fetchApi<User[]>("/api/v1/user", { errorMessageLabel: "usersLoadingError" })
      .then(setUsers)
      .finally(() => setIsLoading(false));
  }, [fetchApi]);

  const startEditing = (id: GridRowId) => {
    navigateTo(`/admin/users/${id}`);
  };

  useEffect(() => {
    if (user?.isAdmin) {
      if (users === undefined) {
        loadUsers();
      }
    }
  }, [loadUsers, users, user?.isAdmin]);

  const columns: GridColDef[] = [
    {
      field: "fullName",
      headerName: t("name"),
      type: "string",
      flex: 1,
      minWidth: 200,
    },
    {
      field: "email",
      headerName: t("email"),
      type: "string",
      flex: 1,
      minWidth: 280,
    },
    {
      field: "isAdmin",
      headerName: t("isAdmin"),
      width: 160,
      type: "boolean",
    },
    {
      field: "organisations",
      headerName: t("organisations"),
      flex: 1,
      minWidth: 400,
      valueGetter: (organisations: Organisation[]) => {
        const sortedNames = [...organisations.map(o => o.name)].sort();
        return sortedNames.join(", ");
      },
    },
    {
      field: "actions",
      type: "actions",
      headerName: "",
      flex: 0,
      resizable: false,
      cellClassName: "actions",
      getActions: ({ id }) => [
        <Tooltip title={t("edit")} key={`edit-${id}`}>
          <GridActionsCellItem
            icon={<EditOutlinedIcon />}
            label={t("edit")}
            onClick={() => startEditing(id)}
            color="inherit"
          />
        </Tooltip>,
      ],
    },
  ];

  return (
    <>
      <GeopilotDataGrid name="users" loading={isLoading} rows={users} columns={columns} onSelect={startEditing} />
    </>
  );
};

export default Users;
