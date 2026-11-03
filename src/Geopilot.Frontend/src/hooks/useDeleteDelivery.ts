import { useCallback, useContext } from "react";
import { useTranslation } from "react-i18next";
import { ApiError } from "../api/apiInterfaces";
import { AlertContext } from "../components/alert/alertContext";
import { PromptContext } from "../components/prompt/promptContext";
import { PromptAction } from "../components/prompt/promptInterfaces";
import useFetch from "./useFetch.ts";

/**
 * Asks whether to delete a delivery and deletes it. The question names the transfers that forwarded the delivery
 * already, since what they forwarded stays at its target.
 */
export const useDeleteDelivery = () => {
  const { t } = useTranslation();
  const { fetchApi } = useFetch();
  const { showPrompt } = useContext(PromptContext);
  const { showAlert } = useContext(AlertContext);

  return useCallback(
    (id: number, forwardedTo: string[], onDone: (deleted: boolean) => void) => {
      const deleteDelivery = () =>
        fetchApi(`/api/v1/delivery/${id}`, { method: "DELETE" })
          .then(() => onDone(true))
          .catch((error: ApiError) => {
            if (error.status === 404) {
              showAlert(t("deliveryOverviewDeleteIdNotExistError", { id: id }), "error");
            } else if (error.status === 500) {
              showAlert(t("deliveryOverviewDeleteIdError", { id: id }), "error");
            } else {
              showAlert(t("deliveryOverviewDeleteError", { error: error }), "error");
            }
            onDone(false);
          });

      const actions: PromptAction[] = [
        { label: "cancel" },
        { label: "delete", action: deleteDelivery, color: "error", variant: "contained" },
      ];
      if (forwardedTo.length > 0) {
        showPrompt("deleteForwardedDeliveryConfirmation", actions, { reactions: forwardedTo.join(", ") });
      } else {
        showPrompt("deleteDeliveryConfirmation", actions);
      }
    },
    [fetchApi, showAlert, showPrompt, t],
  );
};
