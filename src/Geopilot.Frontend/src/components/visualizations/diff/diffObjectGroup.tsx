import { FC, Fragment } from "react";
import { useTranslation } from "react-i18next";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import ExpandMoreIcon from "@mui/icons-material/ExpandMore";
import MapOutlinedIcon from "@mui/icons-material/MapOutlined";
import { Box, Divider, Stack, Typography } from "@mui/material";
import { DiffChangeType } from "../../../api/apiInterfaces";
import { IconButton } from "../../buttons";
import { DiffChangeRow, DiffLayout, ReferenceTarget, WIDE_CHANGE_COLUMNS } from "./diffChangeRow";
import { DiffChangeTypeIcon } from "./diffChangeTypeIcon";
import {
  DIFF_OBJECT_LABEL_KEYS,
  DiffObject,
  getClassName,
  getPropertyChanges,
  summarize,
  SUMMARY_PREFIXES,
} from "./diffObjects";

const SUMMARY_COLORS: Record<DiffChangeType, string | undefined> = {
  added: "diff.added",
  deleted: "diff.deleted",
  changed: undefined,
};

/** The width of the toggle in front of the class, which the rows below are indented by. */
const TOGGLE_WIDTH = 5;

interface DiffObjectSummaryProps {
  object: DiffObject;
}

/** What happened to the object in one line: added or deleted, or the properties it changed in, cut with an ellipsis. */
const DiffObjectSummary: FC<DiffObjectSummaryProps> = ({ object }) => {
  const { t } = useTranslation();
  const items = summarize(object);
  return (
    <Typography
      variant="body2"
      noWrap
      data-cy="diff-object-summary"
      sx={{ color: "text.secondary", flex: 1, minWidth: 0 }}>
      {object.kind !== "changed" ? (
        <Box component="span" sx={{ color: SUMMARY_COLORS[object.kind] }}>
          {t(DIFF_OBJECT_LABEL_KEYS[object.kind])}
        </Box>
      ) : (
        items.map((item, index) => (
          <Fragment key={item.path}>
            {index > 0 && ", "}
            <Box component="span" sx={{ color: SUMMARY_COLORS[item.changeType] }}>
              {SUMMARY_PREFIXES[item.changeType]}
              {item.path}
            </Box>
          </Fragment>
        ))
      )}
    </Typography>
  );
};

interface DiffObjectGroupProps {
  object: DiffObject;
  expanded: boolean;
  /** The object is under the pointer, here or on the map. */
  hovered: boolean;
  layout: DiffLayout;
  onToggle: () => void;
  /** Zooms the map to the object; undefined when the object is not on the map. */
  onShowOnMap?: () => void;
  onHover: (objectId: string | null) => void;
  lookupReference: (oid: string) => ReferenceTarget;
  onReference: (oid: string) => void;
  /** Receives the root, so a jump to the object can scroll to it and focus it. */
  groupRef: (element: HTMLDivElement | null) => void;
}

/**
 * An object of the diff: a header with the class, the object id and a summary of what changed, and below it, when
 * expanded, each change with the value before and after. An object added or deleted as a whole has no details.
 */
export const DiffObjectGroup: FC<DiffObjectGroupProps> = ({
  object,
  expanded,
  hovered,
  layout,
  onToggle,
  onShowOnMap,
  onHover,
  lookupReference,
  onReference,
  groupRef,
}) => {
  const { t } = useTranslation();
  const propertyChanges = getPropertyChanges(object);
  const expandable = propertyChanges.length > 0;
  const className = (
    <Typography variant="body2" noWrap title={object.interlisName} sx={{ fontWeight: 600, flexShrink: 0 }}>
      {getClassName(object.interlisName)}
    </Typography>
  );
  const objectId = (
    <Typography variant="body2" noWrap sx={{ color: "text.secondary", fontFamily: "monospace", minWidth: 0 }}>
      {object.objectId}
    </Typography>
  );

  return (
    <Box
      ref={groupRef}
      tabIndex={-1}
      data-cy="diff-object"
      data-object-id={object.objectId}
      onMouseEnter={() => onHover(object.objectId)}
      onMouseLeave={() => onHover(null)}
      sx={{
        border: 1,
        borderColor: "divider",
        borderRadius: theme => theme.radius.default,
        bgcolor: hovered ? "primary.states.hover" : "background.content",
        "&:focus": {
          outline: theme => `2px solid ${theme.palette.primary.main}`,
          boxShadow: theme => `0 0 0 6px ${theme.palette.primary.states.selected}`,
          bgcolor: "primary.states.hover",
        },
      }}>
      <Stack direction="row" sx={{ alignItems: "center", gap: 1, minHeight: 48, pr: 0.5 }}>
        {expandable ? (
          <IconButton
            size="small"
            label={expanded ? "diffCollapseObject" : "diffExpandObject"}
            icon={expanded ? <ExpandMoreIcon /> : <ChevronRightIcon />}
            aria-expanded={expanded}
            onClick={onToggle}
            data-cy="diff-object-toggle"
            sx={{ ml: 0.5 }}
          />
        ) : (
          <Box sx={{ width: theme => theme.spacing(TOGGLE_WIDTH), flexShrink: 0 }} />
        )}
        <DiffChangeTypeIcon changeType={object.kind} />
        {layout === "wide" ? (
          <Stack direction="row" sx={{ alignItems: "baseline", gap: 1.5, flex: 1, minWidth: 0 }}>
            {className}
            {objectId}
            <DiffObjectSummary object={object} />
          </Stack>
        ) : (
          <Stack sx={{ flex: 1, minWidth: 0, py: 0.5 }}>
            <Stack direction="row" sx={{ alignItems: "baseline", gap: 1, minWidth: 0 }}>
              {className}
              <DiffObjectSummary object={object} />
            </Stack>
            {objectId}
          </Stack>
        )}
        {onShowOnMap && (
          <IconButton
            size="small"
            label="diffShowObjectOnMap"
            icon={<MapOutlinedIcon />}
            onClick={onShowOnMap}
            data-cy="diff-object-map"
          />
        )}
      </Stack>
      {expandable && expanded && (
        <Box sx={{ pl: TOGGLE_WIDTH + 0.5, pr: 1.5, pb: 0.5 }}>
          <Divider />
          {layout === "wide" && (
            <Box
              sx={{
                display: "grid",
                gridTemplateColumns: WIDE_CHANGE_COLUMNS,
                columnGap: 2,
                pt: 1,
                color: "text.secondary",
              }}>
              <Typography variant="caption">{t("diffColumnAttribute")}</Typography>
              <Typography variant="caption">{t("diffColumnOldValue")}</Typography>
              <Typography variant="caption">{t("diffColumnNewValue")}</Typography>
            </Box>
          )}
          {propertyChanges.map((change, index) => (
            <Fragment key={change.id}>
              {index > 0 && <Divider />}
              <DiffChangeRow
                change={change}
                layout={layout}
                lookupReference={lookupReference}
                onReference={onReference}
                // shortcut: a geometry zooms to its whole object, to that geometry and state once the map is reworked
                onShowOnMap={onShowOnMap}
              />
            </Fragment>
          ))}
        </Box>
      )}
    </Box>
  );
};
