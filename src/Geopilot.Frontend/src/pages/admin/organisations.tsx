import { useTranslation } from "react-i18next";
import { DataRow, GridColDef } from "../../components/adminGrid/adminGridInterfaces";
import { useCallback, useEffect, useState } from "react";
import { Mandate, Organisation, User } from "../../api/apiInterfaces";
import { useGeopilotAuth } from "../../auth";
import { Tooltip } from "@mui/material";
import { useApi } from "../../api";
import { BaseButton } from "../../components/buttons.tsx";
import AddIcon from "@mui/icons-material/Add";
import { DataGrid, GridActionsCellItem, GridRowId } from "@mui/x-data-grid";
import { useControlledNavigate } from "../../components/controlledNavigate";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";

export const Organisations = () => {
  const { t } = useTranslation();
  const { user } = useGeopilotAuth();
  const { navigateTo } = useControlledNavigate();
  const [organisations, setOrganisations] = useState<Organisation[]>();
  const { fetchApi } = useApi();

  const loadOrganisations = useCallback(() => {
    fetchApi<Organisation[]>("/api/v1/organisation", { errorMessageLabel: "organisationsLoadingError" }).then(
      setOrganisations,
    );
  }, [fetchApi]);

  const startEditing = (id: GridRowId) => {
    navigateTo(`/admin/organisations/${id}`);
  };

  useEffect(() => {
    if (user?.isAdmin) {
      if (organisations === undefined) {
        loadOrganisations();
      }
    }
  }, [loadOrganisations, organisations, user?.isAdmin]);

  const columns: GridColDef[] = [
    {
      field: "name",
      headerName: t("name"),
      type: "string",
      flex: 0.5,
      minWidth: 200,
    },
    {
      field: "mandates",
      headerName: t("mandates"),
      flex: 1,
      minWidth: 400,
      valueFormatter: (mandates: Mandate[]) => {
        return mandates?.map(m => m.name).join(", ");
      },
    },
    {
      field: "users",
      headerName: t("users"),
      flex: 1,
      minWidth: 400,
      valueFormatter: (users: User[]) => {
        return users?.map(u => u.fullName).join(", ");
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
      <BaseButton
        variant="outlined"
        icon={<AddIcon />}
        sx={{ marginBottom: "20px" }}
        onClick={() => startEditing(0)}
        label={"addOrganisation"}
      />
      <DataGrid
        data-cy="organisations-grid"
        loading={!organisations?.length}
        rows={organisations as unknown as DataRow[]}
        columns={columns}
        disableColumnSelector
        hideFooterSelectedRowCount
        pagination
        pageSizeOptions={[5, 10, 25]}
        initialState={{
          pagination: { paginationModel: { pageSize: 10 } },
        }}
        onRowSelectionModelChange={newRowSelectionModel => {
          startEditing(newRowSelectionModel[0]);
        }}
      />
    </>
  );
};

export default Organisations;
