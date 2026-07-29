import { FC, useContext, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { CircularProgress, Divider, Stack, styled, ToggleButton, ToggleButtonGroup, Typography } from "@mui/material";
import { toggleButtonClasses } from "@mui/material/ToggleButton";
import { Mandate } from "../../api/apiInterfaces";
import { useGeopilotAuth } from "../../auth";
import { Button } from "../../components/buttons";
import useFetch from "../../hooks/useFetch";
import { useLocalized } from "../../hooks/useLocalized";
import { DeliveryBackButton, DeliveryContinueButton } from "./deliveryButtons";
import { DeliveryContent } from "./deliveryContent";
import { DeliveryContext } from "./deliveryContext";
import { DeliveryStepEnum, DeliveryStepProps } from "./deliveryInterfaces";

const StyledToggleButtonGroup = styled(ToggleButtonGroup)(({ theme }) => ({
  gap: theme.spacing(2),
  flexWrap: "wrap",
  [`& .${toggleButtonClasses.root}`]: {
    borderRadius: theme.radius.default,
    borderLeft: `1px solid ${theme.palette.primary.light}`,
    paddingLeft: theme.spacing(3),
    paddingRight: theme.spacing(3),
    [`&.${toggleButtonClasses.disabled}`]: {
      borderLeftColor: theme.palette.action.disabledBackground,
    },
  },
}));

interface MandateToggleButtonProps {
  mandate: Mandate;
}

const MandateToggleButton: FC<MandateToggleButtonProps> = ({ mandate }) => {
  return (
    <ToggleButton value={mandate} data-cy={`mandate-${mandate.id}`}>
      {mandate.name}
    </ToggleButton>
  );
};

export const DeliverySelectMandate: FC<DeliveryStepProps> = ({ completed }) => {
  const { startProcessing, uploadId, setStepError, isLoading, selectedMandate } = useContext(DeliveryContext);
  const { fetchApi } = useFetch();
  const { t } = useTranslation();
  const { user } = useGeopilotAuth();
  const localized = useLocalized();
  const [selected, setSelected] = useState<Mandate | null>(null);
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
        setSelected(mandates.length === 1 ? mandates[0] : null);
      });
    }
  }, [uploadId, fetchApi, setStepError, t, user, selectedMandate]);

  const currentMandate = selectedMandate ?? selected;
  const description = localized(currentMandate?.description);

  const submitForm = () => {
    if (currentMandate) {
      startProcessing(currentMandate);
    }
  };

  const handleSelectMandate = (mandate: Mandate | null) => {
    if (!completed && mandate) {
      setSelected(mandate);
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
          disabled={completed || isLoading || !currentMandate}
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
            value={currentMandate}
            onChange={(_, value) => handleSelectMandate(value)}>
            {mandates.map(mandate => (
              <MandateToggleButton key={mandate.id} mandate={mandate} />
            ))}
          </StyledToggleButtonGroup>
        )}
        {description && (
          <>
            <Divider />
            <Typography variant="body2" color="text.secondary" sx={{ whiteSpace: "pre-line" }}>
              {description}
            </Typography>
          </>
        )}
      </Stack>
    </DeliveryContent>
  );
};
