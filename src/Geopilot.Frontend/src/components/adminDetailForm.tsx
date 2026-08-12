import { ReactNode, useCallback, useContext, useEffect, useRef } from "react";
import { FieldValues, FormProvider, KeepStateOptions, useForm } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { ChevronLeft } from "@mui/icons-material";
import { Box, CircularProgress, Stack, Typography } from "@mui/material";
import { alpha } from "@mui/material/styles";
import useFetch from "../hooks/useFetch.ts";
import { Button } from "./buttons.tsx";
import { useControlledNavigate } from "./controlledNavigate";
import { PromptContext } from "./prompt/promptContext.tsx";
import { PromptAction } from "./prompt/promptInterfaces.ts";

/**
 * A form reset with reset(data) does not clear a field that is missing in data.
 * i.e. if data.foo = undefined, the field foo would not be reset.
 * keepFieldsRef makes so that you can omit a field from data, and it still resets the field,
 * instead of silently just keeping the value.
 */
const resetOptions: KeepStateOptions = { keepFieldsRef: true };

interface AdminDetailFormProps<T> {
  basePath: string;
  backLabel: string;
  data: T | undefined;
  apiEndpoint: string;
  saveErrorLabel: string;
  prepareDataForSave: (data: FieldValues) => T;
  prepareDataAfterSave?: (data: T) => T;
  onSaveSuccess: (savedData: T) => void;
  children: ReactNode;
}

const AdminDetailForm = <T extends { id: number }>({
  basePath,
  backLabel,
  data,
  apiEndpoint,
  saveErrorLabel,
  prepareDataForSave,
  prepareDataAfterSave,
  onSaveSuccess,
  children,
}: AdminDetailFormProps<T>) => {
  const { t } = useTranslation();
  const { fetchApi } = useFetch();
  const formMethods = useForm({ mode: "all" });
  const { registerCheckIsDirty, unregisterCheckIsDirty, checkIsDirty, leaveEditingPage, navigateTo } =
    useControlledNavigate();
  const navigate = useNavigate();
  const { showPrompt } = useContext(PromptContext);
  const dataIdRef = useRef<number | undefined>(data?.id);

  const saveData = useCallback(
    async (formData: FieldValues, reloadAfterSave = true) => {
      const id = dataIdRef.current || 0;
      const dataToSave = prepareDataForSave(formData);
      dataToSave.id = id;
      const response = await fetchApi(apiEndpoint, {
        method: id === 0 ? "POST" : "PUT",
        body: JSON.stringify(dataToSave),
        errorMessageLabel: saveErrorLabel,
      });
      const savedData = response as T;
      const newFormData = prepareDataAfterSave ? prepareDataAfterSave(savedData) : savedData;

      if (reloadAfterSave) {
        onSaveSuccess(savedData);
        formMethods.reset(newFormData, resetOptions);
      }

      return savedData;
    },
    [
      apiEndpoint,
      fetchApi,
      formMethods,
      onSaveSuccess,
      prepareDataForSave,
      prepareDataAfterSave,
      saveErrorLabel
    ],
  );

  const submitForm = async (data: FieldValues) => {
    await saveData(data, false);
    navigate(basePath);
  };

  useEffect(() => {
    const path = window.location.pathname;
    registerCheckIsDirty(path);

    return () => {
      unregisterCheckIsDirty(path);
    };
  }, [registerCheckIsDirty, unregisterCheckIsDirty]);

  useEffect(() => {
    if (checkIsDirty) {
      if (!formMethods.formState.isDirty) {
        leaveEditingPage(true);
      } else {
        formMethods.trigger().then(isValid => {
          const promptActions: PromptAction[] = [
            { label: "cancel", action: () => leaveEditingPage(false) },
            {
              label: "reset",
              action: () => leaveEditingPage(true),
            },
          ];
          if (isValid) {
            promptActions.push({
              label: "save",
              variant: "contained",
              action: () => {
                saveData(formMethods.getValues(), false).then(() => leaveEditingPage(true));
              },
            });
          }
          showPrompt("unsavedChanges", promptActions);
        });
      }
    }
  }, [checkIsDirty, formMethods, leaveEditingPage, saveData, showPrompt]);

  useEffect(() => {
    if (data) {
      dataIdRef.current = data.id;
    }
  }, [data]);

  return (
    <Stack>
      <Stack direction="row" sx={{ alignItems: "center", flexWrap: "wrap", justifyContent: "space-between" }}>
        <Button variant="text" startIcon={<ChevronLeft />} onClick={() => navigateTo(basePath)} label={backLabel} />
        {data && data.id !== 0 && <Typography variant={"body2"}>{t("id") + ": " + data?.id}</Typography>}
      </Stack>
      {!data ? (
        <Stack sx={{ flex: "1 0 0", justifyContent: "center", alignItems: "center", height: "100%" }}>
          <CircularProgress />
        </Stack>
      ) : (
        <FormProvider {...formMethods}>
          <Box sx={{ position: "relative" }}>
            <form onSubmit={event => formMethods.handleSubmit(submitForm)(event)}>
              <Stack>
                {children}
                <Stack direction="row" sx={{ alignItems: "center", flexWrap: "wrap", justifyContent: "flex-end" }}>
                  <Button
                    disabled={!formMethods.formState.isDirty}
                    onClick={() => formMethods.reset(data, resetOptions)}
                    label={"reset"}
                  />
                  <Button
                    variant="contained"
                    disabled={
                      formMethods.formState.isSubmitting ||
                      !formMethods.formState.isDirty ||
                      (formMethods.formState.errors && Object.keys(formMethods.formState.errors).length > 0)
                    }
                    onClick={() => formMethods.handleSubmit(submitForm)()}
                    label={"save"}
                  />
                </Stack>
              </Stack>
            </form>
            {formMethods.formState.isSubmitting && (
              <Box
                data-cy="form-saving-overlay"
                sx={theme => ({
                  position: "absolute",
                  inset: 0,
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  backgroundColor: alpha(theme.palette.background.content, 0.6),
                  zIndex: 1,
                })}>
                <CircularProgress />
              </Box>
            )}
          </Box>
        </FormProvider>
      )}
    </Stack>
  );
};

export default AdminDetailForm;
