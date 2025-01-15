import { useTranslation } from "react-i18next";
import { useCallback, useEffect, useState } from "react";
import { Mandate, Organisation } from "../../api/apiInterfaces";
import { useGeopilotAuth } from "../../auth";
import { DataRow, GridColDef } from "../../components/adminGrid/adminGridInterfaces";
import { useApi } from "../../api";
import { DataGrid, GridActionsCellItem, GridRowId } from "@mui/x-data-grid";
import { Tooltip } from "@mui/material";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import AddIcon from "@mui/icons-material/Add";
import { BaseButton } from "../../components/buttons.tsx";
import { useControlledNavigate } from "../../components/controlledNavigate";

export const Mandates = () => {
  const { t } = useTranslation();
  const { user } = useGeopilotAuth();
  const { navigateTo } = useControlledNavigate();
  const [mandates, setMandates] = useState<Mandate[]>();
  const { fetchApi } = useApi();

  const loadMandates = useCallback(() => {
    fetchApi<Mandate[]>("/api/v1/mandate", { errorMessageLabel: "mandatesLoadingError" }).then(setMandates);
  }, [fetchApi]);

  const startEditing = (id: GridRowId) => {
    navigateTo(`/admin/mandates/${id}`);
  };

  useEffect(() => {
    if (user?.isAdmin) {
      if (mandates === undefined) {
        loadMandates();
      }
    }
  }, [loadMandates, mandates, user?.isAdmin]);

  const columns: GridColDef[] = [
    {
      field: "name",
      headerName: t("name"),
      flex: 0.5,
      minWidth: 200,
    },
    {
      field: "fileTypes",
      headerName: t("fileTypes"),
      flex: 1,
      minWidth: 200,
    },
    {
      field: "organisations",
      headerName: t("organisations"),
      flex: 1,
      minWidth: 400,
      valueFormatter: (organisations: Organisation[]) => {
        return organisations?.map(o => o.name).join(", ");
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
        label={"addMandate"}
      />
      <DataGrid
        loading={!mandates?.length}
        rows={mandates as unknown as DataRow[]}
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

export default Mandates;
