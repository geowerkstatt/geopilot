import { useTranslation } from "react-i18next";
import { useCallback, useEffect, useState } from "react";
import { Mandate, Organisation, User } from "../../api/apiInterfaces";
import { useGeopilotAuth } from "../../auth";
import { Tooltip } from "@mui/material";
import { GridActionsCellItem, GridColDef, GridRowId } from "@mui/x-data-grid";
import { useControlledNavigate } from "../../components/controlledNavigate";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import GeopilotDataGrid from "../../components/geopilotDataGrid.tsx";
import useFetch from "../../hooks/useFetch.ts";

export const Organisations = () => {
  const { t } = useTranslation();
  const { user } = useGeopilotAuth();
  const { navigateTo } = useControlledNavigate();
  const [organisations, setOrganisations] = useState<Organisation[]>();
  const { fetchApi } = useFetch();

  const loadOrganisations = useCallback(() => {
    fetchApi<Organisation[]>("/api/v1/organisation", { errorMessageLabel: "organisationsLoadingError" }).then(
      setOrganisations,
    );
  }, [fetchApi]);

  const startEditing = (id: GridRowId) => {
    navigateTo(`/admin/organisations/${id}`);
  };

  useEffect(() => {
    if (user?.isAdmin) {
      if (organisations === undefined) {
        loadOrganisations();
      }
    }
  }, [loadOrganisations, organisations, user?.isAdmin]);

  const columns: GridColDef[] = [
    {
      field: "name",
      headerName: t("name"),
      type: "string",
      flex: 0.5,
      minWidth: 200,
    },
    {
      field: "mandates",
      headerName: t("mandates"),
      flex: 1,
      minWidth: 400,
      valueGetter: (mandates: Mandate[]) => {
        const sortedNames = [...mandates].map(m => m.name).sort();
        return sortedNames.join(", ");
      },
    },
    {
      field: "users",
      headerName: t("users"),
      flex: 1,
      minWidth: 400,
      valueGetter: (users: User[]) => {
        const sortedNames = [...users].map(u => u.fullName).sort();
        return sortedNames.join(", ");
      },
    },
    {
      field: "actions",
      type: "actions",
      headerName: "",
      flex: 0,
      resizable: false,
      cellClassName: "actions",
      getActions: ({ id }) => [
        <Tooltip title={t("edit")} key={`edit-${id}`}>
          <GridActionsCellItem
            icon={<EditOutlinedIcon />}
            label={t("edit")}
            onClick={() => startEditing(id)}
            color="inherit"
          />
        </Tooltip>,
      ],
    },
  ];

  return (
    <GeopilotDataGrid
      name="organisations"
      addLabel="addOrganisation"
      loading={!organisations?.length}
      rows={organisations}
      columns={columns}
      onSelect={startEditing}
    />
  );
};

export default Organisations;
