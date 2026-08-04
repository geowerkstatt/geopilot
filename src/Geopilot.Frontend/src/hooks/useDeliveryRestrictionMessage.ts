import { useCallback } from "react";
import { useTranslation } from "react-i18next";
import { StepResult } from "../api/apiInterfaces.ts";
import { getDeliveryRestrictionReason } from "../pages/delivery/deliveryUtils.tsx";
import { useLocalized } from "./useLocalized.ts";

export const useDeliveryRestrictionMessage = () => {
  const { t } = useTranslation();
  const localized = useLocalized();

  return useCallback(
    (steps: StepResult[]) => {
      let message = t("deliveryNotPossible");
      const restrictionReason = getDeliveryRestrictionReason(steps);
      if (restrictionReason) {
        message += `: ${localized(restrictionReason)}`;
      }
      return message;
    },
    [t, localized],
  );
};
