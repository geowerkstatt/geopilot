import { FC, useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link as RouterLink, useLocation, useNavigate, useParams } from "react-router-dom";
import { Box, CircularProgress, Link, Stack, SxProps, Tab, Tabs, Theme, Typography } from "@mui/material";
import { DeclarerKind, PipelineRunOutcomeKind, PipelineRunResponse } from "../../../api/generated";
import { AdminDetailHeader } from "../../../components/adminDetailHeader.tsx";
import { useCapabilities } from "../../../components/capabilities/capabilitiesInterface.ts";
import { GeopilotBox } from "../../../components/styledComponents.ts";
import useFetch from "../../../hooks/useFetch.ts";
import { useLocalized } from "../../../hooks/useLocalized.ts";
import { SummaryField } from "../deliveries/summaryField.tsx";
import { PipelineRunFiles } from "./pipelineRunFiles.tsx";
import { formatClientKind, formatDuration } from "./pipelineRunFormat.ts";
import { PipelineRunOutcomeLabel } from "./pipelineRunOutcomeLabel.tsx";
import { parseJobId } from "./pipelineRunQuery.ts";
import { PipelineRunReactionEvents } from "./pipelineRunReactionEvents.tsx";
import { PipelineRunSteps } from "./pipelineRunSteps.tsx";
import { PipelineSnapshot } from "./pipelineSnapshot.tsx";
import { ProcessingStateLabel } from "./processingStateLabel.tsx";
import { TechnicalText } from "./technicalText.tsx";

type DetailTab = "steps" | "files" | "reactions" | "pipeline";

// The tab row overlaps the top border of the box by one pixel, so the selected tab can cover it and join the box.
const folderTabsSx: SxProps<Theme> = theme => ({
  position: "relative",
  zIndex: 1,
  marginBottom: "-1px",
  "& .MuiTab-root": {
    ...theme.typography.h3,
    margin: 0,
    color: theme.palette.text.secondary,
    backgroundColor: theme.palette.primary.states.hover,
    border: `1px solid ${theme.palette.primary.light}`,
    borderBottomColor: "transparent",
    borderTopLeftRadius: theme.radius.default,
    borderTopRightRadius: theme.radius.default,
    // Keeps the hover background off the transparent bottom border, where the top border of the box shows through.
    backgroundClip: "padding-box",
  },
  "& .MuiTabs-list": {
    gap: theme.spacing(1),
  },
  "& .MuiTab-root:hover": {
    color: theme.palette.text.primary,
    backgroundColor: theme.palette.primary.states.selected,
  },
  "& .MuiTab-root.Mui-selected": {
    color: theme.palette.text.primary,
    backgroundColor: theme.palette.background.content,
    borderBottomColor: theme.palette.background.content,
  },
});

const listPath = "/admin/pipeline-runs";
const panelId = "pipelineRunPanel";

/** One pipeline run: who started it, what became of it, its steps, files, reactions and pipeline. */
export const PipelineRunDetail: FC = () => {
  const { t } = useTranslation();
  const { jobId: jobIdParam } = useParams<{ jobId: string }>();
  const navigate = useNavigate();
  const location = useLocation();
  const { fetchApi } = useFetch();
  const { localized } = useLocalized();
  const { machineDeliveryEnabled } = useCapabilities();
  const [loaded, setLoaded] = useState<{ jobId: string; run?: PipelineRunResponse }>();
  const [tab, setTab] = useState<DetailTab>("steps");

  // The address may spell the id in capitals or without hyphens; the API answers in one form only.
  const jobId = parseJobId(jobIdParam ?? "");

  useEffect(() => {
    if (!jobId) {
      return;
    }
    let current = true;
    fetchApi<PipelineRunResponse>(`/api/v1/pipelinerun/${jobId}`, { errorMessageLabel: "pipelineRunLoadingError" })
      .then(run => current && setLoaded({ jobId, run }))
      // useFetch has shown why; the page then says that there is no such run instead of waiting.
      .catch(() => current && setLoaded({ jobId }));
    return () => {
      current = false;
    };
  }, [fetchApi, jobId]);

  // Back returns to where the admin came from, such as a filtered list or a delivery; opened directly, to the list.
  const goBack = () => (location.key !== "default" ? navigate(-1) : navigate(listPath));
  const header = <AdminDetailHeader backLabel="backToPipelineRuns" onBack={goBack} />;

  // Following a link to another run keeps the page mounted, so the previous run must not show meanwhile.
  const result = jobId && loaded?.jobId === jobId ? loaded : undefined;
  if (!jobId || (result && !result.run)) {
    return (
      <Stack>
        {header}
        <GeopilotBox>
          <Typography data-cy="pipelineRunNotFound">{t("pipelineRunNotFound")}</Typography>
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
  // A run without reactions offers no tab for them, so the page must not stay on it.
  const visibleTab = tab === "reactions" && run.reactionEvents.length === 0 ? "steps" : tab;
  const tabProps = (value: DetailTab) => ({
    value,
    id: `pipelineRunTab-${value}`,
    "aria-controls": panelId,
    "data-cy": `pipelineRunTab-${value}`,
  });

  return (
    <Stack>
      {header}
      <GeopilotBox>
        <Typography variant="h3" sx={{ marginTop: 0 }}>
          {t("summary")}
        </Typography>
        <Box sx={{ display: "grid", gridTemplateColumns: { xs: "1fr", md: "1fr 1fr" }, gap: 2 }}>
          <SummaryField label="outcome">
            <PipelineRunOutcomeLabel outcome={run.outcome} allSteps linkDelivery />
            {run.outcome.kind === PipelineRunOutcomeKind.NotDelivered && (
              <Typography variant="body2" sx={{ color: "text.secondary" }}>
                {t("pipelineRunNotDeliveredHint")}
              </Typography>
            )}
          </SummaryField>
          <SummaryField label="processedBy">
            {declarerPath ? (
              <Link component={RouterLink} to={declarerPath}>
                {run.declarer?.name}
              </Link>
            ) : (
              (run.declarer?.name ?? t("anonymous"))
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
          <SummaryField label="pipeline">
            <TechnicalText>{run.pipelineId}</TechnicalText>
          </SummaryField>
          <SummaryField label="processingStarted">{new Date(run.startedAt).toLocaleString()}</SummaryField>
          <SummaryField label="startedVia">{formatClientKind(t, run.clientKind) ?? "-"}</SummaryField>
          {run.clientReference && <SummaryField label="clientReference">{run.clientReference}</SummaryField>}
          <SummaryField label="state">
            <Stack direction="row" sx={{ alignItems: "center", gap: 1 }}>
              <ProcessingStateLabel
                state={run.terminalState}
                running={run.outcome.kind === PipelineRunOutcomeKind.Running}
              />
              {duration && `(${duration})`}
            </Stack>
          </SummaryField>
          <SummaryField label="jobId">
            <TechnicalText>{run.jobId}</TechnicalText>
          </SummaryField>
          <SummaryField label="appVersion">
            <TechnicalText>{run.appVersion}</TechnicalText>
          </SummaryField>
          {run.failureReason && (
            <SummaryField label="failureReason" wide>
              {run.failureReason}
            </SummaryField>
          )}
        </Box>
      </GeopilotBox>
      <Stack spacing={0}>
        <Tabs
          value={visibleTab}
          onChange={(_event, value: DetailTab) => setTab(value)}
          sx={folderTabsSx}
          slotProps={{ indicator: { sx: { display: "none" } } }}>
          <Tab {...tabProps("steps")} label={t("processingSteps")} />
          <Tab {...tabProps("files")} label={t("files")} />
          {run.reactionEvents.length > 0 && <Tab {...tabProps("reactions")} label={t("reactions")} />}
          <Tab {...tabProps("pipeline")} label={t("pipeline")} />
        </Tabs>
        <GeopilotBox
          role="tabpanel"
          id={panelId}
          aria-labelledby={`pipelineRunTab-${visibleTab}`}
          sx={{ borderTopLeftRadius: theme => theme.radius.none }}>
          {visibleTab === "steps" && <PipelineRunSteps steps={run.steps} />}
          {visibleTab === "files" && (
            <PipelineRunFiles files={run.files} scanState={run.scanState} scanDetails={run.scanDetails} />
          )}
          {visibleTab === "reactions" && <PipelineRunReactionEvents events={run.reactionEvents} />}
          {visibleTab === "pipeline" && <PipelineSnapshot jobId={run.jobId} pipelineId={run.pipelineId} />}
        </GeopilotBox>
      </Stack>
    </Stack>
  );
};
