import { FC, Fragment, memo } from "react";
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

/** The width of the toggle slot; with the gap after it, the rows below are indented as much, so all change icons line up. */
const TOGGLE_WIDTH = 4.5;

interface DiffObjectSummaryProps {
  object: DiffObject;
}

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
  onToggle: (objectId: string) => void;
  /** Zooms the map to the object; undefined when the object is not on the map. */
  onShowOnMap?: (objectId: string) => void;
  onHover: (objectId: string | null) => void;
  lookupReference: (oid: string) => ReferenceTarget;
  onReference: (oid: string) => void;
  /** Receives the root, so a jump to the object can scroll to it and focus it. */
  registerObject: (objectId: string, element: HTMLDivElement | null) => void;
}

const DiffObjectGroupContent: FC<DiffObjectGroupProps> = ({
  object,
  expanded,
  hovered,
  layout,
  onToggle,
  onShowOnMap,
  onHover,
  lookupReference,
  onReference,
  registerObject,
}) => {
  const { t } = useTranslation();
  const propertyChanges = getPropertyChanges(object);
  const expandable = propertyChanges.length > 0;
  const showOnMap = onShowOnMap && (() => onShowOnMap(object.objectId));
  // Selecting text in the header, the object id to copy it for example, leaves the object as it is.
  const toggle = () => {
    if (!window.getSelection()?.toString()) onToggle(object.objectId);
  };
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
      ref={(element: HTMLDivElement | null) => registerObject(object.objectId, element)}
      tabIndex={-1}
      data-cy="diff-object"
      data-object-id={object.objectId}
      onMouseEnter={() => onHover(object.objectId)}
      onMouseLeave={() => onHover(null)}
      sx={{
        border: 1,
        borderColor: hovered ? "primary.main" : "primary.light",
        borderRadius: theme => theme.radius.default,
        bgcolor: "background.content",
        outline: "none",
        "&:focus": {
          borderColor: "primary.main",
          boxShadow: theme => `inset 0 0 0 1px ${theme.palette.primary.main}`,
        },
      }}>
      {/* The header handles the click; the chevron has no handler of its own, so its keyboard click bubbles here. */}
      <Stack
        onClick={expandable ? toggle : undefined}
        sx={{ gap: 0, pr: 0.5, py: layout === "wide" ? 0.5 : 0.25, cursor: expandable ? "pointer" : undefined }}>
        <Stack direction="row" sx={{ alignItems: "center", gap: 1, minHeight: layout === "wide" ? 40 : 36 }}>
          <Stack sx={{ width: theme => theme.spacing(TOGGLE_WIDTH), flexShrink: 0, alignItems: "center" }}>
            {expandable && (
              <IconButton
                size="small"
                label={expanded ? "diffCollapseObject" : "diffExpandObject"}
                icon={expanded ? <ExpandMoreIcon /> : <ChevronRightIcon />}
                aria-expanded={expanded}
                data-cy="diff-object-toggle"
              />
            )}
          </Stack>
          <DiffChangeTypeIcon changeType={object.kind} />
          {/* Without containment the page grows to fit this unbreakable text instead of cutting it. */}
          <Stack direction="row" sx={{ alignItems: "center", gap: 1.5, flex: 1, minWidth: 0, contain: "inline-size" }}>
            {className}
            {layout === "wide" && objectId}
            {layout === "wide" && <DiffObjectSummary object={object} />}
          </Stack>
          {showOnMap && (
            <IconButton
              size="small"
              label="diffShowObjectOnMap"
              icon={<MapOutlinedIcon />}
              onClick={event => {
                event.stopPropagation();
                showOnMap();
              }}
              data-cy="diff-object-map"
            />
          )}
        </Stack>
        {layout === "narrow" && <Box sx={{ pl: TOGGLE_WIDTH + 1, pb: 0.5, contain: "inline-size" }}>{objectId}</Box>}
      </Stack>
      {expandable && expanded && (
        <Box sx={{ pl: TOGGLE_WIDTH + 1, pr: 1.5, pb: 0.5 }}>
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
                onShowOnMap={showOnMap}
              />
            </Fragment>
          ))}
        </Box>
      )}
    </Box>
  );
};

// Memoized: a hover change then renders only the two objects whose state flips.
export const DiffObjectGroup = memo(DiffObjectGroupContent);
