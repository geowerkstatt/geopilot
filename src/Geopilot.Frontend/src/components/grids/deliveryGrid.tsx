import { FC, useCallback, useContext, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import DeleteOutlinedIcon from "@mui/icons-material/DeleteOutlined";
import { Tooltip } from "@mui/material";
import { GridActionsCell, GridActionsCellItem, GridColDef, GridRenderCellParams, GridRowId } from "@mui/x-data-grid";
import { ApiError, LocalizedText } from "../../api/apiInterfaces";
import {
  DeliveryReactionResponse,
  DeliveryResponse,
  ReactionKind,
  ReactionState,
  UploadedDelivery,
} from "../../api/generated";
import useFetch from "../../hooks/useFetch.ts";
import { useLocalized } from "../../hooks/useLocalized.ts";
import { AlertContext } from "..//alert/alertContext";
import { PromptContext } from "..//prompt/promptContext";
import { Button } from "../buttons";
import { PromptAction } from "../prompt/promptInterfaces";
import { ForwardingDialog } from "./forwardingDialog.tsx";
import GeopilotDataGrid from "./geopilotDataGrid.tsx";
import { ReactionStateLabel } from "./reactionStateLabel.tsx";

interface DeliveryInfo {
  id: number;
  date: string;
  userName: string;
  mandateName: LocalizedText;
  forwardingState?: ReactionState | null;
  comment: string;
  canDelete?: boolean;
  reactions?: DeliveryReactionResponse[];
}

type ColumnName = keyof Omit<DeliveryInfo, "canDelete" | "reactions">;

interface DeliveryGridProps {
  fetchUrl: string;
  columns: ColumnName[];
}

export const DeliveryGrid: FC<DeliveryGridProps> = ({ fetchUrl, columns }) => {
  const { t } = useTranslation();
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [deliveries, setDeliveries] = useState<DeliveryInfo[]>([]);
  const [forwardingOf, setForwardingOf] = useState<DeliveryInfo>();
  const { showPrompt } = useContext(PromptContext);
  const { showAlert } = useContext(AlertContext);
  const { fetchApi } = useFetch();
  const { localized } = useLocalized();

  const loadDeliveries = useCallback(async () => {
    // Administrators get all deliveries with their reactions, users their own uploads and whether they may delete them.
    fetchApi<(DeliveryResponse | UploadedDelivery)[]>(fetchUrl, { errorMessageLabel: "deliveryOverviewLoadingError" })
      .then(response => {
        setDeliveries(
          response.map(d => ({
            id: d.id,
            date: d.date,
            userName: "declarerName" in d ? d.declarerName : "",
            mandateName: d.mandate.name,
            forwardingState: "forwardingState" in d ? d.forwardingState : undefined,
            comment: d.comment,
            canDelete: "canDelete" in d ? d.canDelete : undefined,
            reactions: "reactions" in d ? d.reactions : undefined,
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

  const handleDelete = (id: GridRowId) => {
    fetchApi("/api/v1/delivery/" + id, { method: "DELETE" })
      .catch((error: ApiError) => {
        if (error.status === 404) {
          showAlert(t("deliveryOverviewDeleteIdNotExistError", { id: id }), "error");
        } else if (error.status === 500) {
          showAlert(t("deliveryOverviewDeleteIdError", { id: id }), "error");
        } else {
          showAlert(t("deliveryOverviewDeleteError", { error: error }), "error");
        }
      })
      .finally(() => loadDeliveries());
  };

  const confirmDelete = (id: GridRowId) => {
    const actions: PromptAction[] = [
      { label: "cancel" },
      { label: "delete", action: () => handleDelete(id), color: "error", variant: "contained" },
    ];
    // What a transfer has forwarded stays at its target, so the deletion cannot take it back.
    const forwardedBy = (deliveries.find(delivery => delivery.id === id)?.reactions ?? [])
      .filter(reaction => reaction.kind === ReactionKind.Transfer && reaction.state === ReactionState.Done)
      .map(reaction => localized(reaction.displayName ?? undefined, reaction.reactionId));
    if (forwardedBy.length > 0) {
      showPrompt("deleteForwardedDeliveryConfirmation", actions, { reactions: forwardedBy.join(", ") });
    } else {
      showPrompt("deleteDeliveryConfirmation", actions);
    }
  };

  const namedColumnDefs: Record<ColumnName, GridColDef> = {
    id: { field: "id", headerName: t("id"), width: 60 },
    date: {
      field: "date",
      headerName: t("deliveryDate"),
      valueFormatter: (params: string) => {
        const date = new Date(params);
        return `${date.toLocaleString()}`;
      },
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
      width: 180,
      renderCell: ({ row }: GridRenderCellParams<DeliveryInfo>) =>
        row.forwardingState && (
          <Button variant="text" onClick={() => setForwardingOf(row)} data-cy="forwardingState-button">
            <ReactionStateLabel state={row.forwardingState} />
          </Button>
        ),
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
        {params.row.canDelete !== false && (
          <GridActionsCellItem
            icon={
              <Tooltip title={t("delete")} key={`delete-${params.id}`}>
                <DeleteOutlinedIcon data-cy="delete" color="error" />
              </Tooltip>
            }
            label={t("delete")}
            onClick={() => confirmDelete(params.id)}
          />
        )}
      </GridActionsCell>
    ),
  });

  return (
    <>
      <GeopilotDataGrid name="deliveryOverview" loading={isLoading} rows={deliveries} columns={columnDefs} />
      {forwardingOf && (
        <ForwardingDialog
          deliveryId={forwardingOf.id}
          reactions={forwardingOf.reactions ?? []}
          onClose={() => setForwardingOf(undefined)}
          onReset={() => {
            setForwardingOf(undefined);
            loadDeliveries();
          }}
        />
      )}
    </>
  );
};
