import { useTranslation } from "react-i18next";
import { useEffect, useState } from "react";
import { Mandate, Organisation, Validation } from "../../AppInterfaces.ts";
import { useAuth } from "../../auth";
import { AdminGrid } from "../../components/adminGrid/AdminGrid.tsx";
import { DataRow } from "../../components/adminGrid/AdminGridTypes.ts";
import { GridColDef } from "../../components/dataGrid/DataGridMultiSelectColumn.tsx";

export const Mandates = () => {
  const { t } = useTranslation();
  const { user } = useAuth();
  const [mandates, setMandates] = useState<Mandate[]>();
  const [organisations, setOrganisations] = useState<Organisation[]>();
  const [fileExtensions, setFileExtensions] = useState<string[]>();

  async function loadMandates() {
    try {
      const response = await fetch("/api/v1/mandate");
      if (response.ok) {
        const results = await response.json();
        setMandates(results);
      }
    } catch (error) {
      // TODO: Show error alert
    }
  }

  async function loadOrganisations() {
    try {
      const response = await fetch("/api/v1/organisation");
      if (response.ok) {
        const results = await response.json();
        setOrganisations(results);
      }
    } catch (error) {
      // TODO: Show error alert
    }
  }

  async function loadFileExtensions() {
    try {
      const response = await fetch("/api/v1/validation");
      if (response.ok) {
        const results: Validation = await response.json();
        setFileExtensions(results.allowedFileExtensions);
      }
    } catch (error) {
      // TODO: Show error alert
    }
  }

  async function onSave(row: DataRow) {
    console.log("onSave: ", row);
    // TODO: Save changes
  }

  async function onDisconnect(row: DataRow) {
    console.log("onDisconnect: ", row);
    const mandate = row as unknown as Mandate;
    mandate.organisations = [];
    // TODO: Save changes
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
