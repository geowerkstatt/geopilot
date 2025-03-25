import { FlexBox, FlexRowEndBox, FlexRowSpaceBetweenBox } from "./styledComponents.ts";
import { BaseButton } from "./buttons.tsx";
import { ChevronLeft, UndoOutlined } from "@mui/icons-material";
import SaveOutlinedIcon from "@mui/icons-material/SaveOutlined";
import { FieldValues, FormProvider, useForm } from "react-hook-form";
import { ReactNode, useCallback, useContext, useEffect, useRef } from "react";
import { PromptAction } from "./prompt/promptInterfaces.ts";
import CancelOutlinedIcon from "@mui/icons-material/CancelOutlined";
import { useControlledNavigate } from "./controlledNavigate";
import { PromptContext } from "./prompt/promptContext.tsx";
import { CircularProgress, Stack, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { useApi } from "../api";
import { useNavigate } from "react-router-dom";

interface AdminDetailFormProps<T> {
  basePath: string;
  backLabel: string;
  data: T | undefined;
  apiEndpoint: string;
  saveErrorLabel: string;
  prepareDataForSave: (data: FieldValues) => T;
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
  onSaveSuccess,
  children,
}: AdminDetailFormProps<T>) => {
  const { t } = useTranslation();
  const { fetchApi } = useApi();
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

      if (reloadAfterSave) {
        onSaveSuccess(savedData);
        formMethods.reset(savedData);

        if (id === 0) {
          const newPath = `${basePath}/${savedData.id}`;
          navigate(newPath, { replace: true });
          unregisterCheckIsDirty(`${basePath}/0`);
          registerCheckIsDirty(newPath);
        }
      }

      return savedData;
    },
    [
      apiEndpoint,
      basePath,
      fetchApi,
      formMethods,
      navigate,
      onSaveSuccess,
      prepareDataForSave,
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
            { label: "cancel", icon: <CancelOutlinedIcon />, action: () => leaveEditingPage(false) },
            {
              label: "reset",
              icon: <UndoOutlined />,
              action: () => leaveEditingPage(true),
            },
          ];
          if (isValid) {
            promptActions.push({
              label: "save",
              icon: <SaveOutlinedIcon />,
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
  }, [checkIsDirty]);

  useEffect(() => {
    if (data) {
      dataIdRef.current = data.id;
    }
  }, [data]);

  return (
    <FlexBox>
      <FlexRowSpaceBetweenBox>
        <BaseButton variant={"text"} icon={<ChevronLeft />} onClick={() => navigateTo(basePath)} label={backLabel} />
        {data && data.id !== 0 && <Typography variant={"body2"}>{t("id") + ": " + data?.id}</Typography>}
      </FlexRowSpaceBetweenBox>
      {!data ? (
        <Stack sx={{ flex: "1 0 0", justifyContent: "center", alignItems: "center", height: "100%" }}>
          <CircularProgress />
        </Stack>
      ) : (
        <FormProvider {...formMethods}>
          <form onSubmit={formMethods.handleSubmit(submitForm)}>
            <FlexBox>
              {children}
              <FlexRowEndBox>
                <BaseButton
                  icon={<UndoOutlined />}
                  variant={"outlined"}
                  disabled={!formMethods.formState.isDirty}
                  onClick={() => formMethods.reset(data)}
                  label={"reset"}
                />
                <BaseButton
                  icon={<SaveOutlinedIcon />}
                  disabled={
                    !formMethods.formState.isDirty ||
                    (formMethods.formState.errors && Object.keys(formMethods.formState.errors).length > 0)
                  }
                  onClick={() => formMethods.handleSubmit(submitForm)()}
                  label={"save"}
                />
              </FlexRowEndBox>
            </FlexBox>
          </form>
        </FormProvider>
      )}
    </FlexBox>
  );
};

export default AdminDetailForm;
