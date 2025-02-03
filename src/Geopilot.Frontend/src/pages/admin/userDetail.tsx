import { BaseButton } from "../../components/buttons.tsx";
import { FieldValues, FormProvider, useForm } from "react-hook-form";
import { CircularProgress, Stack, Typography } from "@mui/material";
import { FlexBox, FlexRowEndBox, FlexRowSpaceBetweenBox, GeopilotBox } from "../../components/styledComponents.ts";
import SaveOutlinedIcon from "@mui/icons-material/SaveOutlined";
import { useCallback, useContext, useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { PromptContext } from "../../components/prompt/promptContext.tsx";
import CancelOutlinedIcon from "@mui/icons-material/CancelOutlined";
import { ChevronLeft, UndoOutlined } from "@mui/icons-material";
import { useParams } from "react-router-dom";
import {
  FormAutocomplete,
  FormCheckbox,
  FormContainer,
  FormContainerHalfWidth,
  FormInput,
} from "../../components/form/form.ts";
import { Organisation, User } from "../../api/apiInterfaces.ts";
import { useApi } from "../../api";
import { useGeopilotAuth } from "../../auth";
import { useControlledNavigate } from "../../components/controlledNavigate";
import { PromptAction } from "../../components/prompt/promptInterfaces.ts";
import { FormAutocompleteValue } from "../../components/form/formAutocomplete.tsx";

export const UserDetail = () => {
  const { t } = useTranslation();
  const { user } = useGeopilotAuth();
  const formMethods = useForm({ mode: "all" });
  const { showPrompt } = useContext(PromptContext);
  const { fetchApi } = useApi();
  const { registerCheckIsDirty, unregisterCheckIsDirty, checkIsDirty, leaveEditingPage, navigateTo } =
    useControlledNavigate();
  const { id } = useParams<{
    id: string;
  }>();

  const [editableUser, setEditableUser] = useState<User>();
  const [organisations, setOrganisations] = useState<Organisation[]>();

  useEffect(() => {
    registerCheckIsDirty(`/admin/users/${id}`);

    return () => {
      unregisterCheckIsDirty(`/admin/users/${id}`);
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
              saveUser(formMethods.getValues() as User, false).then(() => leaveEditingPage(true));
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

  const loadUser = useCallback(() => {
    fetchApi<User>(`/api/v1/user/${id}`, { errorMessageLabel: "userLoadingError" }).then(setEditableUser);
  }, [fetchApi, id]);

  const loadOrganisations = useCallback(() => {
    fetchApi<Organisation[]>("/api/v1/organisation", { errorMessageLabel: "organisationsLoadingError" }).then(
      setOrganisations,
    );
  }, [fetchApi]);

  useEffect(() => {
    if (user?.isAdmin) {
      if (editableUser === undefined) {
        loadUser();
      }
      if (organisations === undefined) {
        loadOrganisations();
      }
    }
  }, [editableUser, organisations, user?.isAdmin, loadUser, loadOrganisations]);

  const saveUser = async (data: FieldValues, reloadAfterSave = true) => {
    if (id !== undefined) {
      const user = data as User;
      user.organisations = data["organisations"]?.map(
        (value: FormAutocompleteValue) => ({ id: value.key }) as Organisation,
      );
      user.id = parseInt(id);
      const response = await fetchApi("/api/v1/user", {
        method: "PUT",
        body: JSON.stringify(user),
        errorMessageLabel: "userSaveError",
      });
      const userResponse = response as User;
      if (reloadAfterSave) {
        setEditableUser(userResponse);
        formMethods.reset(userResponse);
      }
    }
  };

  const submitForm = (data: FieldValues) => {
    saveUser(data, true);
  };

  // trigger form validation on mount
  useEffect(() => {
    if (editableUser) {
      formMethods.trigger();
    }
  }, [editableUser, formMethods, formMethods.trigger]);

  const availableOrganisations = useMemo(() => {
    return organisations?.map(o => ({ key: o.id, name: o.name }));
  }, [organisations]);

  const selectedOrganisations = useMemo(() => {
    return editableUser?.organisations?.map(o => ({ key: o.id, name: o.name }));
  }, [editableUser?.organisations]);

  return (
    <FlexBox>
      <FlexRowSpaceBetweenBox>
        <BaseButton
          variant={"text"}
          icon={<ChevronLeft />}
          onClick={() => {
            navigateTo("/admin/users");
          }}
          label={"backToUsers"}
        />
        <Typography variant={"body2"}>{t("id") + ": " + id}</Typography>
      </FlexRowSpaceBetweenBox>
      {editableUser ? (
        <FormProvider {...formMethods}>
          <form onSubmit={formMethods.handleSubmit(submitForm)}>
            <FlexBox>
              <GeopilotBox>
                <Typography variant={"h3"} margin={0}>
                  {t("description")}
                </Typography>
                <FormContainer>
                  <FormInput fieldName={"fullName"} label={"name"} value={editableUser?.fullName} disabled={true} />
                  <FormInput fieldName={"email"} label={"email"} value={editableUser?.email} disabled={true} />
                </FormContainer>
                <FormContainerHalfWidth>
                  <FormCheckbox
                    fieldName={"isAdmin"}
                    label={"isAdmin"}
                    checked={editableUser?.isAdmin}
                    disabled={!user || user?.id === editableUser?.id}
                  />
                </FormContainerHalfWidth>
                <FormContainer>
                  <FormAutocomplete
                    fieldName={"organisations"}
                    label={"organisations"}
                    required={false}
                    values={availableOrganisations}
                    selected={selectedOrganisations}
                  />
                </FormContainer>
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

export default UserDetail;
