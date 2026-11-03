import { FC, Fragment, useCallback, useContext, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link as RouterLink, useNavigate, useParams } from "react-router-dom";
import { Box, CircularProgress, Link, Stack, Typography } from "@mui/material";
import { DeliveryDetailResponse, DeliveryReactionResponse, ReactionKind, ReactionState } from "../../../api/generated";
import { AdminDetailHeader } from "../../../components/adminDetailHeader.tsx";
import { AlertContext } from "../../../components/alert/alertContext";
import { Button } from "../../../components/buttons.tsx";
import { ReactionsGrid } from "../../../components/grids/reactionsGrid.tsx";
import { GeopilotBox } from "../../../components/styledComponents.ts";
import { useDeleteDelivery } from "../../../hooks/useDeleteDelivery.ts";
import useFetch from "../../../hooks/useFetch.ts";
import { useLocalized } from "../../../hooks/useLocalized.ts";
import { SummaryField } from "./summaryField.tsx";

const overviewPath = "/admin/delivery-overview";

const formatTime = (value: string) => new Date(value).toLocaleString();

/** A delivery with what was delivered and how far its reactions got. */
export const DeliveryDetail: FC = () => {
  const { t } = useTranslation();
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { fetchApi } = useFetch();
  const { localized } = useLocalized();
  const { showAlert } = useContext(AlertContext);
  const confirmDelete = useDeleteDelivery();
  const [loaded, setLoaded] = useState<DeliveryDetailResponse>();

  // From a delivery to its precursor the page stays mounted, so the previous delivery must not show meanwhile.
  const delivery = loaded?.id === Number(id) ? loaded : undefined;

  const loadDelivery = useCallback(
    () =>
      fetchApi<DeliveryDetailResponse>(`/api/v1/delivery/${id}`, { errorMessageLabel: "deliveryLoadingError" }).then(
        setLoaded,
      ),
    [fetchApi, id],
  );

  useEffect(() => {
    // useFetch has shown why the delivery could not be loaded.
    loadDelivery().catch(() => undefined);
  }, [loadDelivery]);

  const retryReaction = (reaction: DeliveryReactionResponse) =>
    fetchApi(`/api/v1/delivery/${id}/reactions/${reaction.reactionId}/reset`, {
      method: "POST",
      errorMessageLabel: "reactionRetryError",
    })
      .then(() => {
        showAlert(t("reactionRetried"), "success");
        return loadDelivery();
      })
      // useFetch has shown why; the retry can be repeated.
      .catch(() => undefined);

  if (!delivery) {
    return (
      <Stack>
        <AdminDetailHeader backLabel="backToDeliveryOverview" onBack={() => navigate(overviewPath)} />
        <Stack sx={{ flex: "1 0 0", justifyContent: "center", alignItems: "center", height: "100%" }}>
          <CircularProgress />
        </Stack>
      </Stack>
    );
  }

  // Only a forwarded delivery is in the STAC catalog, and only then can its files be downloaded.
  const stacItem = delivery.stacItem;
  const forwardedTo = delivery.reactions
    .filter(reaction => reaction.kind === ReactionKind.Transfer && reaction.state === ReactionState.Done)
    .map(reaction => localized(reaction.displayName ?? undefined, reaction.reactionId));

  return (
    <Stack>
      <AdminDetailHeader backLabel="backToDeliveryOverview" onBack={() => navigate(overviewPath)} id={delivery.id} />
      <GeopilotBox>
        <Typography variant="h3" sx={{ marginTop: 0 }}>
          {t("summary")}
        </Typography>
        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "1fr 1fr" }, gap: 2 }}>
          <SummaryField label="deliveryDate">{formatTime(delivery.date)}</SummaryField>
          <SummaryField label="deliveredBy">{delivery.declarerName}</SummaryField>
          <SummaryField label="mandate">{localized(delivery.mandate.name)}</SummaryField>
          {delivery.precursorDelivery && (
            <SummaryField label="precursor">
              <Link component={RouterLink} to={`${overviewPath}/${delivery.precursorDelivery.id}`}>
                {`${delivery.precursorDelivery.id} (${formatTime(delivery.precursorDelivery.date)})`}
              </Link>
            </SummaryField>
          )}
          {delivery.partial != null && (
            <SummaryField label="partialDelivery">{t(delivery.partial ? "yes" : "no")}</SummaryField>
          )}
          <SummaryField label="jobId">{delivery.jobId}</SummaryField>
          <SummaryField label="files">
            {delivery.files.map((file, index) => (
              <Fragment key={file.id}>
                {index > 0 && ", "}
                {stacItem ? (
                  <Link href={`/api/v1/delivery/assets/${file.id}`} download>
                    {file.fileName}
                  </Link>
                ) : (
                  file.fileName
                )}
              </Fragment>
            ))}
          </SummaryField>
          <SummaryField label="stacBrowser">
            {stacItem ? (
              // The STAC browser names its pages by the path from the folder of its catalog, /api/stac.
              <Link
                href={`/browser/stac/collections/${stacItem.collectionId}/items/${stacItem.itemId}`}
                target="_blank"
                rel="noopener">
                {stacItem.itemId}
              </Link>
            ) : (
              t("stacItemAfterForwarding")
            )}
          </SummaryField>
          {delivery.comment && (
            <SummaryField label="comment" wide>
              {delivery.comment}
            </SummaryField>
          )}
        </Box>
      </GeopilotBox>
      {delivery.reactions.length > 0 && (
        <GeopilotBox>
          <Typography variant="h3" sx={{ marginTop: 0 }}>
            {t("reactions")}
          </Typography>
          <ReactionsGrid reactions={delivery.reactions} forwarded={!!stacItem} onRetry={retryReaction} />
        </GeopilotBox>
      )}
      <Stack direction="row">
        <Button
          variant="outlined"
          color="error"
          label="delete"
          onClick={() =>
            confirmDelete(delivery.id, forwardedTo, deleted => {
              if (deleted) {
                navigate(overviewPath);
              }
            })
          }
        />
      </Stack>
    </Stack>
  );
};
