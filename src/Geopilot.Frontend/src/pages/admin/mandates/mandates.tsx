import { useTranslation } from "react-i18next";
import { useCallback, useEffect, useState } from "react";
import { Mandate, Organisation } from "../../../api/apiInterfaces";
import { useGeopilotAuth } from "../../../auth";
import { GridActionsCellItem, GridColDef, GridRowId } from "@mui/x-data-grid";
import { Tooltip } from "@mui/material";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import { useControlledNavigate } from "../../../components/controlledNavigate";
import GeopilotDataGrid from "../../../components/geopilotDataGrid.tsx";
import useFetch from "../../../hooks/useFetch.ts";

export const Mandates = () => {
  const { t } = useTranslation();
  const { user } = useGeopilotAuth();
  const { navigateTo } = useControlledNavigate();
  const [mandates, setMandates] = useState<Mandate[]>();
  const [isLoading, setIsLoading] = useState(true);
  const { fetchApi } = useFetch();

  const loadMandates = useCallback(() => {
    fetchApi<Mandate[]>("/api/v1/mandate", { errorMessageLabel: "mandatesLoadingError" })
      .then(setMandates)
      .finally(() => setIsLoading(false));
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
      valueGetter: (fileTypes: string[]) => {
        const sortedNames = fileTypes.sort();
        return sortedNames.join(", ");
      },
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
    <GeopilotDataGrid
      name="mandates"
      addLabel="addMandate"
      loading={isLoading}
      rows={mandates}
      columns={columns}
      onSelect={startEditing}
    />
  );
};

export default Mandates;
