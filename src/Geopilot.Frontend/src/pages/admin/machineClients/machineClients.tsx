import { useCallback, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import { Tooltip } from "@mui/material";
import { GridActionsCell, GridActionsCellItem, GridColDef, GridRowId } from "@mui/x-data-grid";
import { MachineClient, Organisation } from "../../../api/generated";
import { useGeopilotAuth } from "../../../auth";
import { useControlledNavigate } from "../../../components/controlledNavigate";
import GeopilotDataGrid from "../../../components/grids/geopilotDataGrid.tsx";
import useFetch from "../../../hooks/useFetch.ts";

const MachineClients = () => {
  const { t } = useTranslation();
  const { user } = useGeopilotAuth();
  const { navigateTo } = useControlledNavigate();
  const [machineClients, setMachineClients] = useState<MachineClient[]>();
  const [isLoading, setIsLoading] = useState(true);
  const { fetchApi } = useFetch();

  const loadMachineClients = useCallback(() => {
    fetchApi<MachineClient[]>("/api/v1/machineclient", { errorMessageLabel: "machineClientsLoadingError" })
      .then(setMachineClients)
      .finally(() => setIsLoading(false));
  }, [fetchApi]);

  const startEditing = (id: GridRowId) => {
    navigateTo(`/admin/machine-clients/${id}`);
  };

  useEffect(() => {
    if (user?.isAdmin && machineClients === undefined) {
      loadMachineClients();
    }
  }, [loadMachineClients, machineClients, user?.isAdmin]);

  const columns: GridColDef[] = [
    {
      field: "name",
      headerName: t("name"),
      type: "string",
      flex: 1,
      minWidth: 200,
    },
    {
      field: "authIdentifier",
      headerName: t("machineClientIdentifier"),
      type: "string",
      flex: 1,
      minWidth: 280,
    },
    {
      field: "state",
      headerName: t("machineClientState"),
      width: 160,
      valueFormatter: (param: string) => {
        return t(param);
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
      name="machineClients"
      addLabel="addMachineClient"
      loading={isLoading}
      rows={machineClients}
      columns={columns}
      onSelect={startEditing}
    />
  );
};

export default MachineClients;
