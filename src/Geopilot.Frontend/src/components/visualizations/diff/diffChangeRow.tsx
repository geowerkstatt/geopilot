import { FC, ReactNode, useMemo } from "react";
import { useTranslation } from "react-i18next";
import MapOutlinedIcon from "@mui/icons-material/MapOutlined";
import { Box, Chip, Stack, Tooltip, Typography } from "@mui/material";
import { DiffChange, DiffState } from "../../../api/apiInterfaces";
import { TranslationKey } from "../../../appInterfaces";
import { px2rem } from "../../../appTheme";
import { IconButton } from "../../buttons";
import { DiffChangeTypeIcon } from "./diffChangeTypeIcon";
import { MARK_BACKGROUNDS } from "./diffObjects";
import { DiffValueText } from "./diffValue";
import { diffValueSegments, parseReferences, ValueSegment } from "./valueDiff";

/** Wide: before and after side by side. Narrow (fullscreen panel, phone): one below the other. */
export type DiffLayout = "wide" | "narrow";

export interface ReferenceTarget {
  state: "listed" | "filtered" | "absent";
  className?: string;
}

/** The grid of a change in the wide layout, shared with the column header of the expanded object. */
export const WIDE_CHANGE_COLUMNS = `${px2rem(200)} minmax(0, 1fr) minmax(0, 1fr)`;

const SHORT_OID_LENGTH = 12;

/** Why a reference is not a link to its object. */
const REFERENCE_REASONS: Record<ReferenceTarget["state"], TranslationKey | undefined> = {
  listed: undefined,
  filtered: "diffReferenceFiltered",
  absent: "diffReferenceNotInDiff",
};

interface ReferenceChipProps {
  oid: string;
  state: DiffState;
  /** The reference is only in this state of the change. */
  changed: boolean;
  target: ReferenceTarget;
  onReference: (oid: string) => void;
}

const ReferenceChip: FC<ReferenceChipProps> = ({ oid, state, changed, target, onReference }) => {
  const { t } = useTranslation();
  const shortOid = `…${oid.slice(-SHORT_OID_LENGTH)}`;
  const listed = target.state === "listed";
  const reason = REFERENCE_REASONS[target.state];
  const description = reason ? `${t(reason)}: ${oid}` : oid;
  return (
    <Tooltip title={description}>
      <Chip
        size="small"
        variant="outlined"
        label={listed && target.className ? `${target.className} ${shortOid}` : shortOid}
        onClick={listed ? () => onReference(oid) : undefined}
        tabIndex={listed ? undefined : 0}
        aria-label={listed ? undefined : description}
        data-cy="diff-reference"
        sx={{
          bgcolor: changed ? MARK_BACKGROUNDS[state] : "transparent",
          borderStyle: listed ? "solid" : "dashed",
          color: listed ? "primary.main" : "text.secondary",
          textDecoration: listed ? "underline" : "none",
          maxWidth: "100%",
        }}
      />
    </Tooltip>
  );
};

interface DiffChangeRowProps {
  change: DiffChange;
  layout: DiffLayout;
  lookupReference: (oid: string) => ReferenceTarget;
  onReference: (oid: string) => void;
  onShowOnMap?: (state: DiffState) => void;
}

export const DiffChangeRow: FC<DiffChangeRowProps> = ({
  change,
  layout,
  lookupReference,
  onReference,
  onShowOnMap,
}) => {
  const { t } = useTranslation();
  const segments = useMemo(() => {
    if (change.oldValue !== undefined && change.newValue !== undefined)
      return diffValueSegments(change.oldValue, change.newValue);
    const whole = (value?: string): ValueSegment[] => (value === undefined ? [] : [{ text: value, changed: true }]);
    return { old: whole(change.oldValue), new: whole(change.newValue) };
  }, [change.oldValue, change.newValue]);
  const references = useMemo(
    () =>
      change.valueType === "reference"
        ? {
            old: change.oldValue === undefined ? undefined : parseReferences(change.oldValue),
            new: change.newValue === undefined ? undefined : parseReferences(change.newValue),
          }
        : undefined,
    [change.valueType, change.oldValue, change.newValue],
  );

  const renderValue = (state: DiffState): ReactNode => {
    const value = state === "old" ? change.oldValue : change.newValue;
    if (value === undefined) {
      return (
        <Typography variant="body2" sx={{ fontStyle: "italic", color: "text.secondary" }}>
          {t("diffEmptyValue")}
        </Typography>
      );
    }
    const oids = references?.[state];
    if (oids) {
      const otherState = references?.[state === "old" ? "new" : "old"] ?? [];
      return (
        <Stack direction="row" sx={{ gap: 0.5, flexWrap: "wrap", minWidth: 0 }}>
          {oids.map(oid => (
            <ReferenceChip
              key={oid}
              oid={oid}
              state={state}
              changed={!otherState.includes(oid)}
              target={lookupReference(oid)}
              onReference={onReference}
            />
          ))}
        </Stack>
      );
    }
    const text = <DiffValueText segments={segments[state]} state={state} monospace={change.valueType === "geometry"} />;
    if (change.valueType !== "geometry" || !onShowOnMap) return text;
    return (
      <Stack direction="row" sx={{ gap: 0.5, alignItems: "flex-start", justifyContent: "space-between", minWidth: 0 }}>
        {text}
        <IconButton
          size="small"
          label={state === "old" ? "diffShowBeforeOnMap" : "diffShowAfterOnMap"}
          icon={<MapOutlinedIcon fontSize="small" />}
          onClick={() => onShowOnMap(state)}
          data-cy={`diff-geometry-map-${state}`}
          sx={{ mt: -0.5 }}
        />
      </Stack>
    );
  };

  const attribute = (
    <Stack direction="row" sx={{ gap: 1, alignItems: "center", minWidth: 0 }}>
      <DiffChangeTypeIcon changeType={change.changeType} />
      <Typography variant="body2" sx={{ fontWeight: layout === "narrow" ? 600 : undefined, wordBreak: "break-word" }}>
        {change.attributePath}
      </Typography>
    </Stack>
  );

  if (layout === "narrow") {
    return (
      <Stack data-cy="diff-change" data-change-id={change.id} sx={{ gap: 0.5, py: 1 }}>
        {attribute}
        <Stack sx={{ gap: 1.5, pl: 3.5, minWidth: 0 }}>
          {(["old", "new"] as const).map(state => (
            <Stack key={state} sx={{ gap: 0.5, minWidth: 0 }} data-cy={`diff-value-${state}`}>
              <Typography variant="caption" sx={{ color: "text.secondary", lineHeight: 1.4 }}>
                {t(state === "old" ? "diffColumnOldValue" : "diffColumnNewValue")}
              </Typography>
              {renderValue(state)}
            </Stack>
          ))}
        </Stack>
      </Stack>
    );
  }

  return (
    <Box
      data-cy="diff-change"
      data-change-id={change.id}
      sx={{ display: "grid", gridTemplateColumns: WIDE_CHANGE_COLUMNS, columnGap: 2, py: 1, alignItems: "start" }}>
      {attribute}
      <Box sx={{ minWidth: 0 }} data-cy="diff-value-old">
        {renderValue("old")}
      </Box>
      <Box sx={{ minWidth: 0 }} data-cy="diff-value-new">
        {renderValue("new")}
      </Box>
    </Box>
  );
};
