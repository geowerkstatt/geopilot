import { FC, useCallback, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import VisibilityOutlinedIcon from "@mui/icons-material/VisibilityOutlined";
import { Tooltip } from "@mui/material";
import { GridActionsCell, GridActionsCellItem, GridColDef, GridRenderCellParams } from "@mui/x-data-grid";
import { LocalizedText } from "../../api/apiInterfaces";
import { DeliveryResponse, ReactionState, UploadedDelivery } from "../../api/generated";
import { useDeleteDelivery } from "../../hooks/useDeleteDelivery.ts";
import useFetch from "../../hooks/useFetch.ts";
import { useLocalized } from "../../hooks/useLocalized.ts";
import GeopilotDataGrid from "./geopilotDataGrid.tsx";
import { ReactionStateLabel } from "./reactionStateLabel.tsx";

interface DeliveryInfo {
  id: number;
  date: string;
  userName: string;
  mandateName: LocalizedText;
  forwardingState?: ReactionState | null;
  notificationState?: ReactionState | null;
  comment: string;
  canDelete?: boolean;
}

type ColumnName = keyof Omit<DeliveryInfo, "canDelete">;

interface DeliveryGridProps {
  fetchUrl: string;
  columns: ColumnName[];
  /** What a row offers: the page of the delivery, or its deletion while the deliverer may still delete it. */
  rowAction: "details" | "delete";
}

export const DeliveryGrid: FC<DeliveryGridProps> = ({ fetchUrl, columns, rowAction }) => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [deliveries, setDeliveries] = useState<DeliveryInfo[]>([]);
  const { fetchApi } = useFetch();
  const { localized } = useLocalized();
  const confirmDelete = useDeleteDelivery();

  const loadDeliveries = useCallback(async () => {
    // Administrators get all deliveries with how far their reactions got, users their own uploads and whether they
    // may delete them.
    fetchApi<(DeliveryResponse | UploadedDelivery)[]>(fetchUrl, { errorMessageLabel: "deliveryOverviewLoadingError" })
      .then(response => {
        setDeliveries(
          response.map(d => ({
            id: d.id,
            date: d.date,
            userName: "declarerName" in d ? d.declarerName : "",
            mandateName: d.mandate.name,
            forwardingState: "forwardingState" in d ? d.forwardingState : undefined,
            notificationState: "notificationState" in d ? d.notificationState : undefined,
            comment: d.comment,
            canDelete: "canDelete" in d ? d.canDelete : undefined,
          })),
        );
      })
      .finally(() => {
        setIsLoading(false);
      });
  }, [fetchApi, fetchUrl]);

  useEffect(() => {
    loadDeliveries();
  }, [loadDeliveries]);

  const namedColumnDefs: Record<ColumnName, GridColDef> = {
    id: { field: "id", headerName: t("id"), width: 60 },
    date: {
      field: "date",
      headerName: t("deliveryDate"),
      type: "dateTime",
      valueGetter: (date: string) => new Date(date),
      width: 180,
    },
    userName: { field: "userName", headerName: t("deliveredBy"), flex: 0.5, minWidth: 200 },
    mandateName: {
      field: "mandateName",
      headerName: t("mandate"),
      flex: 0.5,
      minWidth: 200,
      valueGetter: (mandateName: LocalizedText) => localized(mandateName),
    },
    forwardingState: {
      field: "forwardingState",
      headerName: t("forwarding"),
      width: 170,
      display: "flex",
      renderCell: ({ row }: GridRenderCellParams<DeliveryInfo>) =>
        row.forwardingState && <ReactionStateLabel state={row.forwardingState} />,
    },
    notificationState: {
      field: "notificationState",
      headerName: t("notifications"),
      width: 190,
      display: "flex",
      renderCell: ({ row }: GridRenderCellParams<DeliveryInfo>) =>
        row.notificationState && <ReactionStateLabel state={row.notificationState} />,
    },
    comment: { field: "comment", headerName: t("comment"), flex: 1, minWidth: 400 },
  };

  const columnDefs = columns.map(column => namedColumnDefs[column]);
  columnDefs.push({
    field: "actions",
    type: "actions",
    headerName: "",
    flex: 0,
    resizable: false,
    cellClassName: "actions",
    renderCell: params => (
      <GridActionsCell {...params}>
        {rowAction === "details" && (
          <GridActionsCellItem
            icon={
              <Tooltip title={t("showDetails")} key={`details-${params.id}`}>
                <VisibilityOutlinedIcon data-cy="details" color="primary" />
              </Tooltip>
            }
            label={t("showDetails")}
            onClick={() => navigate(`/admin/delivery-overview/${params.id}`)}
          />
        )}
        {rowAction === "delete" && params.row.canDelete && (
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
  });

  return (
    <GeopilotDataGrid
      name="deliveryOverview"
      loading={isLoading}
      rows={deliveries}
      columns={columnDefs}
      initialState={{ sorting: { sortModel: [{ field: "date", sort: "desc" }] } }}
    />
  );
};
