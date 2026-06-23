import { useTranslation } from "react-i18next";
import { useCallback, useEffect, useState } from "react";
import { AvailablePipelinesResponse, Mandate, Organisation, PipelineSummary } from "../../../api/apiInterfaces";
import { useGeopilotAuth } from "../../../auth";
import { GridActionsCellItem, GridColDef, GridRenderCellParams, GridRowId } from "@mui/x-data-grid";
import { Tooltip } from "@mui/material";
import EditOutlinedIcon from "@mui/icons-material/EditOutlined";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutline";
import { useControlledNavigate } from "../../../components/controlledNavigate";
import GeopilotDataGrid from "../../../components/geopilotDataGrid.tsx";
import useFetch from "../../../hooks/useFetch.ts";
import { findPipeline, getLocalisedPipelineName } from "./pipelineDisplay";
import { FlexRowBox } from "../../../components/styledComponents.ts";

export const Mandates = () => {
  const { t, i18n } = useTranslation();
  const { user } = useGeopilotAuth();
  const { navigateTo } = useControlledNavigate();
  const [mandates, setMandates] = useState<Mandate[]>();
  const [pipelines, setPipelines] = useState<PipelineSummary[]>();
  const [isLoading, setIsLoading] = useState(true);
  const { fetchApi } = useFetch();

  const loadMandates = useCallback(() => {
    fetchApi<Mandate[]>("/api/v1/mandate", { errorMessageLabel: "mandatesLoadingError" })
      .then(setMandates)
      .finally(() => setIsLoading(false));
  }, [fetchApi]);

  const loadPipelines = useCallback(() => {
    fetchApi<AvailablePipelinesResponse>("/api/v1/pipeline", { errorMessageLabel: "pipelineLoadingError" })
      .then(response => setPipelines(response?.pipelines ?? []))
      // Resolve to an empty list on error so the loading gate clears instead of spinning forever.
      .catch(() => setPipelines([]));
  }, [fetchApi]);

  const startEditing = (id: GridRowId) => {
    navigateTo(`/admin/mandates/${id}`);
  };

  useEffect(() => {
    if (user?.isAdmin) {
      if (mandates === undefined) {
        loadMandates();
      }
      if (pipelines === undefined) {
        loadPipelines();
      }
    }
  }, [loadMandates, loadPipelines, mandates, pipelines, user?.isAdmin]);

  const columns: GridColDef[] = [
    {
      field: "name",
      headerName: t("name"),
      flex: 0.5,
      minWidth: 200,
    },
    {
      field: "pipelineId",
      headerName: t("pipeline"),
      flex: 0.5,
      minWidth: 200,
      renderCell: (params: GridRenderCellParams) => {
        const pipelineId = params.value as string | undefined;
        if (!pipelineId) {
          return "";
        }
        const pipeline = findPipeline(pipelines, pipelineId);
        if (pipeline) {
          return getLocalisedPipelineName(pipeline, i18n.language);
        }
        return (
          <Tooltip
            title={t("pipelineNotKnown", { pipelineId })}
            slotProps={{ popper: { modifiers: [{ name: "offset", options: { offset: [-20, -16] } }] } }}>
            <FlexRowBox sx={{ alignItems: "center", gap: 0.5, color: "error.main" }}>
              <ErrorOutlineIcon fontSize="small" />
              {pipelineId}
            </FlexRowBox>
          </Tooltip>
        );
      },
    },
    {
      field: "fileTypes",
      headerName: t("fileTypes"),
      flex: 1,
      minWidth: 200,
      valueGetter: (fileTypes: string[]) => {
        const sortedNames = fileTypes.sort();
        return sortedNames.join(", ");
      },
    },
    {
      field: "isPublic",
      headerName: t("public"),
      width: 160,
      type: "boolean",
    },
    {
      field: "organisations",
      headerName: t("organisations"),
      flex: 1,
      minWidth: 400,
      valueGetter: (organisations: Organisation[]) => {
        const sortedNames = [...organisations.map(o => o.name)].sort();
        return sortedNames.join(", ");
      },
    },
    {
      field: "actions",
      type: "actions",
      headerName: "",
      flex: 0,
      resizable: false,
      cellClassName: "actions",
      getActions: ({ id }) => [
        <Tooltip title={t("edit")} key={`edit-${id}`}>
          <GridActionsCellItem
            icon={<EditOutlinedIcon />}
            label={t("edit")}
            onClick={() => startEditing(id)}
            color="inherit"
          />
        </Tooltip>,
      ],
    },
  ];

  // Keep the grid in its loading state until the pipelines are loaded as well, otherwise the
  // pipeline column briefly renders valid pipelines as "unknown" while the list is still fetching.
  const isGridLoading = isLoading || pipelines === undefined;

  return (
    <GeopilotDataGrid
      name="mandates"
      addLabel="addMandate"
      loading={isGridLoading}
      rows={mandates}
      columns={columns}
      onSelect={startEditing}
    />
  );
};

export default Mandates;
