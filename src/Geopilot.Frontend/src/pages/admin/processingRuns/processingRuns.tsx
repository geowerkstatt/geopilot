import { FC, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate, useSearchParams } from "react-router-dom";
import { Box, Stack } from "@mui/material";
import { GridColDef, GridPaginationModel, GridRenderCellParams } from "@mui/x-data-grid";
import { PipelineRunOutcomeKind, PipelineRunPageResponse } from "../../../api/generated";
import GeopilotDataGrid from "../../../components/grids/geopilotDataGrid.tsx";
import { ReactionStateLabel } from "../../../components/grids/reactionStateLabel.tsx";
import useFetch from "../../../hooks/useFetch.ts";
import { useLocalized } from "../../../hooks/useLocalized.ts";
import { ProcessingRunAttempts } from "./processingRunAttempts.tsx";
import { ProcessingRunDeliveryLabel } from "./processingRunDeliveryLabel.tsx";
import { ProcessingRunFilters } from "./processingRunFilters.tsx";
import { formatDuration } from "./processingRunFormat.ts";
import {
  listQueryState,
  ProcessingRunFilterValues,
  readProcessingRunSearchParams,
  toProcessingRunQuery,
  toProcessingRunSearchParams,
} from "./processingRunQuery.ts";
import { ProcessingRunRow, toProcessingRunRows } from "./processingRunRows.ts";
import { ProcessingStateLabel } from "./processingStateLabel.tsx";

// Fixed rather than fitted to the height of the grid: a page in the address then names the same runs on every screen,
// and the grid has no size of its own to ask a second time with.
const pageSize = 25;

/** All pipeline runs of the installation, newest first, paged and filtered on the server. */
export const ProcessingRuns: FC = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { fetchApi } = useFetch();
  const { localized } = useLocalized();
  // Filters and page live in the address, so going back from a run shows the same runs again.
  const [searchParams, setSearchParams] = useSearchParams();
  const { filters, page } = readProcessingRunSearchParams(searchParams);
  const [loaded, setLoaded] = useState<{ query: string; page?: PipelineRunPageResponse }>();
  // The deliveries that show their earlier attempts, by the job id of their newest attempt.
  const [expanded, setExpanded] = useState<ReadonlySet<string>>(new Set());

  const query = toProcessingRunQuery(filters, page, pageSize);
  // Loading lasts until the answer to the current query is there, so no effect has to switch it on.
  const isLoading = loaded?.query !== query;

  useEffect(() => {
    let current = true;
    fetchApi<PipelineRunPageResponse>(`/api/v1/pipelinerun?${query}`, {
      errorMessageLabel: "processingRunsLoadingError",
    })
      .then(page => current && setLoaded({ query, page }))
      // useFetch has shown why; an empty page ends the loading.
      .catch(() => current && setLoaded({ query }));
    return () => {
      current = false;
    };
  }, [fetchApi, query]);

  const changeFilters = (values: ProcessingRunFilterValues) =>
    setSearchParams(toProcessingRunSearchParams(values, 0), { replace: true });

  const changePagination = (model: GridPaginationModel) =>
    setSearchParams(toProcessingRunSearchParams(filters, model.page), { replace: true });

  const toggleExpanded = (jobId: string) =>
    setExpanded(current => {
      const next = new Set(current);
      if (next.has(jobId)) {
        next.delete(jobId);
      } else {
        next.add(jobId);
      }
      return next;
    });

  const columns: GridColDef<ProcessingRunRow>[] = [
    {
      field: "attempts",
      headerName: t("attempts"),
      width: 100,
      display: "flex",
      renderCell: ({ row }: GridRenderCellParams<ProcessingRunRow>) => (
        <ProcessingRunAttempts
          row={row}
          expanded={expanded.has(row.jobId)}
          onToggle={() => toggleExpanded(row.jobId)}
        />
      ),
    },
    {
      field: "startedAt",
      headerName: t("processingStarted"),
      type: "dateTime",
      valueGetter: (startedAt: string) => new Date(startedAt),
      width: 180,
    },
    {
      field: "declarer",
      headerName: t("processedBy"),
      valueGetter: (_value, row) => row.declarer?.name ?? t("anonymous"),
      flex: 0.5,
      minWidth: 200,
    },
    {
      field: "mandate",
      headerName: t("mandate"),
      valueGetter: (_value, row) => (row.mandate ? localized(row.mandate.name) : ""),
      flex: 0.5,
      minWidth: 200,
    },
    {
      field: "terminalState",
      headerName: t("processingState"),
      flex: 0.6,
      minWidth: 260,
      display: "flex",
      renderCell: ({ row }: GridRenderCellParams<ProcessingRunRow>) => (
        <ProcessingStateLabel
          state={row.terminalState}
          running={row.outcome.kind === PipelineRunOutcomeKind.Running}
          blockingSteps={row.outcome.blockingSteps}
        />
      ),
    },
    {
      field: "outcome",
      headerName: t("delivery"),
      width: 160,
      display: "flex",
      renderCell: ({ row }: GridRenderCellParams<ProcessingRunRow>) => (
        <ProcessingRunDeliveryLabel outcome={row.outcome} />
      ),
    },
    {
      field: "forwardingState",
      headerName: t("forwarding"),
      width: 160,
      display: "flex",
      renderCell: ({ row }: GridRenderCellParams<ProcessingRunRow>) =>
        row.outcome.forwardingState && <ReactionStateLabel state={row.outcome.forwardingState} />,
    },
    {
      field: "duration",
      headerName: t("duration"),
      valueGetter: (_value, row) => formatDuration(row.startedAt, row.terminalAt) ?? t("durationOpen"),
      width: 120,
    },
  ];

  return (
    <Stack sx={{ height: "100%" }}>
      <ProcessingRunFilters
        values={filters}
        onChange={changeFilters}
        onJobIdEntered={jobId =>
          navigate(`/admin/processing-runs/${jobId}`, { state: listQueryState(searchParams.toString()) })
        }
      />
      <Box sx={{ flex: "1 1 0", minHeight: 0 }}>
        <GeopilotDataGrid
          name="processingRuns"
          loading={isLoading}
          rows={toProcessingRunRows(loaded?.page?.items ?? [], expanded)}
          getRowId={row => row.jobId}
          columns={columns}
          paginationMode="server"
          autoPageSize={false}
          pageSizeOptions={[pageSize]}
          rowCount={loaded?.page?.totalCount ?? 0}
          paginationModel={{ page, pageSize }}
          onPaginationModelChange={changePagination}
          disableColumnSorting
          disableColumnFilter
          onSelect={jobId =>
            navigate(`/admin/processing-runs/${jobId}`, { state: listQueryState(searchParams.toString()) })
          }
        />
      </Box>
    </Stack>
  );
};
