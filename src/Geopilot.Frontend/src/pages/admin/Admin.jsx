import { useState, useEffect } from "react";
import { Button, Modal, Alert } from "react-bootstrap";
import { GoTrash } from "react-icons/go";
import { useTranslation } from "react-i18next";
import { DataGrid, deDE } from "@mui/x-data-grid";
import { Snackbar } from "@mui/material";
import { useAuth } from "@/auth";

export const useTranslatedColumns = t => {
  const columns = [
    { field: "id", headerName: t("id"), width: 60 },
    {
      field: "date",
      headerName: t("deliveryDate"),
      valueFormatter: params => {
        const date = new Date(params.value);
        return (
          `${date.getHours().toString().padStart(2, "0")}:` +
          `${date.getMinutes().toString().padStart(2, "0")}:` +
          `${date.getSeconds().toString().padStart(2, "0")} ` +
          `${date.getDate().toString().padStart(2, "0")}.` +
          `${(date.getMonth() + 1).toString().padStart(2, "0")}.` +
          `${date.getFullYear()}`
        );
      },
      width: 180,
    },
    { field: "user", headerName: t("deliveredBy"), flex: 0.5, minWidth: 200 },
    { field: "mandate", headerName: t("mandate"), flex: 0.5, minWidth: 200 },
    { field: "comment", headerName: t("comment"), flex: 1, minWidth: 600 },
  ];
  return columns;
};

export const Admin = () => {
  const { t } = useTranslation();
  const columns = useTranslatedColumns(t);
  const [deliveries, setDeliveries] = useState(undefined);
  const [selectedRows, setSelectedRows] = useState([]);
  const [showModal, setShowModal] = useState(false);
  const [alertMessages, setAlertMessages] = useState([]);
  const [currentAlert, setCurrentAlert] = useState(undefined);
  const [showAlert, setShowAlert] = useState(false);

  const { user } = useAuth();

  if (user && deliveries == undefined) {
    loadDeliveries();
  }

  useEffect(() => {
    if (alertMessages.length && (!currentAlert || !showAlert)) {
      setCurrentAlert(alertMessages[0]);
      setAlertMessages(prev => prev.slice(1));
      setShowAlert(true);
    }
  }, [alertMessages, currentAlert, showAlert]);

  const closeAlert = (event, reason) => {
    if (reason === "clickaway") {
      return;
    }
    setShowAlert(false);
  };

  async function loadDeliveries() {
    try {
      var response = await fetch("/api/v1/delivery");
      if (response.status == 200) {
        var deliveries = await response.json();
        setDeliveries(
          deliveries.map(d => ({
            id: d.id,
            date: d.date,
            user: d.declaringUser.fullName,
            mandate: d.deliveryMandate.name,
            comment: d.comment,
          })),
        );
      }
    } catch (error) {
      setAlertMessages(prev => [
        ...prev,
        {
          message: t("deliveryOverviewLoadingError", { error: error }),
          key: new Date().getTime(),
        },
      ]);
    }
  }

  async function handleDelete() {
    setShowModal(false);
    for (var row of selectedRows) {
      try {
        var response = await fetch("api/v1/delivery/" + row, {
          method: "DELETE",
        });
        if (response.status == 404) {
          setAlertMessages(prev => [
            ...prev,
            {
              message: t("deliveryOverviewDeleteIdNotExistError", { id: row }),
              key: new Date().getTime(),
            },
          ]);
        } else if (response.status == 500) {
          setAlertMessages(prev => [
            ...prev,
            {
              message: t("deliveryOverviewDeleteIdError", { id: row }),
              key: new Date().getTime(),
            },
          ]);
        }
      } catch (error) {
        setAlertMessages(prev => [
          ...prev,
          { message: t("deliveryOverviewDeleteError", { error: error }), key: new Date().getTime() },
        ]);
      }
    }
    await loadDeliveries();
  }

  return (
    <>
      <main>
        {deliveries?.length > 0 && (
          <DataGrid
            sx={{
              fontFamily: "system-ui, -apple-syste",
            }}
            pagination
            rows={deliveries}
            columns={columns}
            initialState={{
              pagination: {
                paginationModel: { page: 0, pageSize: 5 },
              },
            }}
            pageSizeOptions={[5, 10, 25]}
            checkboxSelection
            onRowSelectionModelChange={newSelection => {
              setSelectedRows(newSelection);
            }}
            hideFooterRowCount
            hideFooterSelectedRowCount
          />
        )}
        {selectedRows.length > 0 && (
          <div className="center-button-container">
            <Button
              className="icon-button"
              onClick={() => {
                setShowModal(true);
              }}>
              <GoTrash />
              <div style={{ marginLeft: 10 }}>{t("deleteDelivery", { count: selectedRows.length })}</div>
            </Button>
          </div>
        )}
        <Modal show={showModal} animation={false}>
          <Modal.Body>{t("deleteDeliveryConfirmation")}</Modal.Body>
          <Modal.Footer>
            <Button
              variant="secondary"
              onClick={() => {
                setShowModal(false);
              }}>
              {t("cancel")}
            </Button>
            <Button variant="danger" onClick={handleDelete}>
              {t("delete")}
            </Button>
          </Modal.Footer>
        </Modal>
        <Snackbar
          key={currentAlert ? currentAlert.key : undefined}
          open={showAlert}
          onClose={closeAlert}
          anchorOrigin={{ vertical: "top", horizontal: "right" }}>
          <Alert variant="danger" onClose={closeAlert} dismissible>
            <p>{currentAlert ? currentAlert.message : undefined}</p>
          </Alert>
        </Snackbar>
      </main>
    </>
  );
};

export default Admin;
