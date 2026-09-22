import { useCallback, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import { Tooltip } from "@mui/material";
import { GridActionsCell, GridActionsCellItem, GridColDef, GridRowId } from "@mui/x-data-grid";
import { Mandate, Organisation, User } from "../../../api/generated";
import { useGeopilotAuth } from "../../../auth";
import { useControlledNavigate } from "../../../components/controlledNavigate";
import GeopilotDataGrid from "../../../components/grids/geopilotDataGrid.tsx";
import useFetch from "../../../hooks/useFetch.ts";
import { useLocalized } from "../../../hooks/useLocalized.ts";

const Organisations = () => {
  const { t } = useTranslation();
  const { localized } = useLocalized();
  const { user } = useGeopilotAuth();
  const { navigateTo } = useControlledNavigate();
  const [organisations, setOrganisations] = useState<Organisation[]>();
  const [isLoading, setIsLoading] = useState(true);
  const { fetchApi } = useFetch();

  const loadOrganisations = useCallback(() => {
    fetchApi<Organisation[]>("/api/v1/organisation", { errorMessageLabel: "organisationsLoadingError" })
      .then(setOrganisations)
      .finally(() => setIsLoading(false));
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
        const sortedNames = [...mandates].map(m => localized(m.name)).sort();
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
      renderCell: params => (
        <GridActionsCell {...params}>
          <GridActionsCellItem
            icon={
              <Tooltip title={t("edit")} key={`edit-${params.id}`}>
                <EditOutlinedIcon />
              </Tooltip>
            }
            label={t("edit")}
            onClick={() => startEditing(params.id)}
            color="inherit"
          />
        </GridActionsCell>
      ),
    },
  ];

  return (
    <GeopilotDataGrid
      name="organisations"
      addLabel="addOrganisation"
      loading={isLoading}
      rows={organisations}
      columns={columns}
      onSelect={startEditing}
    />
  );
};

export default Organisations;
