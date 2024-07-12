import { useTranslation } from "react-i18next";
import { useContext, useEffect, useState } from "react";
import { ErrorResponse, Mandate, Organisation, Validation } from "../../AppInterfaces.ts";
import { useAuth } from "../../auth";
import { AdminGrid } from "../../components/adminGrid/AdminGrid.tsx";
import { DataRow } from "../../components/adminGrid/AdminGridTypes.ts";
import { GridColDef } from "../../components/dataGrid/DataGridMultiSelectColumn.tsx";
import { AlertContext } from "../../components/alert/AlertContext.tsx";
import { PromptContext } from "../../components/prompt/PromptContext.tsx";

export const Mandates = () => {
  const { t } = useTranslation();
  const { user } = useAuth();
  const [mandates, setMandates] = useState<Mandate[]>();
  const [organisations, setOrganisations] = useState<Organisation[]>();
  const [fileExtensions, setFileExtensions] = useState<string[]>();
  const { showAlert } = useContext(AlertContext);
  const { showPrompt } = useContext(PromptContext);

  async function loadMandates() {
    try {
      const response = await fetch("/api/v1/mandate");
      if (response.ok) {
        const results = await response.json();
        setMandates(results);
      } else {
        const errorResponse: ErrorResponse = await response.json();
        showAlert(t("mandatesLoadingError", { error: errorResponse.detail }), "error");
      }
    } catch (error) {
      showAlert(t("mandatesLoadingError", { error: error }), "error");
    }
  }

  async function loadOrganisations() {
    try {
      const response = await fetch("/api/v1/organisation");
      if (response.ok) {
        const results = await response.json();
        setOrganisations(results);
      } else {
        const errorResponse: ErrorResponse = await response.json();
        showAlert(t("organisationsLoadingError", { error: errorResponse.detail }), "error");
      }
    } catch (error) {
      showAlert(t("organisationsLoadingError", { error: error }), "error");
    }
  }

  async function loadFileExtensions() {
    try {
      const response = await fetch("/api/v1/validation");
      if (response.ok) {
        const results: Validation = await response.json();
        setFileExtensions(results.allowedFileExtensions);
      } else {
        const errorResponse: ErrorResponse = await response.json();
        showAlert(t("fileTypesLoadingError", { error: errorResponse.detail }), "error");
      }
    } catch (error) {
      showAlert(t("fileTypesLoadingError", { error: error }), "error");
    }
  }

  async function saveMandate(mandate: Mandate) {
    try {
      const response = await fetch("/api/v1/mandate", {
        method: mandate.id === 0 ? "POST" : "PUT",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify(mandate),
      });

      if (response.ok) {
        loadMandates();
      } else {
        const errorResponse: ErrorResponse = await response.json();
        showAlert(t("mandateSaveError", { error: errorResponse.detail }), "error");
      }
    } catch (error) {
      showAlert(t("mandateSaveError", { error: error }), "error");
    }
  }

  async function onSave(row: DataRow) {
    await saveMandate(row as Mandate);
  }

  async function onDisconnect(row: DataRow) {
    showPrompt(t("mandateDisconnectTitle"), t("mandateDisconnectMessage"), [
      { label: t("cancel") },
      {
        label: t("disconnect"),
        action: () => {
          const mandate = row as unknown as Mandate;
          mandate.organisations = [];
          saveMandate(mandate);
        },
        color: "error",
        variant: "contained",
      },
    ]);
  }

  useEffect(() => {
    if (user?.isAdmin) {
      if (mandates === undefined) {
        loadMandates();
      }
      if (organisations === undefined) {
        loadOrganisations();
      }
      if (fileExtensions === undefined) {
        loadFileExtensions();
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const columns: GridColDef[] = [
    {
      field: "name",
      headerName: t("name"),
      type: "string",
      editable: true,
      flex: 0.5,
    },
    {
      field: "fileTypes",
      headerName: t("fileTypes"),
      editable: true,
      flex: 1,
      type: "custom",
      valueOptions: fileExtensions,
      getOptionLabel: (value: DataRow | string) => value as string,
      getOptionValue: (value: DataRow | string) => value as string,
    },
    {
      field: "organisations",
      headerName: t("organisations"),
      editable: true,
      flex: 1,
      type: "custom",
      valueOptions: organisations,
      getOptionLabel: (value: DataRow | string) => (value as Organisation).name,
      getOptionValue: (value: DataRow | string) => (value as Organisation).id,
    },
  ];

  return (
    <AdminGrid
      addLabel="addMandate"
      data={mandates as unknown as DataRow[]}
      columns={columns}
      onSave={onSave}
      onDisconnect={onDisconnect}
    />
  );
};

export default Mandates;
