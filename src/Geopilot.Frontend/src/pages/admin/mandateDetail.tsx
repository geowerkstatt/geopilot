import { BaseButton } from "../../components/buttons.tsx";
import { FieldValues, FormProvider, useForm } from "react-hook-form";
import { CircularProgress, Stack, Typography } from "@mui/material";
import { FlexBox, FlexRowEndBox, FlexRowSpaceBetweenBox, GeopilotBox } from "../../components/styledComponents.ts";
import SaveOutlinedIcon from "@mui/icons-material/SaveOutlined";
import { useCallback, useContext, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { PromptContext } from "../../components/prompt/promptContext.tsx";
import CancelOutlinedIcon from "@mui/icons-material/CancelOutlined";
import { ChevronLeft, UndoOutlined } from "@mui/icons-material";
import { useNavigate, useParams } from "react-router-dom";
import {
  FormAutocomplete,
  FormContainer,
  FormContainerHalfWidth,
  FormExtent,
  FormInput,
  FormSelect,
} from "../../components/form/form.ts";
import { FieldEvaluationType, Mandate, Organisation, ValidationSettings } from "../../api/apiInterfaces.ts";
import { useApi } from "../../api";
import { useGeopilotAuth } from "../../auth";

export const MandateDetail = () => {
  const { t } = useTranslation();
  const { user } = useGeopilotAuth();
  const formMethods = useForm({ mode: "all" });
  const { showPrompt } = useContext(PromptContext);
  const { fetchApi } = useApi();
  const navigate = useNavigate();
  const { id } = useParams<{
    id: string;
  }>();

  const [mandate, setMandate] = useState<Mandate>();
  const [organisations, setOrganisations] = useState<Organisation[]>();
  const [fileExtensions, setFileExtensions] = useState<string[]>();

  const loadMandate = useCallback(() => {
    if (id !== "0") {
      fetchApi<Mandate>(`/api/v1/mandate/${id}`, { errorMessageLabel: "mandateLoadingError" }).then(setMandate);
    } else {
      setMandate({ id: 0, name: "", organisations: [], fileTypes: [], coordinates: [], deliveries: [] });
    }
  }, [fetchApi, id]);

  const loadOrganisations = useCallback(() => {
    fetchApi<Organisation[]>("/api/v1/organisation", { errorMessageLabel: "organisationsLoadingError" }).then(
      setOrganisations,
    );
  }, [fetchApi]);

  const loadFileExtensions = useCallback(() => {
    fetchApi<ValidationSettings>("/api/v1/validation", { errorMessageLabel: "fileTypesLoadingError" }).then(
      validation => {
        setFileExtensions(validation?.allowedFileExtensions);
      },
    );
  }, [fetchApi]);

  useEffect(() => {
    if (user?.isAdmin) {
      if (mandate === undefined) {
        loadMandate();
      }
      if (organisations === undefined) {
        loadOrganisations();
      }
      if (fileExtensions === undefined) {
        loadFileExtensions();
      }
    }
  }, [fileExtensions, loadFileExtensions, loadMandate, loadOrganisations, mandate, organisations, user?.isAdmin]);

  async function saveMandate(mandate: Mandate) {
    mandate.deliveries = [];
    mandate.organisations = mandate.organisations?.map(value =>
      typeof value === "number"
        ? ({ id: value } as Organisation)
        : ({ id: (value as Organisation).id } as Organisation),
    );
    await fetchApi("/api/v1/mandate", {
      method: mandate.id === 0 ? "POST" : "PUT",
      body: JSON.stringify(mandate),
      errorMessageLabel: "mandateSaveError",
    });
  }

  const checkChangesBeforeNavigate = () => {
    if (formMethods.formState.isDirty) {
      showPrompt(t("unsavedChanges"), [
        { label: t("cancel"), icon: <CancelOutlinedIcon /> },
        {
          label: t("reset"),
          icon: <UndoOutlined />,
          action: () => {
            navigate(`/admin/mandates`);
          },
        },
        {
          label: t("save"),
          icon: <SaveOutlinedIcon />,
          action: () => {
            saveMandate(formMethods.getValues() as Mandate).then(() => navigate(`/admin/mandates`));
          },
        },
      ]);
    } else {
      navigate(`/admin/mandates`);
    }
  };

  const submitForm = (data: FieldValues) => {
    console.log("submitForm", data);
    //saveMandate(data as Mandate);
  };

  // trigger form validation on mount
  useEffect(() => {
    formMethods.trigger();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [formMethods.trigger]);

  return (
    <FlexBox>
      <FlexRowSpaceBetweenBox>
        <BaseButton
          variant={"text"}
          icon={<ChevronLeft />}
          onClick={checkChangesBeforeNavigate}
          label={"backToMandates"}
        />
        {id !== "0" && <Typography variant={"body2"}>{t("id") + ": " + id}</Typography>}
      </FlexRowSpaceBetweenBox>
      {mandate ? (
        <FormProvider {...formMethods}>
          <form onSubmit={formMethods.handleSubmit(submitForm)}>
            <FlexBox>
              <GeopilotBox>
                <Typography variant={"h3"} margin={0}>
                  {t("description")}
                </Typography>
                <FormContainer>
                  <FormInput fieldName={"name"} label={"name"} value={mandate?.name} required={true} />
                </FormContainer>
                <FormContainer>
                  <FormAutocomplete
                    fieldName={"eligibleOrganisations"}
                    label={"eligibleOrganisations"}
                    required={false}
                    values={organisations?.map(organisation => ({ key: organisation.id, name: organisation.name }))}
                    selected={mandate?.organisations?.map(organisation => ({
                      key: (organisation as Organisation).id,
                      name: (organisation as Organisation).name,
                    }))}
                  />
                </FormContainer>
                <FormContainer>
                  <FormAutocomplete
                    fieldName={"fileTypes"}
                    label={"fileTypes"}
                    required={false}
                    values={fileExtensions}
                    selected={mandate?.fileTypes}
                  />
                </FormContainer>
                <FormContainer>
                  <FormExtent fieldName={"spatialExtent"} label={"spatialExtent"} value={mandate?.coordinates} />
                </FormContainer>
              </GeopilotBox>
              <GeopilotBox>
                <Typography variant={"h3"} margin={0}>
                  {t("deliveryForm")}
                </Typography>
                <FormContainer>
                  <FormSelect
                    fieldName={"precursor"}
                    label={"precursor"}
                    required={true}
                    selected={mandate?.evaluatePrecursorDelivery ? [mandate.evaluatePrecursorDelivery] : []}
                    values={[
                      { key: 0, value: FieldEvaluationType.NotEvaluated, name: t("fieldNotEvaluated") },
                      { key: 1, value: FieldEvaluationType.Optional, name: t("fieldOptional") },
                      { key: 2, value: FieldEvaluationType.Required, name: t("fieldRequired") },
                    ]}
                  />
                  <FormSelect
                    fieldName={"partialDelivery"}
                    label={"partialDelivery"}
                    required={true}
                    selected={mandate?.evaluatePartial ? [mandate.evaluatePartial] : []}
                    values={[
                      { key: 0, value: FieldEvaluationType.NotEvaluated, name: t("fieldNotEvaluated") },
                      { key: 1, value: FieldEvaluationType.Required, name: t("fieldRequired") },
                    ]}
                  />
                </FormContainer>
                <FormContainerHalfWidth>
                  <FormSelect
                    fieldName={"comment"}
                    label={"comment"}
                    required={true}
                    selected={mandate?.evaluateComment ? [mandate.evaluateComment] : []}
                    values={[
                      { key: 0, value: FieldEvaluationType.NotEvaluated, name: t("fieldNotEvaluated") },
                      { key: 1, value: FieldEvaluationType.Optional, name: t("fieldOptional") },
                      { key: 2, value: FieldEvaluationType.Required, name: t("fieldRequired") },
                    ]}
                  />
                </FormContainerHalfWidth>
              </GeopilotBox>
              <FlexRowEndBox>
                <BaseButton
                  icon={<SaveOutlinedIcon />}
                  disabled={!formMethods.formState.isValid}
                  onClick={() => formMethods.handleSubmit(submitForm)()}
                  label={"save"}
                />
              </FlexRowEndBox>
            </FlexBox>
          </form>
        </FormProvider>
      ) : (
        <Stack sx={{ flex: "1 0 0", justifyContent: "center", alignItems: "center", height: "100%" }}>
          <CircularProgress />
        </Stack>
      )}
    </FlexBox>
  );
};

export default MandateDetail;
