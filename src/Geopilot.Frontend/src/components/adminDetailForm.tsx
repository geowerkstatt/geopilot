import { ReactNode, useCallback, useContext, useEffect, useRef } from "react";
import { FieldValues, FormProvider, useForm } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router-dom";
import { ChevronLeft } from "@mui/icons-material";
import { CircularProgress, Stack, Typography } from "@mui/material";
import useFetch from "../hooks/useFetch.ts";
import { Button } from "./buttons.tsx";
import { useControlledNavigate } from "./controlledNavigate";
import { PromptContext } from "./prompt/promptContext.tsx";
import { PromptAction } from "./prompt/promptInterfaces.ts";

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
  const isSavingRef = useRef<boolean>(false);

  const saveData = useCallback(
    async (formData: FieldValues, reloadAfterSave = true) => {
      if (isSavingRef.current) {
        return;
      }
      isSavingRef.current = true;
      try {
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
          formMethods.reset(newFormData);

          if (id === 0) {
            const newPath = `${basePath}/${savedData.id}`;
            navigate(newPath, { replace: true });
            unregisterCheckIsDirty(`${basePath}/0`);
            registerCheckIsDirty(newPath);
          }
        }

        return savedData;
      } finally {
        isSavingRef.current = false;
      }
    },
    [
      apiEndpoint,
      basePath,
      fetchApi,
      formMethods,
      navigate,
      onSaveSuccess,
      prepareDataForSave,
      prepareDataAfterSave,
      registerCheckIsDirty,
      saveErrorLabel,
      unregisterCheckIsDirty,
    ],
  );

  const submitForm = (data: FieldValues) => {
    formMethods.trigger().then(isValid => {
      if (isValid) {
        saveData(data, true);
      }
    });
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
          <form onSubmit={formMethods.handleSubmit(submitForm)}>
            <Stack>
              {children}
              <Stack direction="row" sx={{ alignItems: "center", flexWrap: "wrap", justifyContent: "flex-end" }}>
                <Button
                  disabled={!formMethods.formState.isDirty}
                  onClick={() => formMethods.reset(data)}
                  label={"reset"}
                />
                <Button
                  variant="contained"
                  disabled={
                    isSavingRef.current ||
                    !formMethods.formState.isDirty ||
                    (formMethods.formState.errors && Object.keys(formMethods.formState.errors).length > 0)
                  }
                  onClick={() => formMethods.handleSubmit(submitForm)()}
                  label={"save"}
                />
              </Stack>
            </Stack>
          </form>
        </FormProvider>
      )}
    </Stack>
  );
};

export default AdminDetailForm;
