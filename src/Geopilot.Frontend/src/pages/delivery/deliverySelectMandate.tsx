import PublishedWithChangesIcon from "@mui/icons-material/PublishedWithChanges";
import { Stack, ToggleButton, ToggleButtonGroup, Typography } from "@mui/material";
import { toggleButtonClasses } from "@mui/material/ToggleButton";
import { styled } from "@mui/system";
import { useContext, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Mandate } from "../../api/apiInterfaces";
import { useGeopilotAuth } from "../../auth";
import { BaseButton, CancelButton } from "../../components/buttons";
import useFetch from "../../hooks/useFetch";
import { DeliveryContent } from "./deliveryContent";
import { DeliveryContext } from "./deliveryContext";
import { DeliveryStepEnum } from "./deliveryInterfaces";

const StyledToggleButtonGroup = styled(ToggleButtonGroup)(({ theme }) => ({
  gap: "1rem",
  flexWrap: "wrap",
  [`& .${toggleButtonClasses.root}`]: {
    borderRadius: theme.shape.borderRadius,
    borderLeft: `1px solid ${theme.palette.primary.light}`,
  },
}));

const StyledToggleButton = styled(ToggleButton)({
  width: "max-content",
  maxWidth: "400px",
});

export const DeliverySelectMandate = () => {
  const { resetDelivery, startProcessing, jobId, setStepError, setSelectedMandate, isLoading } =
    useContext(DeliveryContext);
  const { fetchApi } = useFetch();
  const { t } = useTranslation();
  const { user } = useGeopilotAuth();
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [mandates, setMandates] = useState<Mandate[]>([]);

  useEffect(() => {
    if (jobId) {
      fetchApi<Mandate[]>("/api/v1/mandate?" + new URLSearchParams({ jobId })).then(mandates => {
        if (mandates.length === 0) {
          setStepError(DeliveryStepEnum.Process, "noMandatesFound");
        }
        setMandates(mandates);
        setSelectedId(mandates.length === 1 ? mandates[0].id : null);
      });
    }
  }, [jobId, fetchApi, setStepError, t, user]);

  const submitForm = () => {
    if (selectedId) {
      startProcessing({
        mandateId: selectedId,
      });
      setSelectedMandate(mandates.find(m => m.id === selectedId));
    }
  };

  const handleSelectMandate = (newValue: number | null) => {
    if (newValue !== null) {
      setSelectedId(newValue);
    }
  };

  const buttons = (
    <>
      <CancelButton onClick={resetDelivery} />
      <BaseButton
        onClick={submitForm}
        icon={<PublishedWithChangesIcon />}
        label="process"
        disabled={isLoading || selectedId === null}
      />
    </>
  );

  return (
    <DeliveryContent title="selectMandate" subtitle="selectMandateSubtitle" buttons={buttons}>
      <Stack>
        {mandates.length === 0 ? (
          <Typography>{t("noMandatesFound")}</Typography>
        ) : (
          <StyledToggleButtonGroup exclusive value={selectedId} onChange={(_, value) => handleSelectMandate(value)}>
            {mandates.map(mandate => (
              <StyledToggleButton key={mandate.id} value={mandate.id}>
                {mandate.name}
              </StyledToggleButton>
            ))}
          </StyledToggleButtonGroup>
        )}
      </Stack>
    </DeliveryContent>
  );
};
