import * as React from "react";
import { useState } from "react";
import {
  AuthenticatedTemplate,
  UnauthenticatedTemplate,
  useMsal,
} from "@azure/msal-react";
import { Box } from "@mui/material";
import { DataGrid } from "@mui/x-data-grid";
import useAuthenticatedFetch from "../../hooks/authHooks";

const columns = [
  { field: "id", headerName: "ID", width: 60 },
  { field: "date", headerName: "Abgabedatum", flex: 1 },
  { field: "declaringUser", headerName: "Abgegeben von", flex: 1 },
  { field: "deliveryMandate", headerName: "Operat", flex: 1 },
];

export const Admin = ({ clientSettings }) => {
  const authenticatedFetch = useAuthenticatedFetch(clientSettings);
  const [deliveries, setDeliveries] = useState([]);

  const { instance } = useMsal();
  const activeAccount = instance.getActiveAccount();

  if (activeAccount && deliveries.length == 0) {
    authenticatedFetch("/api/v1/delivery")
      .then(
        (res) =>
          res.headers.get("content-type")?.includes("application/json") &&
          res.json(),
      )
      .then((deliveries) => {
        setDeliveries(deliveries);
      });
  }

  return (
    <>
      <main>
        <UnauthenticatedTemplate>
          <div className="admin-no-access">
            <div className="app-subtitle">Bitte melden Sie sich an.</div>
          </div>
        </UnauthenticatedTemplate>
        <AuthenticatedTemplate>
          <div className="app-title">Datenabgaben</div>
          <Box sx={{ width: "100%", padding: "20px 35px" }}>
            <DataGrid
              pagination
              rows={deliveries}
              columns={columns}
              initialState={{
                pagination: {
                  paginationModel: { page: 0, pageSize: 10 },
                },
              }}
              pageSizeOptions={[5, 10, 25]}
              checkboxSelection
            />
          </Box>
        </AuthenticatedTemplate>
      </main>
    </>
  );
};

export default Admin;
