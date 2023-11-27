import { useState } from "react";
import { useMsal } from "@azure/msal-react";
import { Button, Modal } from "react-bootstrap";
import { GoTrash } from "react-icons/go";
import { DataGrid, deDE } from "@mui/x-data-grid";

const columns = [
  { field: "id", headerName: "ID", width: 60 },
  {
    field: "date",
    headerName: "Abgabedatum",
    valueFormatter: (params) => {
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
    flex: 1,
    minWidth: 150,
  },
  { field: "user", headerName: "Abgegeben von", flex: 1, minWidth: 200 },
  { field: "mandate", headerName: "Operat", flex: 1, minWidth: 200 },
];

export const Admin = () => {
  const [deliveries, setDeliveries] = useState(undefined);
  const [selectedRows, setSelectedRows] = useState([]);
  const [showModal, setShowModal] = useState(false);

  const { instance } = useMsal();
  const activeAccount = instance.getActiveAccount();

  if (activeAccount && deliveries == undefined) {
    fetch("/api/v1/delivery")
      .then((res) => res.ok && res.headers.get("content-type")?.includes("application/json") && res.json())
      .then((deliveries) => {
        if (deliveries) {
          setDeliveries(
            deliveries.map((d) => ({
              id: d.id,
              date: d.date,
              user: d.declaringUser.authIdentifier,
              mandate: d.deliveryMandate.name,
            })),
          );
        }
      });
  }

  async function handleDelete() {
    setShowModal(false);
    fetch("/api/v1/delivery", {
      method: "DELETE",
      body: JSON.stringify(selectedRows),
    })
      .then((res) => res.headers.get("content-type")?.includes("application/json") && res.json())
      .then((deliveries) => {
        setDeliveries(deliveries);
        setSelectedRows([]);
      });
  }

  return (
    <>
      <main>
        {deliveries?.length > 0 && (
          <DataGrid
            localeText={deDE.components.MuiDataGrid.defaultProps.localeText}
            sx={{
              margin: "20px 35px",
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
            onRowSelectionModelChange={(newSelection) => {
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
              }}
            >
              <GoTrash />
              <div style={{ marginLeft: 10 }}>
                {selectedRows.length} Datenabgabe
                {selectedRows.length > 1 ? "n" : ""} löschen
              </div>
            </Button>
          </div>
        )}
        <Modal show={showModal} animation={false}>
          <Modal.Body>
            Möchten Sie die Datenabgabe wirklich löschen? Diese Aktion kann nicht rückgängig gemacht werden.
          </Modal.Body>
          <Modal.Footer>
            <Button
              variant="secondary"
              onClick={() => {
                setShowModal(false);
              }}
            >
              Abbrechen
            </Button>
            <Button variant="danger" onClick={handleDelete}>
              Löschen
            </Button>
          </Modal.Footer>
        </Modal>
      </main>
    </>
  );
};

export default Admin;
