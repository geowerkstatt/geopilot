import { useContext, useEffect, useState } from "react";
import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import { useTranslation } from "react-i18next";
import { DataGrid, GridRowSelectionModel } from "@mui/x-data-grid";
import { Box, Button } from "@mui/material";
import { useGeopilotAuth } from "../../auth";
import { PromptContext } from "../../components/prompt/promptContext";
import { AlertContext } from "../../components/alert/alertContext";
import { Delivery } from "../../api/apiInterfaces";
import { TranslationFunction } from "../../appInterfaces";
import { FetchMethod, runFetch } from "../../api/fetch.ts";

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
    { field: "userName", headerName: t("deliveredBy"), flex: 0.5, minWidth: 200 },
    { field: "mandateName", headerName: t("mandate"), flex: 0.5, minWidth: 200 },
    { field: "comment", headerName: t("comment"), flex: 1, minWidth: 600 },
  ];
};

interface DeliveryMandate {
  id: number;
  date: Date;
  userName: string;
  mandateName: string;
  comment: string;
}

export const DeliveryOverview = () => {
  const { t } = useTranslation();
  const columns = useTranslatedColumns(t);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [deliveries, setDeliveries] = useState<DeliveryMandate[]>([]);
  const [selectedRows, setSelectedRows] = useState<GridRowSelectionModel>([]);
  const { showPrompt } = useContext(PromptContext);
  const { showAlert } = useContext(AlertContext);
  const { user } = useGeopilotAuth();

  async function loadDeliveries() {
    setIsLoading(true);
    runFetch({
      url: "/api/v1/delivery",
      onSuccess: response => {
        setDeliveries(
          (response as Delivery[]).map((d: Delivery) => ({
            id: d.id,
            date: d.date,
            userName: d.declaringUser.fullName,
            mandateName: d.mandate.name,
            comment: d.comment,
          })),
        );
        setIsLoading(false);
      },
      onError: (error: string) => {
        setIsLoading(false);
        showAlert(t("deliveryOverviewLoadingError", { error: error }), "error");
      },
    });
  }

  useEffect(() => {
    if (user?.isAdmin) {
      loadDeliveries();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  async function handleDelete() {
    for (const row of selectedRows) {
      runFetch({
        url: "/api/v1/delivery/" + row,
        method: FetchMethod.DELETE,
        onSuccess: () => {},
        onError: (error: string, status?: number) => {
          if (status === 404) {
            showAlert(t("deliveryOverviewDeleteIdNotExistError", { id: row }), "error");
          } else if (status === 500) {
            showAlert(t("deliveryOverviewDeleteIdError", { id: row }), "error");
          } else {
            showAlert(t("deliveryOverviewDeleteError", { error: error }), "error");
          }
        },
      });
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
