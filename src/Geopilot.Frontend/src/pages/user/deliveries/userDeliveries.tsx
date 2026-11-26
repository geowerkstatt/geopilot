import { FC, useCallback, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import { Tooltip, Typography } from "@mui/material";
import { GridActionsCell, GridActionsCellItem, GridColDef } from "@mui/x-data-grid";
import { UploadedDelivery } from "../../../api/generated";
import GeopilotDataGrid from "../../../components/grids/geopilotDataGrid.tsx";
import { CenteredContent } from "../../../components/styledComponents.ts";
import { useDeleteDelivery } from "../../../hooks/useDeleteDelivery.ts";
import useFetch from "../../../hooks/useFetch.ts";
import { useLocalized } from "../../../hooks/useLocalized.ts";

/** The deliveries of the user and of the machine clients the user is responsible for, each deletable while allowed. */
export const UserDeliveries: FC = () => {
  const { t } = useTranslation();
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [deliveries, setDeliveries] = useState<UploadedDelivery[]>([]);
  const { fetchApi } = useFetch();
  const { localized } = useLocalized();
  const confirmDelete = useDeleteDelivery();

  const loadDeliveries = useCallback(async () => {
    fetchApi<UploadedDelivery[]>("/api/v1/delivery/uploads", { errorMessageLabel: "deliveriesLoadingError" })
      .then(setDeliveries)
      .finally(() => {
        setIsLoading(false);
      });
  }, [fetchApi]);

  useEffect(() => {
    loadDeliveries();
  }, [loadDeliveries]);

  const columns: GridColDef<UploadedDelivery>[] = [
    {
      field: "date",
      headerName: t("deliveryDate"),
      type: "dateTime",
      valueGetter: (date: string) => new Date(date),
      width: 180,
    },
    { field: "deliveredByName", headerName: t("deliveredBy"), flex: 0.5, minWidth: 250 },
    {
      field: "mandate",
      headerName: t("mandate"),
      flex: 0.5,
      minWidth: 200,
      valueGetter: (_value, row) => localized(row.mandate.name),
    },
    { field: "comment", headerName: t("comment"), flex: 1, minWidth: 300 },
    {
      field: "actions",
      type: "actions",
      headerName: "",
      flex: 0,
      resizable: false,
      cellClassName: "actions",
      renderCell: params => (
        <GridActionsCell {...params}>
          {params.row.canDelete && (
            <GridActionsCellItem
              icon={
                <Tooltip title={t("delete")} key={`delete-${params.id}`}>
                  <DeleteOutlinedIcon data-cy="delete" color="error" />
                </Tooltip>
              }
              label={t("delete")}
              onClick={() => confirmDelete(params.row.id, [], () => loadDeliveries())}
            />
          )}
        </GridActionsCell>
      ),
    },
  ];

  return (
    <CenteredContent sx={{ height: "100%" }}>
      <Typography variant="h2">{t("myDeliveries")}</Typography>
      <GeopilotDataGrid
        name="deliveries"
        loading={isLoading}
        rows={deliveries}
        columns={columns}
        initialState={{ sorting: { sortModel: [{ field: "date", sort: "desc" }] } }}
      />
    </CenteredContent>
  );
};
