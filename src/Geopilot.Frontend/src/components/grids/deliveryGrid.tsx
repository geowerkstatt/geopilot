import { FC, useCallback, useContext, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import { Tooltip } from "@mui/material";
import { GridActionsCell, GridActionsCellItem, GridColDef, GridRowId } from "@mui/x-data-grid";
import { ApiError, LocalizedText } from "../../api/apiInterfaces";
import { Delivery } from "../../api/generated";
import useFetch from "../../hooks/useFetch.ts";
import { useLocalized } from "../../hooks/useLocalized.ts";
import { AlertContext } from "..//alert/alertContext";
import { PromptContext } from "..//prompt/promptContext";
import GeopilotDataGrid from "./geopilotDataGrid.tsx";

interface DeliveryInfo {
  id: number;
  date: string;
  userName: string;
  mandateName: LocalizedText;
  comment: string;
  canDelete?: boolean;
}

type ColumnName = keyof Omit<DeliveryInfo, "canDelete">;

interface DeliveryGridProps {
  fetchUrl: string;
  columns: ColumnName[];
}

export const DeliveryGrid: FC<DeliveryGridProps> = ({ fetchUrl, columns }) => {
  const { t } = useTranslation();
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [deliveries, setDeliveries] = useState<DeliveryInfo[]>([]);
  const { showPrompt } = useContext(PromptContext);
  const { showAlert } = useContext(AlertContext);
  const { fetchApi } = useFetch();
  const { localized } = useLocalized();

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
            canDelete: d.canDelete ?? undefined,
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

  const namedColumnDefs: Record<ColumnName, GridColDef> = {
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
    mandateName: {
      field: "mandateName",
      headerName: t("mandate"),
      flex: 0.5,
      minWidth: 200,
      valueGetter: (mandateName: LocalizedText) => localized(mandateName),
    },
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
    renderCell: params => (
      <GridActionsCell {...params}>
        {params.row.canDelete !== false && (
          <GridActionsCellItem
            icon={
              <Tooltip title={t("delete")} key={`delete-${params.id}`}>
                <DeleteOutlinedIcon data-cy="delete" color="error" />
              </Tooltip>
            }
            label={t("delete")}
            onClick={() => confirmDelete(params.id)}
          />
        )}
      </GridActionsCell>
    ),
  });

  return <GeopilotDataGrid name="deliveryOverview" loading={isLoading} rows={deliveries} columns={columnDefs} />;
};
