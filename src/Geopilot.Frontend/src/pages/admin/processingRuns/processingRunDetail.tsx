import { FC, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link as RouterLink, useLocation, useNavigate, useParams } from "react-router-dom";
import { Box, CircularProgress, Link, Stack, SxProps, Tab, Tabs, Theme, Typography } from "@mui/material";
import { DeclarerKind, PipelineRunOutcomeKind, PipelineRunResponse, ReactionState } from "../../../api/generated";
import { NEOGEO_FONTWEIGHT_BOLD, NEOGEO_FONTWEIGHT_REGULAR, px2rem } from "../../../appTheme.ts";
import { AdminDetailHeader } from "../../../components/adminDetailHeader.tsx";
import { useCapabilities } from "../../../components/capabilities/capabilitiesInterface.ts";
import { DeclarerLabel } from "../../../components/declarerLabel.tsx";
import { GeopilotBox } from "../../../components/styledComponents.ts";
import useFetch from "../../../hooks/useFetch.ts";
import { useLocalized } from "../../../hooks/useLocalized.ts";
import { SummaryField } from "../deliveries/summaryField.tsx";
import { PipelineSnapshot } from "./pipelineSnapshot.tsx";
import { ProcessingRunAttemptList } from "./processingRunAttemptList.tsx";
import { ProcessingRunDeliveryLabel } from "./processingRunDeliveryLabel.tsx";
import { formatDuration } from "./processingRunFormat.ts";
import { listQueryOf, listQueryState, parseJobId, ProcessingRunTab, tabFromHash } from "./processingRunQuery.ts";
import { ProcessingRunReactionEvents } from "./processingRunReactionEvents.tsx";
import { ProcessingRunSteps } from "./processingRunSteps.tsx";
import { ProcessingStateLabel } from "./processingStateLabel.tsx";

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

const listPath = "/admin/processing-runs";
const panelId = "processingRunPanel";
// How often the page loads the run again while a reaction of its delivery is pending or running.
const reactionRefreshInterval = 15_000;

/**
 * One pipeline run: who started it, the other attempts of its delivery, what became of it, its preparation with the
 * uploaded files, its steps, reactions and pipeline.
 */
export const ProcessingRunDetail: FC = () => {
  const { t } = useTranslation();
  const { jobId: jobIdParam } = useParams<{ jobId: string }>();
  const navigate = useNavigate();
  const location = useLocation();
  const { fetchApi } = useFetch();
  const { localized } = useLocalized();
  const { machineDeliveryEnabled } = useCapabilities();
  const [loaded, setLoaded] = useState<{ jobId: string; run?: PipelineRunResponse }>();

  // The address may spell the id in capitals or without hyphens; the API answers in one form only.
  const jobId = parseJobId(jobIdParam ?? "");

  const [loadCount, setLoadCount] = useState(0);

  useEffect(() => {
    if (!jobId) {
      return;
    }
    let current = true;
    fetchApi<PipelineRunResponse>(`/api/v1/pipelinerun/${jobId}`, { errorMessageLabel: "processingRunLoadingError" })
      .then(run => current && setLoaded({ jobId, run }))
      // useFetch has shown why. A run already shown stays when loading it again fails; otherwise the page says that
      // there is no such run instead of waiting.
      .catch(() => current && setLoaded(previous => (previous?.jobId === jobId ? previous : { jobId })));
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
  const showTab = (value: ProcessingRunTab) =>
    navigate({ hash: value }, { replace: true, state: listQueryState(listQuery) });

  // Back shows the list with the filters and page it was left with; opened from anywhere else, the whole list.
  const goBack = () => navigate({ pathname: listPath, search: listQuery });
  const renderHeader = (jobId?: string) => (
    <AdminDetailHeader backLabel="backToProcessingRuns" onBack={goBack} id={jobId} idLabel="jobId" />
  );
  const header = renderHeader();

  // Following a link to another run keeps the page mounted, so the previous run must not show meanwhile.
  const result = jobId && loaded?.jobId === jobId ? loaded : undefined;
  if (!jobId || (result && !result.run)) {
    return (
      <Stack>
        {header}
        <GeopilotBox>
          <Typography data-cy="processingRunNotFound">{t("processingRunNotFound")}</Typography>
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
  const declarerPath =
    run.declarer?.kind === DeclarerKind.Person
      ? `/admin/users/${run.declarer.id}`
      : run.declarer?.kind === DeclarerKind.MachineClient && machineDeliveryEnabled
        ? `/admin/machine-clients/${run.declarer.id}`
        : undefined;
  // Reactions wait without events until their first attempt, so a delivery with reactions offers the tab from the start.
  const hasReactions = run.reactionEvents.length > 0 || !!run.forwardingState || !!run.notificationState;
  // A run without reactions offers no tab for them, so the page must not stay on it.
  const visibleTab = tab === "reactions" && !hasReactions ? "steps" : tab;
  const tabProps = (value: ProcessingRunTab, label: string) => ({
    value,
    label,
    id: `processingRunTab-${value}`,
    "aria-controls": panelId,
    "data-cy": `processingRunTab-${value}`,
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
          <ProcessingRunAttemptList attempts={run.attempts} shownJobId={run.jobId} />
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
              running={run.outcome.kind === PipelineRunOutcomeKind.Running}
              blockingSteps={run.outcome.blockingSteps}
              preflightFailed={run.outcome.preflightFailed}
              allSteps
              duration={duration}
            />
          </SummaryField>
          <SummaryField label="processedBy">
            <DeclarerLabel declarer={run.declarer} path={declarerPath} />
          </SummaryField>
          {run.clientReference && <SummaryField label="clientReference">{run.clientReference}</SummaryField>}
          <SummaryField label="delivery">
            <ProcessingRunDeliveryLabel outcome={run.outcome} linkDelivery />
            {run.outcome.kind === PipelineRunOutcomeKind.DeclarationFailed && (
              <Typography variant="body2" sx={{ color: "text.secondary" }} data-cy="processingRunDeclarationFailure">
                {run.declarationFailure}
              </Typography>
            )}
            {/* Runs recorded before the protocol kept the declarability can only guess why there is no delivery. */}
            {run.outcome.kind === PipelineRunOutcomeKind.NotDelivered && !run.declarability && (
              <Typography variant="body2" sx={{ color: "text.secondary" }}>
                {t("processingRunNotDeliveredHint")}
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
          onChange={(_event, value: ProcessingRunTab) => showTab(value)}
          sx={folderTabsSx}
          slotProps={{ indicator: { sx: { display: "none" } } }}>
          <Tab {...tabProps("steps", t("processingSteps"))} />
          {hasReactions && <Tab {...tabProps("reactions", t("reactions"))} />}
          <Tab {...tabProps("pipeline", t("pipeline"))} />
        </Tabs>
        <GeopilotBox
          role="tabpanel"
          id={panelId}
          aria-labelledby={`processingRunTab-${visibleTab}`}
          sx={{ borderTopLeftRadius: theme => theme.radius.none }}>
          {visibleTab === "steps" && <ProcessingRunSteps run={run} />}
          {visibleTab === "reactions" && <ProcessingRunReactionEvents events={run.reactionEvents} />}
          {visibleTab === "pipeline" && <PipelineSnapshot jobId={run.jobId} pipelineId={run.pipelineId} />}
        </GeopilotBox>
      </Stack>
    </Stack>
  );
};
