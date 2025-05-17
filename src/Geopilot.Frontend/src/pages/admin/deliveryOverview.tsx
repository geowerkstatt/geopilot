import { useCallback, useContext, useEffect, useState } from "react";
import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import { useTranslation } from "react-i18next";
import { GridActionsCellItem, GridColDef, GridRenderCellParams, GridRowId, GridValidRowModel } from "@mui/x-data-grid";
import { Box, Tooltip } from "@mui/material";
import { useGeopilotAuth } from "../../auth";
import { PromptContext } from "../../components/prompt/promptContext";
import { AlertContext } from "../../components/alert/alertContext";
import { ApiError, Delivery, UserType } from "../../api/apiInterfaces";
import GeopilotDataGrid from "../../components/geopilotDataGrid.tsx";
import useFetch from "../../hooks/useFetch.ts";
import MachineIcon from "@mui/icons-material/SmartToyOutlined";

interface DeliveryMandate {
  id: number;
  date: Date;
  userName: string;
  userType: UserType;
  mandateName: string;
  comment: string;
}

export const DeliveryOverview = () => {
  const { t } = useTranslation();
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [deliveries, setDeliveries] = useState<DeliveryMandate[]>([]);
  const { showPrompt } = useContext(PromptContext);
  const { showAlert } = useContext(AlertContext);
  const { user } = useGeopilotAuth();
  const { fetchApi } = useFetch();

  const loadDeliveries = useCallback(async () => {
    fetchApi<Delivery[]>("/api/v1/delivery", { errorMessageLabel: "deliveryOverviewLoadingError" })
      .then(response => {
        setDeliveries(
          response.map((d: Delivery) => ({
            id: d.id,
            date: d.date,
            userName: d.declaringUser.fullName,
            userType: d.declaringUser.userType,
            mandateName: d.mandate.name,
            comment: d.comment,
          })),
        );
      })
      .finally(() => {
        setIsLoading(false);
      });
  }, [fetchApi]);

  useEffect(() => {
    if (user?.isAdmin) {
      loadDeliveries();
    }
  }, [loadDeliveries, user?.isAdmin]);

  const handleDelete = (id: GridRowId) => {
    fetchApi("/api/v1/delivery/" + id, { method: "DELETE" })
      .catch((error: ApiError) => {
        if (error.status === 404) {
          showAlert(t("deliveryOverviewDeleteIdNotExistError", { id: id }), "error");
        } else if (error.status === 500) {
          showAlert(t("deliveryOverviewDeleteIdError", { id: id }), "error");
        } else {
          showAlert(t("deliveryOverviewDeleteError", { error: error }), "error");
        }
      })
      .finally(() => loadDeliveries());
  };

  const confirmDelete = (id: GridRowId) => {
    showPrompt("deleteDeliveryConfirmation", [
      { label: "cancel" },
      { label: "delete", action: () => handleDelete(id), color: "error", variant: "contained" },
    ]);
  };

  const columns: GridColDef[] = [
    { field: "id", headerName: t("id"), width: 60 },
    {
      field: "date",
      headerName: t("deliveryDate"),
      valueFormatter: (params: string) => {
        const date = new Date(params);
        return `${date.toLocaleString()}`;
      },
      width: 180,
    },
    {
      field: "userName",
      headerName: t("deliveredBy"),
      flex: 0.5,
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
    { field: "mandateName", headerName: t("mandate"), flex: 0.5, minWidth: 200 },
    { field: "comment", headerName: t("comment"), flex: 1, minWidth: 600 },
    {
      field: "actions",
      type: "actions",
      headerName: "",
      flex: 0,
      resizable: false,
      cellClassName: "actions",
      getActions: ({ id }) => [
        <Tooltip title={t("delete")} key={`delete-${id}`}>
          <GridActionsCellItem
            icon={<DeleteOutlinedIcon />}
            label={t("delete")}
            onClick={() => confirmDelete(id)}
            color="error"
          />
        </Tooltip>,
      ],
    },
  ];

  return <GeopilotDataGrid name="deliveryOverview" loading={isLoading} rows={deliveries} columns={columns} />;
};

export default DeliveryOverview;
