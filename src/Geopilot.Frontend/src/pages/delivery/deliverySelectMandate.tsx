import { FC, useContext, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { CircularProgress, Divider, Stack, styled, ToggleButton, ToggleButtonGroup, Typography } from "@mui/material";
import { toggleButtonClasses } from "@mui/material/ToggleButton";
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
    <ToggleButton value={mandate.id} data-cy={`mandate-${mandate.id}`}>
        {mandate.name}
    </ToggleButton>
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
