import { FC } from "react";
import { useTranslation } from "react-i18next";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import { Accordion, AccordionDetails, AccordionSummary, Box, Stack, Typography } from "@mui/material";
import { ReactionEvent, RecordingReactionEventResponse } from "../../../api/generated";
import { useLocalized } from "../../../hooks/useLocalized.ts";
import { SummaryField } from "../deliveries/summaryField.tsx";
import { ReactionEventConfig } from "./reactionEventConfig.tsx";
import { ReactionEventSummary } from "./reactionEventSummary.tsx";

interface RecordingReactionEventsProps {
  events: RecordingReactionEventResponse[];
}

// The events carry no id; these values tell them apart, also once a later load adds events.
const eventKey = (reactionEvent: RecordingReactionEventResponse) =>
  [reactionEvent.reactionId, reactionEvent.event, reactionEvent.attempt, reactionEvent.occurredAt].join("|");

/**
 * What happened to the reactions of the delivery a run produced, in the order it happened, one entry per event as the
 * steps of the run are shown. An attempt opens to show its channel, the configuration it ran with and its message. A
 * reset or a deletion ran nothing, so it shows its header only.
 */
export const RecordingReactionEvents: FC<RecordingReactionEventsProps> = ({ events }) => {
  const { t } = useTranslation();
  const { localized } = useLocalized();

  if (events.length === 0) {
    return (
      <SummaryField labelVariant="h5" label="reactionEvents">
        <Typography data-cy="reactionEventsNone">{t("reactionEventsNone")}</Typography>
      </SummaryField>
    );
  }

  return (
    <SummaryField labelVariant="h5" label="reactionEvents">
      <Box>
        {events.map(reactionEvent => {
          const dataCy = `reactionEvent-${reactionEvent.reactionId}-${reactionEvent.attempt ?? reactionEvent.event}`;
          if (reactionEvent.event !== ReactionEvent.Attempt) {
            // Without a summary there is no button that would open nothing.
            return (
              <Accordion key={eventKey(reactionEvent)} expanded={false} data-cy={dataCy}>
                <Box sx={{ paddingX: 2, paddingY: 1.5 }}>
                  <ReactionEventSummary reactionEvent={reactionEvent} />
                </Box>
              </Accordion>
            );
          }

          return (
            <Accordion key={eventKey(reactionEvent)} data-cy={dataCy}>
              <AccordionSummary expandIcon={<ExpandMoreIcon />}>
                <ReactionEventSummary reactionEvent={reactionEvent} />
              </AccordionSummary>
              <AccordionDetails>
                <Stack>
                  <SummaryField labelVariant="h5" label="channel">
                    {reactionEvent.implementation}
                    {reactionEvent.appVersion && (
                      <Typography
                        component="span"
                        variant="body1"
                        sx={{ color: "text.secondary", marginLeft: 1, display: "inline-block" }}>
                        (&nbsp;v{reactionEvent.appVersion}&nbsp;)
                      </Typography>
                    )}
                  </SummaryField>
                  {reactionEvent.config && (
                    <SummaryField labelVariant="h5" label="configuration">
                      <ReactionEventConfig config={reactionEvent.config} />
                    </SummaryField>
                  )}
                  {reactionEvent.message && (
                    <SummaryField labelVariant="h5" label="message">
                      {localized(reactionEvent.message)}
                    </SummaryField>
                  )}
                </Stack>
              </AccordionDetails>
            </Accordion>
          );
        })}
      </Box>
    </SummaryField>
  );
};
