import { FC, Fragment, useState } from "react";
import { useTranslation } from "react-i18next";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import { TableCell, TableRow, Typography } from "@mui/material";
import { ReactionEvent, ReactionKind, RecordingReactionEventResponse } from "../../../api/generated";
import { TranslationKey } from "../../../appInterfaces";
import { px2rem } from "../../../appTheme.ts";
import { IconButton } from "../../../components/buttons.tsx";
import { DetailTable } from "../../../components/detailTable.tsx";
import { ReactionStateLabel } from "../../../components/grids/reactionStateLabel.tsx";
import { useLocalized } from "../../../hooks/useLocalized.ts";
import { SummaryField } from "../deliveries/summaryField.tsx";
import { ReactionEventConfig } from "./reactionEventConfig.tsx";

interface RecordingReactionEventsProps {
  events: RecordingReactionEventResponse[];
}

const reactionKindKey: Record<ReactionKind, TranslationKey> = {
  [ReactionKind.Transfer]: "reactionKindTransfer",
  [ReactionKind.Notification]: "reactionKindNotification",
};

const reactionEventKey: Record<ReactionEvent, TranslationKey> = {
  [ReactionEvent.Attempt]: "reactionEventAttempt",
  [ReactionEvent.Reset]: "reactionEventReset",
  [ReactionEvent.Deletion]: "reactionEventDeletion",
};

// The events carry no id; these values tell them apart, also once a later load adds events.
const eventKey = (reactionEvent: RecordingReactionEventResponse) =>
  [reactionEvent.reactionId, reactionEvent.event, reactionEvent.attempt, reactionEvent.occurredAt].join("|");

/**
 * What happened to the reactions of the delivery a run produced, in the order it happened. An attempt recorded with
 * the version that ran it opens to show that version and the configuration of its channel.
 */
export const RecordingReactionEvents: FC<RecordingReactionEventsProps> = ({ events }) => {
  const { t } = useTranslation();
  const { localized } = useLocalized();
  const [opened, setOpened] = useState<ReadonlySet<string>>(new Set());

  const toggle = (key: string) =>
    setOpened(current => {
      const next = new Set(current);
      if (!next.delete(key)) {
        next.add(key);
      }
      return next;
    });

  if (events.length === 0) {
    return (
      <SummaryField labelVariant="h5" label="reactionEvents">
        <Typography data-cy="reactionEventsNone">{t("reactionEventsNone")}</Typography>
      </SummaryField>
    );
  }

  return (
    <SummaryField labelVariant="h5" label="reactionEvents">
      <DetailTable
        columns={[
          { label: "", width: px2rem(48) },
          { label: t("time"), width: "18%" },
          { label: t("reaction"), width: "16%" },
          { label: t("kind"), width: "12%" },
          { label: t("event"), width: "16%" },
          { label: t("state"), width: "14%" },
          { label: t("message") },
        ]}>
        {events.map(reactionEvent => {
          const key = eventKey(reactionEvent);
          const isOpen = opened.has(key);
          return (
            <Fragment key={key}>
              <TableRow>
                <TableCell>
                  {reactionEvent.appVersion && (
                    <IconButton
                      size="small"
                      label={isOpen ? "reactionEventHideConfig" : "reactionEventShowConfig"}
                      icon={isOpen ? <ExpandMoreIcon /> : <ChevronRightIcon />}
                      aria-expanded={isOpen}
                      onClick={() => toggle(key)}
                      data-cy="reactionEventConfigToggle"
                    />
                  )}
                </TableCell>
                <TableCell>{new Date(reactionEvent.occurredAt).toLocaleString()}</TableCell>
                <TableCell>{reactionEvent.reactionId}</TableCell>
                <TableCell>{t(reactionKindKey[reactionEvent.reactionKind])}</TableCell>
                <TableCell>{t(reactionEventKey[reactionEvent.event], { attempt: reactionEvent.attempt })}</TableCell>
                <TableCell>{reactionEvent.state && <ReactionStateLabel state={reactionEvent.state} />}</TableCell>
                <TableCell>{localized(reactionEvent.message ?? undefined)}</TableCell>
              </TableRow>
              {isOpen && reactionEvent.appVersion && (
                <TableRow>
                  <TableCell colSpan={7}>
                    <ReactionEventConfig appVersion={reactionEvent.appVersion} config={reactionEvent.config} />
                  </TableCell>
                </TableRow>
              )}
            </Fragment>
          );
        })}
      </DetailTable>
    </SummaryField>
  );
};
