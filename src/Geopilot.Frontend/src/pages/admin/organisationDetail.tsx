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
import { FormAutocomplete, FormContainer, FormInput } from "../../components/form/form.ts";
import { Mandate, Organisation, User } from "../../api/apiInterfaces.ts";
import { useApi } from "../../api";
import { useGeopilotAuth } from "../../auth";
import { useControlledNavigate } from "../../components/controlledNavigate";
import { PromptAction } from "../../components/prompt/promptInterfaces.ts";
import { FormAutocompleteValue } from "../../components/form/formAutocomplete.tsx";

export const OrganisationDetail = () => {
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

  const [organisation, setOrganisation] = useState<Organisation>();
  const [mandates, setMandates] = useState<Mandate[]>();
  const [users, setUsers] = useState<User[]>();

  useEffect(() => {
    registerCheckIsDirty(`/admin/organisations/${id}`);

    return () => {
      unregisterCheckIsDirty(`/admin/organisations/${id}`);
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
              saveOrganisation(formMethods.getValues() as Mandate, false).then(() => leaveEditingPage(true));
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

  const loadOrganisation = useCallback(() => {
    if (id !== "0") {
      fetchApi<Organisation>(`/api/v1/organisation/${id}`, { errorMessageLabel: "organisationLoadingError" }).then(
        setOrganisation,
      );
    } else {
      setOrganisation({
        id: 0,
        name: "",
        mandates: [],
        users: [],
      });
    }
  }, [fetchApi, id]);

  const loadMandates = useCallback(() => {
    fetchApi<Mandate[]>("/api/v1/mandate", { errorMessageLabel: "mandatesLoadingError" }).then(setMandates);
  }, [fetchApi]);

  const loadUsers = useCallback(() => {
    fetchApi<User[]>("/api/v1/user", { errorMessageLabel: "usersLoadingError" }).then(setUsers);
  }, [fetchApi]);

  useEffect(() => {
    if (user?.isAdmin) {
      if (organisation === undefined) {
        loadOrganisation();
      }
      if (mandates === undefined) {
        loadMandates();
      }
      if (users === undefined) {
        loadUsers();
      }
    }
  }, [organisation, mandates, user?.isAdmin, users, loadOrganisation, loadMandates, loadUsers]);

  const saveOrganisation = async (data: FieldValues, reloadAfterSave = true) => {
    if (id !== undefined) {
      const organisation = data as Organisation;
      organisation.mandates = data["mandates"]?.map((value: FormAutocompleteValue) => ({ id: value.key }) as Mandate);
      organisation.users = data["users"]?.map((value: FormAutocompleteValue) => ({ id: value.key }) as User);
      organisation.id = parseInt(id);
      const response = await fetchApi("/api/v1/organisation", {
        method: organisation.id === 0 ? "POST" : "PUT",
        body: JSON.stringify(organisation),
        errorMessageLabel: "organisationSaveError",
      });
      const organisationResponse = response as Organisation;
      console.log("organisationResponse", organisationResponse);
      if (reloadAfterSave) {
        setOrganisation(organisationResponse);
        formMethods.reset(organisationResponse);
        if (id === "0") {
          navigate(`/admin/organisations/${organisationResponse.id}`, { replace: true });
        }
      }
    }
  };

  const submitForm = (data: FieldValues) => {
    saveOrganisation(data, true);
  };

  // trigger form validation on mount
  useEffect(() => {
    if (organisation) {
      formMethods.trigger();
    }
  }, [organisation, formMethods, formMethods.trigger]);

  return (
    <FlexBox>
      <FlexRowSpaceBetweenBox>
        <BaseButton
          variant={"text"}
          icon={<ChevronLeft />}
          onClick={() => {
            navigateTo("/admin/organisations");
          }}
          label={"backToOrganisations"}
        />
        {id !== "0" && <Typography variant={"body2"}>{t("id") + ": " + id}</Typography>}
      </FlexRowSpaceBetweenBox>
      {organisation ? (
        <FormProvider {...formMethods}>
          <form onSubmit={formMethods.handleSubmit(submitForm)}>
            <FlexBox>
              <GeopilotBox>
                <Typography variant={"h3"} margin={0}>
                  {t("description")}
                </Typography>
                <FormContainer>
                  <FormInput fieldName={"name"} label={"name"} value={organisation?.name} required={true} />
                </FormContainer>
                <FormContainer>
                  <FormAutocomplete
                    fieldName={"mandates"}
                    label={"mandates"}
                    required={false}
                    values={mandates?.map(mandate => ({ key: mandate.id, name: mandate.name }))}
                    selected={organisation?.mandates?.map(mandate => ({
                      key: (mandate as Mandate).id,
                      name: (mandate as Mandate).name,
                    }))}
                  />
                </FormContainer>
                <FormContainer>
                  <FormAutocomplete
                    fieldName={"users"}
                    label={"users"}
                    required={false}
                    values={users?.map(user => ({ key: user.id, name: user.fullName }))}
                    selected={organisation?.users?.map(user => ({
                      key: (user as User).id,
                      name: (user as User).fullName,
                    }))}
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

export default OrganisationDetail;
