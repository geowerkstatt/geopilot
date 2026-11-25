import { useCallback, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import { Tooltip } from "@mui/material";
import { GridActionsCell, GridActionsCellItem, GridColDef, GridRowId } from "@mui/x-data-grid";
import { RefMapping } from "../../../api/generated/index.ts";
import { useGeopilotAuth } from "../../../auth/index.ts";
import { useControlledNavigate } from "../../../components/controlledNavigate/index.ts";
import GeopilotDataGrid from "../../../components/grids/geopilotDataGrid.tsx";
import useFetch from "../../../hooks/useFetch.ts";

export const RefMappings = () => {
  const { t } = useTranslation();
  const { user } = useGeopilotAuth();
  const { navigateTo } = useControlledNavigate();
  const [refMappings, setRefMappings] = useState<RefMapping[]>();
  const [isLoading, setIsLoading] = useState(true);
  const { fetchApi } = useFetch();

  const loadRefMappings = useCallback(() => {
    fetchApi<RefMapping[]>("/api/v1/refmapping", { errorMessageLabel: "refMappingLoadingError" })
      .then(setRefMappings)
      .finally(() => setIsLoading(false));
  }, [fetchApi]);

  const startEditing = (id: GridRowId) => {
    navigateTo(`/admin/refmappings/${id}`);
  };

  useEffect(() => {
    if (user?.isAdmin && refMappings === undefined) {
      loadRefMappings();
    }
  }, [loadRefMappings, refMappings, user?.isAdmin]);

  const columns: GridColDef[] = [
    {
      field: "scope",
      headerName: t("scope"),
      type: "string",
      width: 200,
    },
    {
      field: "refDataUris",
      headerName: t("refData"),
      type: "string",
      flex: 1,
      minWidth: 400,
      valueGetter: (uris: string[]) => uris.join(", "),
    },
    {
      field: "actions",
      type: "actions",
      headerName: "",
      flex: 0,
      resizable: false,
      cellClassName: "actions",
      renderCell: params => (
        <GridActionsCell {...params}>
          <GridActionsCellItem
            icon={
              <Tooltip title={t("edit")} key={`edit-${params.id}`}>
                <EditOutlinedIcon />
              </Tooltip>
            }
            label={t("edit")}
            onClick={() => startEditing(params.id)}
            color="inherit"
          />
        </GridActionsCell>
      ),
    },
  ];

  return (
    <GeopilotDataGrid
      name="refMappings"
      addLabel="addRefMapping"
      loading={isLoading}
      rows={refMappings}
      columns={columns}
      onSelect={startEditing}
    />
  );
};
