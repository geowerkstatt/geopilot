import { FC, useContext, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import CheckIcon from "@mui/icons-material/Check";
import ClearIcon from "@mui/icons-material/Clear";
import { CircularProgress, Divider, Stack, ToggleButton, ToggleButtonGroup, Typography } from "@mui/material";
import { toggleButtonClasses } from "@mui/material/ToggleButton";
import { styled } from "@mui/system";
import { Mandate } from "../../api/apiInterfaces";
import { useGeopilotAuth } from "../../auth";
import { Button } from "../../components/buttons";
import useFetch from "../../hooks/useFetch";
import { DeliveryBackButton, DeliveryContinueButton } from "./deliveryButtons";
import { DeliveryContent } from "./deliveryContent";
import { DeliveryContext } from "./deliveryContext";
import { DeliveryStepEnum, DeliveryStepProps } from "./deliveryInterfaces";

const StyledToggleButtonGroup = styled(ToggleButtonGroup)(({ theme }) => ({
  gap: theme.spacing(2),
  flexWrap: "wrap",
  [`& .${toggleButtonClasses.root}`]: {
    flex: `0 0 calc(50% - ${theme.spacing(2)} / 2)`,
    maxWidth: `calc(50% - ${theme.spacing(2)} / 2)`,
    minWidth: 0,
    borderRadius: theme.shape.borderRadius,
    borderLeft: `1px solid ${theme.palette.primary.light}`,
    [`&.${toggleButtonClasses.disabled}`]: {
      borderLeftColor: theme.palette.action.disabledBackground,
    },
  },
}));

const StyledToggleButton = styled(ToggleButton)(({ theme }) => ({
  flexDirection: "column",
  alignItems: "flex-start",
  textAlign: "left",
  gap: theme.spacing(1),
}));

interface MandateToggleButtonProps {
  mandate: Mandate;
}

const MandateToggleButton: FC<MandateToggleButtonProps> = ({ mandate }) => {
  const { user } = useGeopilotAuth();
  const { t, i18n } = useTranslation();

  const steps = mandate.pipelineSteps.map(step => step[i18n.language] ?? step["en"]).join(", ");

  return (
    <StyledToggleButton value={mandate.id} data-cy={`mandate-${mandate.id}`}>
      <Typography variant="h5" m={0}>
        {mandate.name}
      </Typography>
      <Typography variant="body2" color="text.secondary" flex={1}>
        {steps}
      </Typography>
      <Divider sx={{ width: "100%" }} />
      {user && (
        <Stack direction="row" gap={1} alignItems="center">
          {mandate.allowDelivery ? <CheckIcon fontSize="small" /> : <ClearIcon fontSize="small" />}
          <Typography variant="body2">
            {mandate.allowDelivery ? t("deliveryPossible") : t("deliveryNotPossible")}
          </Typography>
        </Stack>
      )}
    </StyledToggleButton>
  );
};

export const DeliverySelectMandate: FC<DeliveryStepProps> = ({ completed }) => {
  const { startProcessing, uploadId, setStepError, isLoading, selectedMandate } = useContext(DeliveryContext);
  const { fetchApi } = useFetch();
  const { t } = useTranslation();
  const { user } = useGeopilotAuth();
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [mandates, setMandates] = useState<Mandate[] | null>(null);

  useEffect(() => {
    if (selectedMandate) {
      setMandates([selectedMandate]);
    } else if (uploadId) {
      setStepError(DeliveryStepEnum.Mandate, undefined);
      fetchApi<Mandate[]>("/api/v1/mandate?" + new URLSearchParams({ uploadId })).then(mandates => {
        if (mandates.length === 0) {
          setStepError(DeliveryStepEnum.Mandate, "noMandatesFound");
        }
        setMandates(mandates);
        setSelectedId(mandates.length === 1 ? mandates[0].id : null);
      });
    }
  }, [uploadId, fetchApi, setStepError, t, user, selectedMandate]);

  const submitForm = () => {
    const mandate = selectedId !== null && mandates?.find(m => m.id === selectedId);
    if (mandate) {
      startProcessing(mandate);
    }
  };

  const handleSelectMandate = (newValue: number | null) => {
    if (!completed && newValue !== null) {
      setSelectedId(newValue);
    }
  };

  const buttons = (
    <>
      <DeliveryBackButton />
      {completed ? (
        <DeliveryContinueButton />
      ) : (
        <Button
          variant="contained"
          onClick={submitForm}
          label="startProcessing"
          disabled={completed || isLoading || selectedId === null}
        />
      )}
    </>
  );

  return (
    <DeliveryContent
      title="mandate"
      subtitle={(mandates?.length ?? 0) > 1 ? "selectMandateSubtitle" : undefined}
      buttons={buttons}>
      <Stack>
        {mandates === null ? (
          <CircularProgress sx={{ alignSelf: "center" }} />
        ) : mandates.length === 0 ? (
          <Typography>{t("noMandatesFound")}</Typography>
        ) : (
          <StyledToggleButtonGroup
            data-cy="mandate-selection-group"
            exclusive
            disabled={completed}
            value={selectedMandate?.id ?? selectedId}
            onChange={(_, value) => handleSelectMandate(value)}>
            {mandates.map(mandate => (
              <MandateToggleButton key={mandate.id} mandate={mandate} />
            ))}
          </StyledToggleButtonGroup>
        )}
      </Stack>
    </DeliveryContent>
  );
};
