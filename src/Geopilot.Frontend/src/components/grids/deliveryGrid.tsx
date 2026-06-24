import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import { Tooltip } from "@mui/material";
import { GridActionsCellItem, GridColDef, GridRowId } from "@mui/x-data-grid";
import { FC, useCallback, useContext, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { ApiError, Delivery } from "../../api/apiInterfaces";
import { AlertContext } from "../../components/alert/alertContext";
import GeopilotDataGrid from "../../components/geopilotDataGrid";
import { PromptContext } from "../../components/prompt/promptContext";
import useFetch from "../../hooks/useFetch";

interface DeliveryInfo {
  id: number;
  date: string;
  userName: string;
  mandateName: string;
  comment: string;
}

interface DeliveryGridProps {
  fetchUrl: string;
  columns: (keyof DeliveryInfo)[];
}

export const DeliveryGrid: FC<DeliveryGridProps> = ({ fetchUrl, columns }) => {
  const { t } = useTranslation();
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [deliveries, setDeliveries] = useState<DeliveryInfo[]>([]);
  const { showPrompt } = useContext(PromptContext);
  const { showAlert } = useContext(AlertContext);
  const { fetchApi } = useFetch();

  const loadDeliveries = useCallback(async () => {
    fetchApi<Delivery[]>(fetchUrl, { errorMessageLabel: "deliveryOverviewLoadingError" })
      .then(response => {
        setDeliveries(
          response.map((d: Delivery) => ({
            id: d.id,
            date: d.date,
            userName: d.declaringUser.fullName,
            mandateName: d.mandate.name,
            comment: d.comment,
          })),
        );
      })
      .finally(() => {
        setIsLoading(false);
      });
  }, [fetchApi, fetchUrl]);

  useEffect(() => {
    loadDeliveries();
  }, [loadDeliveries]);

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

  const namedColumnDefs: Record<keyof DeliveryInfo, GridColDef> = {
    id: { field: "id", headerName: t("id"), width: 60 },
    date: {
      field: "date",
      headerName: t("deliveryDate"),
      valueFormatter: (params: string) => {
        const date = new Date(params);
        return `${date.toLocaleString()}`;
      },
      width: 180,
    },
    userName: { field: "userName", headerName: t("deliveredBy"), flex: 0.5, minWidth: 200 },
    mandateName: { field: "mandateName", headerName: t("mandate"), flex: 0.5, minWidth: 200 },
    comment: { field: "comment", headerName: t("comment"), flex: 1, minWidth: 400 },
  };

  const columnDefs = columns.map(column => namedColumnDefs[column]);
  columnDefs.push({
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
  });

  return <GeopilotDataGrid name="deliveryOverview" loading={isLoading} rows={deliveries} columns={columnDefs} />;
};
