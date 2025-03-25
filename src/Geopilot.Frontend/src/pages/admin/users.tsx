import { useTranslation } from "react-i18next";
import { Tooltip } from "@mui/material";
import { Organisation, User } from "../../api/apiInterfaces";
import { useCallback, useEffect, useState } from "react";
import { useGeopilotAuth } from "../../auth";
import { GridActionsCellItem, GridColDef, GridRowId } from "@mui/x-data-grid";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import { useControlledNavigate } from "../../components/controlledNavigate";
import GeopilotDataGrid from "../../components/geopilotDataGrid.tsx";
import useApiFetch from "../../hooks/useApiFetch.ts";

export const Users = () => {
  const { t } = useTranslation();
  const { user } = useGeopilotAuth();
  const { navigateTo } = useControlledNavigate();
  const [users, setUsers] = useState<User[]>();
  const { fetchApi } = useApiFetch();

  const loadUsers = useCallback(() => {
    fetchApi<User[]>("/api/v1/user", { errorMessageLabel: "usersLoadingError" }).then(setUsers);
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
      <GeopilotDataGrid name="users" loading={!users?.length} rows={users} columns={columns} onSelect={startEditing} />
    </>
  );
};

export default Users;
