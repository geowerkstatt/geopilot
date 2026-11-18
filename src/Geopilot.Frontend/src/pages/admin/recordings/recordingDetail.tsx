import { FC, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link as RouterLink, useLocation, useNavigate, useParams } from "react-router-dom";
import { Box, CircularProgress, Link, Stack, SxProps, Tab, Tabs, Theme, Typography } from "@mui/material";
import { ReactionState, RecordingOutcomeKind, RecordingResponse } from "../../../api/generated";
import { NEOGEO_FONTWEIGHT_BOLD, NEOGEO_FONTWEIGHT_REGULAR, px2rem } from "../../../appTheme.ts";
import { AdminDetailHeader } from "../../../components/adminDetailHeader.tsx";
import { useCapabilities } from "../../../components/capabilities/capabilitiesInterface.ts";
import { GeopilotBox } from "../../../components/styledComponents.ts";
import { UserOrClientLabel } from "../../../components/userOrClientLabel.tsx";
import useFetch from "../../../hooks/useFetch.ts";
import { useLocalized } from "../../../hooks/useLocalized.ts";
import { SummaryField } from "../deliveries/summaryField.tsx";
import { PipelineSnapshot } from "./pipelineSnapshot.tsx";
import { ProcessingStateLabel } from "./processingStateLabel.tsx";
import { RecordingAttemptList } from "./recordingAttemptList.tsx";
import { RecordingDeliveryLabel } from "./recordingDeliveryLabel.tsx";
import { formatDuration } from "./recordingFormat.ts";
import { listQueryOf, listQueryState, parseJobId, RecordingTab, tabFromHash } from "./recordingQuery.ts";
import { RecordingReactionEvents } from "./recordingReactionEvents.tsx";
import { RecordingSteps } from "./recordingSteps.tsx";

// The tab row overlaps the top border of the box by one pixel, so the selected tab can cover it and join the box.
const folderTabsSx: SxProps<Theme> = theme => ({
  position: "relative",
  zIndex: 1,
  marginBottom: "-1px",
  minHeight: 0,
  "& .MuiTab-root": {
    ...theme.typography.h3,
    fontSize: px2rem(18),
    fontWeight: NEOGEO_FONTWEIGHT_REGULAR,
    margin: 0,
    minHeight: 0,
    padding: theme.spacing(1, 2),
    color: theme.palette.text.secondary,
    backgroundColor: theme.palette.primary.states.hover,
    border: `1px solid ${theme.palette.primary.light}`,
    borderBottomColor: "transparent",
    borderTopLeftRadius: theme.radius.default,
    borderTopRightRadius: theme.radius.default,
    // Keeps the hover background off the transparent bottom border, where the top border of the box shows through.
    backgroundClip: "padding-box",
  },
  // An invisible copy of the label in the selected style keeps a tab as wide as it becomes once selected.
  "& .MuiTab-root::after": {
    content: "attr(data-label)",
    display: "block",
    height: 0,
    overflow: "hidden",
    visibility: "hidden",
    fontSize: px2rem(20),
    fontWeight: NEOGEO_FONTWEIGHT_BOLD,
  },
  "& .MuiTabs-list": {
    gap: theme.spacing(1),
  },
  "& .MuiTab-root:hover": {
    color: theme.palette.text.primary,
    backgroundColor: theme.palette.primary.states.selected,
  },
  "& .MuiTab-root.Mui-selected": {
    fontSize: px2rem(20),
    fontWeight: NEOGEO_FONTWEIGHT_BOLD,
    color: theme.palette.text.primary,
    backgroundColor: theme.palette.background.content,
    borderBottomColor: theme.palette.background.content,
  },
});

const listPath = "/admin/recordings";
const panelId = "recordingPanel";
// How often the page loads the recording again while a reaction of its delivery is pending or running.
const reactionRefreshInterval = 15_000;

/**
 * One recording: who started its job, the other attempts of its delivery, what became of it, its preparation with the
 * uploaded files, its steps, reactions and pipeline.
 */
export const RecordingDetail: FC = () => {
  const { t } = useTranslation();
  const { jobId: jobIdParam } = useParams<{ jobId: string }>();
  const navigate = useNavigate();
  const location = useLocation();
  const { fetchApi } = useFetch();
  const { localized } = useLocalized();
  const { machineDeliveryEnabled } = useCapabilities();
  const [loaded, setLoaded] = useState<{ jobId: string; run?: RecordingResponse }>();

  // The address may spell the id in capitals or without hyphens; the API answers in one form only.
  const jobId = parseJobId(jobIdParam ?? "");

  const [loadCount, setLoadCount] = useState(0);

  useEffect(() => {
    if (!jobId) {
      return;
    }
    let current = true;
    fetchApi<RecordingResponse>(`/api/v1/recording/${jobId}`, { errorMessageLabel: "recordingLoadingError" })
      .then(run => current && setLoaded({ jobId, run }))
      // useFetch has shown why. A run already shown stays when loading it again fails, as a new object, so that the
      // page loads it again later while reactions are open; otherwise the page says that there is no such run instead
      // of waiting.
      .catch(() => current && setLoaded(previous => (previous?.jobId === jobId ? { ...previous } : { jobId })));
    return () => {
      current = false;
    };
  }, [fetchApi, jobId, loadCount]);

  // shortcut: polls the whole protocol; a call for the states of the reactions alone if the steps make it slow.
  const shownRun = loaded?.jobId === jobId ? loaded?.run : undefined;
  const reactionsOpen = [shownRun?.forwardingState, shownRun?.notificationState].some(
    state => state === ReactionState.Pending || state === ReactionState.Running,
  );
  useEffect(() => {
    if (!reactionsOpen) {
      return;
    }
    const timer = setTimeout(() => setLoadCount(count => count + 1), reactionRefreshInterval);
    return () => clearTimeout(timer);
  }, [reactionsOpen, loaded]);

  const tab = tabFromHash(location.hash);
  // Switching tabs replaces the entry, so the new entry keeps the query of the list the run was opened from.
  const listQuery = listQueryOf(location.state);
  const showTab = (value: RecordingTab) =>
    navigate({ hash: value }, { replace: true, state: listQueryState(listQuery) });

  // Back shows the list with the filters and page it was left with; opened from anywhere else, the whole list.
  const goBack = () => navigate({ pathname: listPath, search: listQuery });
  const renderHeader = (jobId?: string) => (
    <AdminDetailHeader backLabel="backToRecordings" onBack={goBack} id={jobId} idLabel="jobId" />
  );
  const header = renderHeader();

  // Following a link to another run keeps the page mounted, so the previous run must not show meanwhile.
  const result = jobId && loaded?.jobId === jobId ? loaded : undefined;
  if (!jobId || (result && !result.run)) {
    return (
      <Stack>
        {header}
        <GeopilotBox>
          <Typography data-cy="recordingNotFound">{t("recordingNotFound")}</Typography>
        </GeopilotBox>
      </Stack>
    );
  }

  const run = result?.run;
  if (!run) {
    return (
      <Stack>
        {header}
        <Stack sx={{ flex: "1 0 0", justifyContent: "center", alignItems: "center", height: "100%" }}>
          <CircularProgress />
        </Stack>
      </Stack>
    );
  }

  const duration = formatDuration(run.startedAt, run.terminalAt);
  // Without machine delivery the pages of the machine clients are not routed.
  const startedByPath =
    run.startedBy?.userId != null
      ? `/admin/users/${run.startedBy.userId}`
      : run.startedBy?.machineClientId != null && machineDeliveryEnabled
        ? `/admin/machine-clients/${run.startedBy.machineClientId}`
        : undefined;
  // Reactions wait without events until their first attempt, so a delivery with reactions offers the tab from the start.
  const hasReactions = run.reactionEvents.length > 0 || !!run.forwardingState || !!run.notificationState;
  // A run without reactions offers no tab for them, so the page must not stay on it.
  const visibleTab = tab === "reactions" && !hasReactions ? "steps" : tab;
  const tabProps = (value: RecordingTab, label: string) => ({
    value,
    label,
    id: `recordingTab-${value}`,
    "aria-controls": panelId,
    "data-cy": `recordingTab-${value}`,
    "data-label": label,
  });

  return (
    <Stack>
      {renderHeader(run.jobId)}
      {run.attempts.length > 1 && (
        <GeopilotBox>
          <Typography variant="h3" sx={{ marginTop: 0, marginBottom: 0 }}>
            {t("attemptsOfClientReference", { clientReference: run.clientReference })}
          </Typography>
          <RecordingAttemptList attempts={run.attempts} shownJobId={run.jobId} />
        </GeopilotBox>
      )}
      <GeopilotBox>
        <Typography variant="h3" sx={{ marginTop: 0, marginBottom: 0 }}>
          {t("summary")}
        </Typography>
        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "1fr 1fr" }, gap: 2 }}>
          <SummaryField label="processingState">
            <ProcessingStateLabel
              state={run.terminalState}
              running={run.outcome.kind === RecordingOutcomeKind.Running}
              blockingSteps={run.outcome.blockingSteps}
              preflightFailed={run.outcome.preflightFailed}
              allSteps
              duration={duration}
            />
          </SummaryField>
          <SummaryField label="processedBy">
            <UserOrClientLabel userOrClient={run.startedBy} path={startedByPath} />
          </SummaryField>
          {run.clientReference && <SummaryField label="clientReference">{run.clientReference}</SummaryField>}
          <SummaryField label="delivery">
            <RecordingDeliveryLabel outcome={run.outcome} linkDelivery />
            {run.outcome.kind === RecordingOutcomeKind.DeliveryFailed && (
              <Typography variant="body2" sx={{ color: "text.secondary" }} data-cy="recordingDeliveryFailure">
                {run.deliveryFailure}
              </Typography>
            )}
            {/* Runs recorded before the protocol kept the permission can only guess why there is no delivery. */}
            {run.outcome.kind === RecordingOutcomeKind.NotDelivered && run.permissionToDeliver == null && (
              <Typography variant="body2" sx={{ color: "text.secondary" }}>
                {t("recordingNotDeliveredHint")}
              </Typography>
            )}
          </SummaryField>
          <SummaryField label="mandate">
            {run.mandate ? (
              <Link component={RouterLink} to={`/admin/mandates/${run.mandate.id}`}>
                {localized(run.mandate.name)}
              </Link>
            ) : (
              run.mandateId
            )}
          </SummaryField>
          <SummaryField label="pipeline">{run.pipelineId}</SummaryField>
          <SummaryField label="processingStarted">{new Date(run.startedAt).toLocaleString()}</SummaryField>
          <SummaryField label="appVersion">{run.appVersion}</SummaryField>
        </Box>
      </GeopilotBox>
      <Stack spacing={0}>
        <Tabs
          value={visibleTab}
          onChange={(_event, value: RecordingTab) => showTab(value)}
          variant="scrollable"
          scrollButtons={false}
          sx={folderTabsSx}
          slotProps={{ indicator: { sx: { display: "none" } } }}>
          <Tab {...tabProps("steps", t("processingSteps"))} />
          {hasReactions && <Tab {...tabProps("reactions", t("reactions"))} />}
          <Tab {...tabProps("pipeline", t("pipeline"))} />
        </Tabs>
        <GeopilotBox
          role="tabpanel"
          id={panelId}
          aria-labelledby={`recordingTab-${visibleTab}`}
          sx={{ borderTopLeftRadius: theme => theme.radius.none }}>
          {visibleTab === "steps" && <RecordingSteps run={run} />}
          {visibleTab === "reactions" && <RecordingReactionEvents events={run.reactionEvents} />}
          {visibleTab === "pipeline" && <PipelineSnapshot jobId={run.jobId} pipelineId={run.pipelineId} />}
        </GeopilotBox>
      </Stack>
    </Stack>
  );
};
