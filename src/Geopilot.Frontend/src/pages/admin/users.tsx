import { useTranslation } from "react-i18next";
import { Box, Tooltip } from "@mui/material";
import { Organisation, User, UserType } from "../../api/apiInterfaces";
import { useCallback, useEffect, useState } from "react";
import { useGeopilotAuth } from "../../auth";
import { GridActionsCellItem, GridColDef, GridRenderCellParams, GridRowId, GridValidRowModel } from "@mui/x-data-grid";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import MachineIcon from "@mui/icons-material/SmartToyOutlined";
import { useControlledNavigate } from "../../components/controlledNavigate";
import GeopilotDataGrid from "../../components/geopilotDataGrid.tsx";
import useFetch from "../../hooks/useFetch.ts";

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
      renderCell: (params: GridRenderCellParams<GridValidRowModel, string>) => {
        const userRow = params.row;
        const fullName = params.value;

        if (userRow.userType === UserType.MACHINE) {
          return (
            <Box sx={{ display: "flex", alignItems: "center" }}>
              <Tooltip title={t("machineUser") || "Machine User"}>
                <MachineIcon sx={{ color: "action.active", mr: 1 }} />
              </Tooltip>
              {fullName}
            </Box>
          );
        }
        return fullName;
      },
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
