import { useContext, useEffect, useState } from "react";
import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import { useTranslation } from "react-i18next";
import { DataGrid, GridRowSelectionModel } from "@mui/x-data-grid";
import { Box, Button } from "@mui/material";
import { useGeopilotAuth } from "../../auth";
import { PromptContext } from "../../components/prompt/promptContext";
import { AlertContext } from "../../components/alert/alertContext";
import { Delivery, TranslationFunction } from "../../appInterfaces";

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
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [deliveries, setDeliveries] = useState<Delivery[]>([]);
  const [selectedRows, setSelectedRows] = useState<GridRowSelectionModel>([]);
  const { showPrompt } = useContext(PromptContext);
  const { showAlert } = useContext(AlertContext);
  const { user } = useGeopilotAuth();

  async function loadDeliveries() {
    setIsLoading(true);
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
        setIsLoading(false);
      }
    } catch (error) {
      setIsLoading(false);
      showAlert(t("deliveryOverviewLoadingError", { error: error }), "error");
    }
  }

  useEffect(() => {
    if (user?.isAdmin) {
      loadDeliveries();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

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
      <DataGrid
        loading={isLoading}
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
      {selectedRows.length > 0 && (
        <Box sx={{ display: "flex", justifyContent: "center", marginTop: "20px" }}>
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
        </Box>
      )}
    </>
  );
};

export default DeliveryOverview;
