import { FC, useId } from "react";
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
  const idPrefix = useId();

  if (events.length === 0) {
    return (
      <SummaryField labelVariant="h5" label="reactionEvents">
        <Typography data-cy="reactionEventsNone">{t("reactionEventsNone")}</Typography>
      </SummaryField>
    );
  }

  // Like the steps of the run, the events stand without a heading of their own above the headings of the accordions.
  return (
    <Box data-cy="reactionEvents">
      {events.map((reactionEvent, index) => {
        // After a reset the attempts count from one again, so only the position tells the entries apart.
        const dataCy = `reactionEvent-${index}`;
        if (reactionEvent.event !== ReactionEvent.Attempt) {
          // Without a summary there is no button that would open nothing. The right padding takes the place of the
          // expand icon of an attempt, so the times line up.
          return (
            <Accordion key={eventKey(reactionEvent)} expanded={false} data-cy={dataCy}>
              <Box sx={{ paddingLeft: 2, paddingRight: 7, paddingY: 1.5 }}>
                <ReactionEventSummary reactionEvent={reactionEvent} />
              </Box>
            </Accordion>
          );
        }

        return (
          <Accordion key={eventKey(reactionEvent)} data-cy={dataCy}>
            {/* With both ids the accordion names its region after the header. */}
            <AccordionSummary
              expandIcon={<ExpandMoreIcon />}
              id={`${idPrefix}-${index}-header`}
              aria-controls={`${idPrefix}-${index}-content`}>
              <ReactionEventSummary reactionEvent={reactionEvent} />
            </AccordionSummary>
            <AccordionDetails>
              <Stack>
                <SummaryField labelVariant="h5" label="channel">
                  {reactionEvent.implementation}
                  {/* The version of geopilot, not of the channel: it names the build that ran the attempt. */}
                  {reactionEvent.appVersion && (
                    <Typography
                      component="span"
                      variant="body1"
                      sx={{ color: "text.secondary", marginLeft: 1, display: "inline-block" }}>
                      (&nbsp;geopilot v{reactionEvent.appVersion}&nbsp;)
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
  );
};
