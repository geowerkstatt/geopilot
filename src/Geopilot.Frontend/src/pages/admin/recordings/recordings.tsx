import { FC, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate, useSearchParams } from "react-router-dom";
import VisibilityOutlinedIcon from "@mui/icons-material/VisibilityOutlined";
import { Box, Stack, Tooltip } from "@mui/material";
import {
  GridActionsCell,
  GridActionsCellItem,
  GridColDef,
  GridPaginationModel,
  GridRenderCellParams,
  GridRowId,
} from "@mui/x-data-grid";
import { RecordingOutcomeKind, RecordingPageResponse, RecordingSummaryResponse } from "../../../api/generated";
import { useCapabilities } from "../../../components/capabilities/capabilitiesInterface.ts";
import GeopilotDataGrid from "../../../components/grids/geopilotDataGrid.tsx";
import { UserOrClientLabel } from "../../../components/userOrClientLabel.tsx";
import useFetch from "../../../hooks/useFetch.ts";
import { useLocalized } from "../../../hooks/useLocalized.ts";
import { ProcessingStateLabel } from "./processingStateLabel.tsx";
import { RecordingDeliveryLabel } from "./recordingDeliveryLabel.tsx";
import { RecordingFilters } from "./recordingFilters.tsx";
import { formatDuration } from "./recordingFormat.ts";
import {
  listQueryState,
  readRecordingSearchParams,
  RecordingFilterValues,
  toRecordingQuery,
  toRecordingSearchParams,
} from "./recordingQuery.ts";

type RecordingRow = RecordingSummaryResponse & { attemptCount: number };

// The most runs the API returns per page, fewer than the grid fits on a very tall screen.
const maxPageSize = 100;

/** All recordings of the installation, newest first, paged and filtered on the server. */
export const Recordings: FC = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { fetchApi } = useFetch();
  const { localized } = useLocalized();
  const { machineDeliveryEnabled } = useCapabilities();
  // Filters and page live in the address, so going back from a run shows the same runs again.
  const [searchParams, setSearchParams] = useSearchParams();
  const { filters, page } = readRecordingSearchParams(searchParams);
  // The grid fits its page to its height like the other lists and reports the size once it has measured it. Until
  // then the size is 0, as in the grid itself, and nothing is loaded, so the list does not ask twice.
  const [pageSize, setPageSize] = useState(0);
  const [loaded, setLoaded] = useState<{ query: string; page?: RecordingPageResponse }>();

  const query = toRecordingQuery(filters, page, Math.min(pageSize, maxPageSize));
  // Loading lasts until the answer to the current query is there, so no effect has to switch it on.
  const isLoading = loaded?.query !== query;

  useEffect(() => {
    if (pageSize === 0) {
      return;
    }
    let current = true;
    fetchApi<RecordingPageResponse>(`/api/v1/recording?${query}`, {
      errorMessageLabel: "recordingsLoadingError",
    })
      .then(page => current && setLoaded({ query, page }))
      // useFetch has shown why; an empty page ends the loading.
      .catch(() => current && setLoaded({ query }));
    return () => {
      current = false;
    };
  }, [fetchApi, query, pageSize]);

  const openRecording = (jobId: GridRowId) =>
    navigate(`/admin/recordings/${jobId}`, { state: listQueryState(searchParams.toString()) });

  const changeFilters = (values: RecordingFilterValues) =>
    setSearchParams(toRecordingSearchParams(values, 0), { replace: true });

  const changePagination = (model: GridPaginationModel) => {
    setPageSize(model.pageSize);
    // The grid also reports a new page size when its height changes, possibly to the handler of an earlier render,
    // whose filters would undo the latest change.
    if (model.page !== page) {
      setSearchParams(toRecordingSearchParams(filters, model.page), { replace: true });
    }
  };

  const columns: GridColDef<RecordingRow>[] = [
    {
      field: "startedAt",
      headerName: t("processingStarted"),
      type: "dateTime",
      valueGetter: (startedAt: string) => new Date(startedAt),
      width: 180,
    },
    {
      field: "startedBy",
      headerName: t("processedBy"),
      valueGetter: (_value, row) => row.startedBy?.name ?? t("anonymous"),
      flex: 0.5,
      minWidth: 200,
      display: "flex",
      renderCell: ({ row }: GridRenderCellParams<RecordingRow>) => <UserOrClientLabel userOrClient={row.startedBy} />,
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
      renderCell: ({ row }: GridRenderCellParams<RecordingRow>) => (
        <ProcessingStateLabel
          state={row.terminalState}
          running={row.outcome.kind === RecordingOutcomeKind.Running}
          blockingSteps={row.outcome.blockingSteps}
          preflightFailed={row.outcome.preflightFailed}
        />
      ),
    },
    // Only a machine client sends the attempts of one delivery under one reference, so without them every run is one.
    ...(machineDeliveryEnabled
      ? [{ field: "attemptCount", headerName: t("attempts"), width: 100 } satisfies GridColDef<RecordingRow>]
      : []),
    {
      field: "outcome",
      headerName: t("delivery"),
      width: 160,
      display: "flex",
      renderCell: ({ row }: GridRenderCellParams<RecordingRow>) => <RecordingDeliveryLabel outcome={row.outcome} />,
    },
    {
      field: "duration",
      headerName: t("duration"),
      valueGetter: (_value, row) => formatDuration(row.startedAt, row.terminalAt) ?? t("durationOpen"),
      width: 120,
    },
    {
      field: "actions",
      type: "actions",
      headerName: "",
      flex: 0,
      resizable: false,
      cellClassName: "actions",
      renderCell: params => (
        <GridActionsCell {...params}>
          <GridActionsCellItem
            icon={
              <Tooltip title={t("showDetails")} key={`details-${params.id}`}>
                <VisibilityOutlinedIcon data-cy="details" color="primary" />
              </Tooltip>
            }
            label={t("showDetails")}
            onClick={() => openRecording(params.id)}
          />
        </GridActionsCell>
      ),
    },
  ];

  return (
    <Stack sx={{ height: "100%" }}>
      <RecordingFilters values={filters} onChange={changeFilters} onJobIdEntered={openRecording} />
      <Box sx={{ flex: "1 1 0", minHeight: 0 }}>
        <GeopilotDataGrid
          name="recordings"
          loading={isLoading}
          rows={(loaded?.page?.items ?? []).map(group => ({ ...group.latest, attemptCount: group.attemptCount }))}
          getRowId={row => row.jobId}
          columns={columns}
          paginationMode="server"
          rowCount={loaded?.page?.totalCount ?? 0}
          paginationModel={{ page, pageSize }}
          onPaginationModelChange={changePagination}
          disableColumnSorting
          disableColumnFilter
          onSelect={openRecording}
        />
      </Box>
    </Stack>
  );
};
