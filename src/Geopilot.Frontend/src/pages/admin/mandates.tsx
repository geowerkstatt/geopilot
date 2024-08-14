import { useTranslation } from "react-i18next";
import { useContext, useEffect, useState } from "react";
import { Mandate, Organisation } from "../../api/apiInterfaces";
import { Validation } from "../../appInterfaces";
import { useGeopilotAuth } from "../../auth";
import { AdminGrid } from "../../components/adminGrid/adminGrid";
import { DataRow, GridColDef } from "../../components/adminGrid/adminGridInterfaces";
import { AlertContext } from "../../components/alert/alertContext";
import { PromptContext } from "../../components/prompt/promptContext";
import { CircularProgress, Stack } from "@mui/material";
import { FetchMethod, runFetch } from "../../api/fetch.ts";

export const Mandates = () => {
  const { t } = useTranslation();
  const { user } = useGeopilotAuth();
  const [mandates, setMandates] = useState<Mandate[]>();
  const [organisations, setOrganisations] = useState<Organisation[]>();
  const [fileExtensions, setFileExtensions] = useState<string[]>();
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const { showAlert } = useContext(AlertContext);
  const { showPrompt } = useContext(PromptContext);

  useEffect(() => {
    if (mandates && organisations && fileExtensions) {
      setIsLoading(false);
    }
  }, [mandates, organisations, fileExtensions]);

  async function loadMandates() {
    await runFetch({
      url: "/api/v1/mandate",
      onSuccess: response => {
        setMandates(response as Mandate[]);
      },
      onError: (error: string) => {
        showAlert(t("mandatesLoadingError", { error: error }), "error");
      },
    });
  }

  async function loadOrganisations() {
    await runFetch({
      url: "/api/v1/organisation",
      onSuccess: response => {
        setOrganisations(response as Organisation[]);
      },
      onError: (error: string) => {
        showAlert(t("organisationsLoadingError", { error: error }), "error");
      },
    });
  }

  async function loadFileExtensions() {
    await runFetch({
      url: "/api/v1/validation",
      onSuccess: response => {
        setFileExtensions((response as Validation).allowedFileExtensions);
      },
      onError: (error: string) => {
        showAlert(t("fileTypesLoadingError", { error: error }), "error");
      },
    });
  }

  async function saveMandate(mandate: Mandate) {
    mandate.organisations = mandate.organisations?.map(organisationId => {
      return { id: organisationId as number } as Organisation;
    });
    await runFetch({
      url: "/api/v1/mandate",
      method: mandate.id === 0 ? FetchMethod.POST : FetchMethod.PUT,
      body: JSON.stringify(mandate),
      onSuccess: () => {
        loadMandates();
      },
      onError: (error: string) => {
        showAlert(t("mandateSaveError", { error: error }), "error");
      },
    });
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
      minWidth: 200,
    },
    {
      field: "fileTypes",
      headerName: t("fileTypes"),
      editable: true,
      flex: 1,
      minWidth: 200,
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
      minWidth: 400,
      type: "custom",
      valueOptions: organisations,
      getOptionLabel: (value: DataRow | string) => (value as Organisation).name,
      getOptionValue: (value: DataRow | string) => (value as Organisation).id,
    },
    {
      field: "coordinates",
      type: "custom",
      headerName: "",
      editable: true,
      width: 50,
      resizable: false,
      filterable: false,
      sortable: false,
      hideSortIcons: true,
      disableColumnMenu: true,
    },
  ];

  return isLoading ? (
    <Stack sx={{ flex: "1 0 0", justifyContent: "center", alignItems: "center", height: "100%" }}>
      <CircularProgress />
    </Stack>
  ) : (
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
