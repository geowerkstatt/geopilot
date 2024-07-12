import { useContext, useState } from "react";
import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import { useTranslation } from "react-i18next";
import { DataGrid, GridRowSelectionModel } from "@mui/x-data-grid";
import { Button } from "@mui/material";
import { useAuth } from "../../auth";
import { PromptContext } from "../../components/prompt/PromptContext.tsx";
import { AlertContext } from "../../components/alert/AlertContext.tsx";
import { Delivery, TranslationFunction } from "../../AppInterfaces";

const useTranslatedColumns = (t: TranslationFunction) => {
  return [
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
    { field: "user", headerName: t("deliveredBy"), flex: 0.5, minWidth: 200 },
    { field: "mandate", headerName: t("mandate"), flex: 0.5, minWidth: 200 },
    { field: "comment", headerName: t("comment"), flex: 1, minWidth: 600 },
  ];
};

export const DeliveryOverview = () => {
  const { t } = useTranslation();
  const columns = useTranslatedColumns(t);
  const [deliveries, setDeliveries] = useState<Delivery[]>();
  const [selectedRows, setSelectedRows] = useState<GridRowSelectionModel>([]);
  const { showPrompt } = useContext(PromptContext);
  const { showAlert } = useContext(AlertContext);

  const { user } = useAuth();

  if (user?.isAdmin && deliveries === undefined) {
    loadDeliveries();
  }

  async function loadDeliveries() {
    try {
      const response = await fetch("/api/v1/delivery");
      if (response.status === 200) {
        const deliveries = await response.json();
        setDeliveries(
          deliveries.map((d: Delivery) => ({
            id: d.id,
            date: d.date,
            user: d.declaringUser.fullName,
            mandate: d.mandate.name,
            comment: d.comment,
          })),
        );
      }
    } catch (error) {
      showAlert(t("deliveryOverviewLoadingError", { error: error }), "error");
    }
  }

  async function handleDelete() {
    for (const row of selectedRows) {
      try {
        const response = await fetch("/api/v1/delivery/" + row, {
          method: "DELETE",
        });
        if (response.status === 404) {
          showAlert(t("deliveryOverviewDeleteIdNotExistError", { id: row }), "error");
        } else if (response.status === 500) {
          showAlert(t("deliveryOverviewDeleteIdError", { id: row }), "error");
        }
      } catch (error) {
        showAlert(t("deliveryOverviewDeleteError", { error: error }), "error");
      }
    }
    await loadDeliveries();
  }

  return (
    <>
      {deliveries != undefined && deliveries?.length > 0 && (
        <DataGrid
          sx={{
            fontFamily: "system-ui, -apple-system",
          }}
          pagination
          rows={deliveries}
          columns={columns}
          initialState={{
            pagination: {
              paginationModel: { page: 0, pageSize: 10 },
            },
          }}
          disableColumnSelector
          pageSizeOptions={[5, 10, 25]}
          checkboxSelection
          onRowSelectionModelChange={newSelection => {
            setSelectedRows(newSelection);
          }}
          hideFooterSelectedRowCount
        />
      )}
      {selectedRows.length > 0 && (
        <div className="center-button-container">
          <Button
            color="error"
            variant="contained"
            startIcon={<DeleteOutlinedIcon />}
            onClick={() => {
              showPrompt(t("deleteDeliveryConfirmationTitle"), t("deleteDeliveryConfirmationMessage"), [
                { label: t("cancel") },
                { label: t("delete"), action: handleDelete, color: "error", variant: "contained" },
              ]);
            }}>
            <div>{t("deleteDelivery", { count: selectedRows.length })}</div>
          </Button>
        </div>
      )}
    </>
  );
};

export default DeliveryOverview;
