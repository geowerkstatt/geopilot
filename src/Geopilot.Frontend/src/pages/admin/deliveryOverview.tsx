import { useContext, useEffect, useState } from "react";
import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import { useTranslation } from "react-i18next";
import { DataGrid, GridRowSelectionModel } from "@mui/x-data-grid";
import { Button } from "@mui/material";
import { useGeopilotAuth } from "../../auth";
import { PromptContext } from "../../components/prompt/promptContext";
import { AlertContext } from "../../components/alert/alertContext";
import { ApiError, Delivery } from "../../api/apiInterfaces";
import { TranslationFunction } from "../../appInterfaces";
import { useApi } from "../../api";
import { FlexRowCenterBox } from "../../components/styledComponents.ts";

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
  const { fetchApi } = useApi();

  async function loadDeliveries() {
    setIsLoading(true);
    fetchApi<Delivery[]>("/api/v1/delivery", { errorMessageLabel: "deliveryOverviewLoadingError" })
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
  }

  useEffect(() => {
    if (user?.isAdmin) {
      loadDeliveries();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function handleDelete() {
    const deletePromises = selectedRows.map(row =>
      fetchApi("/api/v1/delivery/" + row, { method: "DELETE" }).catch((error: ApiError) => {
        if (error.status === 404) {
          showAlert(t("deliveryOverviewDeleteIdNotExistError", { id: row }), "error");
        } else if (error.status === 500) {
          showAlert(t("deliveryOverviewDeleteIdError", { id: row }), "error");
        } else {
          showAlert(t("deliveryOverviewDeleteError", { error: error }), "error");
        }
      }),
    );

    Promise.all(deletePromises).then(() => {
      loadDeliveries();
    });
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
        <FlexRowCenterBox sx={{ marginTop: "20px" }}>
          <Button
            color="error"
            variant="contained"
            startIcon={<DeleteOutlinedIcon />}
            onClick={() => {
              showPrompt("deleteDeliveryConfirmation", [
                { label: "cancel" },
                { label: "delete", action: handleDelete, color: "error", variant: "contained" },
              ]);
            }}>
            <div>{t("deleteDelivery", { count: selectedRows.length })}</div>
          </Button>
        </FlexRowCenterBox>
      )}
    </>
  );
};

export default DeliveryOverview;
