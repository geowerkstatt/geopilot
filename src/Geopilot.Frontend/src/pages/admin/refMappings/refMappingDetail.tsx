import { useCallback, useContext, useEffect, useState } from "react";
import { FieldValues } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useNavigate, useParams } from "react-router-dom";
import { Typography } from "@mui/material";
import { ApiError } from "../../../api/apiInterfaces.ts";
import { RefMapping } from "../../../api/generated/index.ts";
import AdminDetailForm from "../../../components/adminDetailForm.tsx";
import { AlertContext } from "../../../components/alert/alertContext.tsx";
import { Button } from "../../../components/buttons.tsx";
import { FormChipInput, FormContainer, FormInput } from "../../../components/form/form.ts";
import { PromptContext } from "../../../components/prompt/promptContext.tsx";
import { PromptAction } from "../../../components/prompt/promptInterfaces.ts";
import { GeopilotBox } from "../../../components/styledComponents.ts";
import useFetch from "../../../hooks/useFetch.ts";

const newRefMapping: RefMapping = {
  id: 0,
  scope: "",
  refDataUris: [],
  comment: "",
};

const parseRefDataUri = (input: string) => {
  const value = input.trim() || undefined;
  return value?.startsWith("ilidata:") ? value : undefined;
};

export const RefMappingDetail = () => {
  const { t } = useTranslation();
  const { fetchApi } = useFetch();
  const { id = "0" } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { showPrompt } = useContext(PromptContext);
  const { showAlert } = useContext(AlertContext);

  const [loadedRefMapping, setLoadedRefMapping] = useState<RefMapping>();

  const refMapping = id === "0" ? newRefMapping : loadedRefMapping;

  const loadRefMapping = useCallback(
    (id: string) => {
      fetchApi<RefMapping>(`/api/v1/refmapping/${id}`, { errorMessageLabel: "refMappingLoadingError" }).then(
        setLoadedRefMapping,
      );
    },
    [fetchApi],
  );

  useEffect(() => {
    if (id !== "0") {
      loadRefMapping(id);
    }
  }, [id, loadRefMapping]);

  const prepareForSave = (formData: FieldValues): RefMapping => {
    return formData as RefMapping;
  };

  const confirmDelete = useCallback(
    (id: number) => {
      const deleteRefMapping = () =>
        fetchApi(`/api/v1/refmapping/${id}`, { method: "DELETE" })
          .then(() => {
            navigate("/admin/refmappings");
          })
          .catch((error: ApiError) => {
            if (error.status === 404) {
              showAlert(t("refMappingDeleteIdNotExistError", { id: id }), "error");
            } else if (error.status === 500) {
              showAlert(t("refMappingDeleteIdError", { id: id }), "error");
            } else {
              showAlert(t("refMappingDeleteError", { error: error }), "error");
            }
          });

      const actions: PromptAction[] = [
        { label: "cancel" },
        { label: "delete", action: deleteRefMapping, color: "error", variant: "contained" },
      ];
      showPrompt("deleteRefMappingConfirmation", actions);
    },
    [fetchApi, navigate, showAlert, showPrompt, t],
  );

  const deleteButton = refMapping && refMapping.id !== 0 && (
    <Button variant="outlined" color="error" label="delete" onClick={() => confirmDelete(refMapping.id)} />
  );

  return (
    <AdminDetailForm<RefMapping>
      basePath="/admin/refmappings"
      backLabel="backToRefMappings"
      data={refMapping}
      apiEndpoint="/api/v1/refmapping"
      saveErrorLabel="refMappingSaveError"
      prepareDataForSave={prepareForSave}
      additionalButtons={deleteButton}>
      <GeopilotBox>
        <Typography variant={"h3"} sx={{ margin: 0 }}>
          {t("mapping")}
        </Typography>
        <FormContainer>
          <FormInput fieldName={"scope"} label={"scope"} value={refMapping?.scope} required />
        </FormContainer>
        <FormContainer>
          <FormChipInput
            fieldName={"refDataUris"}
            label={"refData"}
            required
            selected={refMapping?.refDataUris}
            parse={parseRefDataUri}
            errorMessage="refMappingRequiresIlidata"
          />
        </FormContainer>
        <FormContainer>
          <FormInput
            fieldName={"comment"}
            label={"comment"}
            value={refMapping?.comment ?? undefined}
            multiline
            minRows={3}
          />
        </FormContainer>
      </GeopilotBox>
    </AdminDetailForm>
  );
};
