import { FC, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate, useSearchParams } from "react-router-dom";
import { Box, Stack } from "@mui/material";
import { GridColDef, GridPaginationModel, GridRenderCellParams } from "@mui/x-data-grid";
import { PipelineRunOutcomeKind, PipelineRunPageResponse } from "../../../api/generated";
import GeopilotDataGrid from "../../../components/grids/geopilotDataGrid.tsx";
import useFetch from "../../../hooks/useFetch.ts";
import { useLocalized } from "../../../hooks/useLocalized.ts";
import { PipelineRunAttempts } from "./pipelineRunAttempts.tsx";
import { PipelineRunFilters } from "./pipelineRunFilters.tsx";
import { formatDeclarer, formatDuration } from "./pipelineRunFormat.ts";
import { PipelineRunOutcomeLabel } from "./pipelineRunOutcomeLabel.tsx";
import {
  PipelineRunFilterValues,
  readPipelineRunSearchParams,
  toPipelineRunQuery,
  toPipelineRunSearchParams,
} from "./pipelineRunQuery.ts";
import { PipelineRunRow, toPipelineRunRows } from "./pipelineRunRows.ts";
import { ProcessingStateLabel } from "./processingStateLabel.tsx";

/** All pipeline runs of the installation, newest first, paged and filtered on the server. */
export const PipelineRuns: FC = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { fetchApi } = useFetch();
  const { localized } = useLocalized();
  // Filters and page live in the address, so going back from a run shows the same runs again.
  const [searchParams, setSearchParams] = useSearchParams();
  const { filters, page } = readPipelineRunSearchParams(searchParams);
  const [pageSize, setPageSize] = useState(25);
  const [loaded, setLoaded] = useState<{ query: string; page?: PipelineRunPageResponse }>();
  // The deliveries that show their earlier attempts, by the job id of their newest attempt.
  const [expanded, setExpanded] = useState<ReadonlySet<string>>(new Set());

  const query = toPipelineRunQuery(filters, page, pageSize);
  // Loading lasts until the answer to the current query is there, so no effect has to switch it on.
  const isLoading = loaded?.query !== query;

  useEffect(() => {
    let current = true;
    fetchApi<PipelineRunPageResponse>(`/api/v1/pipelinerun?${query}`, { errorMessageLabel: "pipelineRunsLoadingError" })
      .then(page => current && setLoaded({ query, page }))
      // useFetch has shown why; an empty page ends the loading.
      .catch(() => current && setLoaded({ query }));
    return () => {
      current = false;
    };
  }, [fetchApi, query]);

  const changeFilters = (values: PipelineRunFilterValues) =>
    setSearchParams(toPipelineRunSearchParams(values, 0), { replace: true });

  const changePagination = (model: GridPaginationModel) => {
    setPageSize(model.pageSize);
    setSearchParams(toPipelineRunSearchParams(filters, model.page), { replace: true });
  };

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

  const columns: GridColDef<PipelineRunRow>[] = [
    {
      field: "attempts",
      headerName: t("attempts"),
      width: 100,
      display: "flex",
      renderCell: ({ row }: GridRenderCellParams<PipelineRunRow>) => (
        <PipelineRunAttempts row={row} expanded={expanded.has(row.jobId)} onToggle={() => toggleExpanded(row.jobId)} />
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
      valueGetter: (_value, row) => formatDeclarer(t, row.declarer, row.clientKind),
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
      headerName: t("state"),
      width: 210,
      display: "flex",
      renderCell: ({ row }: GridRenderCellParams<PipelineRunRow>) => (
        <ProcessingStateLabel state={row.terminalState} running={row.outcome.kind === PipelineRunOutcomeKind.Running} />
      ),
    },
    {
      field: "outcome",
      headerName: t("outcome"),
      flex: 0.6,
      minWidth: 260,
      display: "flex",
      renderCell: ({ row }: GridRenderCellParams<PipelineRunRow>) => <PipelineRunOutcomeLabel outcome={row.outcome} />,
    },
    {
      field: "duration",
      headerName: t("duration"),
      valueGetter: (_value, row) => formatDuration(row.startedAt, row.terminalAt) ?? t("durationOpen"),
      width: 120,
    },
  ];

  return (
    <Stack sx={{ gap: 2, height: "100%" }}>
      <PipelineRunFilters
        values={filters}
        onChange={changeFilters}
        onJobIdEntered={jobId => navigate(`/admin/processing-runs/${jobId}`)}
      />
      <Box sx={{ flex: "1 1 0", minHeight: 0 }}>
        <GeopilotDataGrid
          name="pipelineRuns"
          loading={isLoading}
          rows={toPipelineRunRows(loaded?.page?.items ?? [], expanded)}
          getRowId={row => row.jobId}
          columns={columns}
          paginationMode="server"
          rowCount={loaded?.page?.totalCount ?? 0}
          paginationModel={{ page, pageSize }}
          onPaginationModelChange={changePagination}
          disableColumnSorting
          disableColumnFilter
          onSelect={jobId => navigate(`/admin/processing-runs/${jobId}`)}
        />
      </Box>
    </Stack>
  );
};
