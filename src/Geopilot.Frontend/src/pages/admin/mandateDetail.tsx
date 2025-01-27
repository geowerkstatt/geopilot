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
import { useControlledNavigate } from "../../components/controlledNavigate";
import { PromptAction } from "../../components/prompt/promptInterfaces.ts";
import { FormAutocompleteValue } from "../../components/form/formAutocomplete.tsx";

export const MandateDetail = () => {
  const { t } = useTranslation();
  const { user } = useGeopilotAuth();
  const formMethods = useForm({ mode: "all" });
  const { showPrompt } = useContext(PromptContext);
  const { fetchApi } = useApi();
  const { registerCheckIsDirty, unregisterCheckIsDirty, checkIsDirty, leaveEditingPage, navigateTo } =
    useControlledNavigate();
  const navigate = useNavigate();
  const { id } = useParams<{
    id: string;
  }>();

  const [mandate, setMandate] = useState<Mandate>();
  const [organisations, setOrganisations] = useState<Organisation[]>();
  const [fileExtensions, setFileExtensions] = useState<string[]>();

  useEffect(() => {
    registerCheckIsDirty(`/admin/mandates/${id}`);

    return () => {
      unregisterCheckIsDirty(`/admin/mandates/${id}`);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (checkIsDirty) {
      if (formMethods.formState.isDirty) {
        const promptActions: PromptAction[] = [
          { label: "cancel", icon: <CancelOutlinedIcon />, action: () => leaveEditingPage(false) },
          {
            label: "reset",
            icon: <UndoOutlined />,
            action: () => leaveEditingPage(true),
          },
        ];
        if (formMethods.formState.isValid) {
          promptActions.push({
            label: "save",
            icon: <SaveOutlinedIcon />,
            variant: "contained",
            action: () => {
              saveMandate(formMethods.getValues() as Mandate, false).then(() => leaveEditingPage(true));
            },
          });
        }
        showPrompt("unsavedChanges", promptActions);
      } else {
        leaveEditingPage(true);
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [checkIsDirty]);

  const loadMandate = useCallback(() => {
    if (id !== "0") {
      fetchApi<Mandate>(`/api/v1/mandate/${id}`, { errorMessageLabel: "mandateLoadingError" }).then(setMandate);
    } else {
      setMandate({
        id: 0,
        name: "",
        organisations: [],
        fileTypes: [],
        coordinates: [
          { x: undefined, y: undefined },
          { x: undefined, y: undefined },
        ],
        deliveries: [],
      });
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

  const saveMandate = async (data: FieldValues, reloadAfterSave = true) => {
    if (id !== undefined) {
      const mandate = data as Mandate;
      mandate.deliveries = [];
      mandate.organisations = data["organisations"]?.map(
        (value: FormAutocompleteValue) => ({ id: value.key }) as Organisation,
      );
      mandate.id = parseInt(id);
      try {
        const response = await fetchApi("/api/v1/mandate", {
          method: mandate.id === 0 ? "POST" : "PUT",
          body: JSON.stringify(mandate),
          errorMessageLabel: "mandateSaveError",
        });
        const mandateResponse = response as Mandate;
        if (reloadAfterSave) {
          setMandate(mandateResponse);
          formMethods.reset(mandateResponse);
          if (id === "0") {
            navigate(`/admin/mandates/${mandateResponse.id}`, { replace: true });
          }
        }
      } catch (error) {
        console.error(t("mandateSaveError", { error: (error as Error)?.message }), error);
      }
    }
  };

  const submitForm = (data: FieldValues) => {
    saveMandate(data, true);
  };

  // trigger form validation on mount
  useEffect(() => {
    if (mandate) {
      formMethods.trigger();
    }
  }, [mandate, formMethods, formMethods.trigger]);

  return (
    <FlexBox>
      <FlexRowSpaceBetweenBox>
        <BaseButton
          variant={"text"}
          icon={<ChevronLeft />}
          onClick={() => {
            navigateTo("/admin/mandates");
          }}
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
                    fieldName={"organisations"}
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
                  <FormExtent
                    fieldName={"coordinates"}
                    label={"spatialExtent"}
                    value={mandate?.coordinates}
                    required={true}
                  />
                </FormContainer>
              </GeopilotBox>
              <GeopilotBox>
                <Typography variant={"h3"} margin={0}>
                  {t("deliveryForm")}
                </Typography>
                <FormContainer>
                  <FormSelect
                    fieldName={"evaluatePrecursorDelivery"}
                    label={"precursor"}
                    required={true}
                    selected={mandate?.evaluatePrecursorDelivery}
                    values={[
                      { key: 0, value: FieldEvaluationType.NotEvaluated, name: t("fieldNotEvaluated") },
                      { key: 1, value: FieldEvaluationType.Optional, name: t("fieldOptional") },
                      { key: 2, value: FieldEvaluationType.Required, name: t("fieldRequired") },
                    ]}
                  />
                  <FormSelect
                    fieldName={"evaluatePartial"}
                    label={"partialDelivery"}
                    required={true}
                    selected={mandate?.evaluatePartial}
                    values={[
                      { key: 0, value: FieldEvaluationType.NotEvaluated, name: t("fieldNotEvaluated") },
                      { key: 1, value: FieldEvaluationType.Required, name: t("fieldRequired") },
                    ]}
                  />
                </FormContainer>
                <FormContainerHalfWidth>
                  <FormSelect
                    fieldName={"evaluateComment"}
                    label={"comment"}
                    required={true}
                    selected={mandate?.evaluateComment}
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
                  icon={<UndoOutlined />}
                  variant={"outlined"}
                  disabled={!formMethods.formState.isDirty}
                  onClick={() => formMethods.reset()}
                  label={"reset"}
                />
                <BaseButton
                  icon={<SaveOutlinedIcon />}
                  disabled={!formMethods.formState.isValid || !formMethods.formState.isDirty}
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
