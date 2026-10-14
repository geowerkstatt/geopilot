import { FC } from "react";
import { useTranslation } from "react-i18next";
import { Box, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography } from "@mui/material";
import { DiffChange } from "../../../api/apiInterfaces";
import { geopilotTheme, px2rem } from "../../../appTheme";
import { COPY_BUTTON_CLASS, CopyButton } from "../../copyButton";
import { GeopilotBox } from "../../styledComponents";
import { DetailRow } from "../tree/detailRow";
import { getChangeLabelKey } from "./diffChanges";
import { DiffChangeTypeIcon } from "./diffChangeTypeIcon";

interface DiffValueProps {
  value?: string;
}

/**
 * A value of a change with a button to copy it, shown on hover where there is a pointer. Long values, a BLACKBOX for
 * example, scroll inside their cell.
 */
const DiffValue: FC<DiffValueProps> = ({ value }) =>
  value === undefined ? null : (
    <Stack
      direction="row"
      sx={{ gap: 0.5, alignItems: "flex-start", [`&:hover .${COPY_BUTTON_CLASS}`]: { opacity: 1 } }}>
      <Box sx={{ flex: 1, minWidth: 0, maxHeight: px2rem(128), overflowY: "auto", wordBreak: "break-word" }}>
        <Typography variant="body2">{value}</Typography>
      </Box>
      <Box sx={{ display: "none", "@media (hover: hover) and (pointer: fine)": { display: "block" } }}>
        <CopyButton value={value} />
      </Box>
    </Stack>
  );

interface DiffChangesProps {
  changes: DiffChange[];
}

/** The changes in a table, the value before next to the value after, where the panel is as wide as the page. */
const DiffChangesTable: FC<DiffChangesProps> = ({ changes }) => {
  const { t } = useTranslation();
  return (
    <Table size="small" sx={{ tableLayout: "fixed" }}>
      <TableHead>
        <TableRow>
          <TableCell>{t("diffColumnAttribute")}</TableCell>
          <TableCell>{t("diffColumnChange")}</TableCell>
          <TableCell>{t("diffColumnOldValue")}</TableCell>
          <TableCell>{t("diffColumnNewValue")}</TableCell>
        </TableRow>
      </TableHead>
      <TableBody>
        {changes.map(change => (
          <TableRow key={change.id} sx={{ verticalAlign: "top" }}>
            <TableCell sx={{ wordBreak: "break-word" }}>{change.attributePath ?? t("diffWholeObject")}</TableCell>
            <TableCell>
              <Stack direction="row" sx={{ gap: 1, alignItems: "center" }}>
                <DiffChangeTypeIcon changeType={change.changeType} />
                {t(getChangeLabelKey(change))}
              </Stack>
            </TableCell>
            <TableCell>
              <DiffValue value={change.oldValue} />
            </TableCell>
            <TableCell>
              <DiffValue value={change.newValue} />
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
};

/**
 * The changes one below the other, where the panel is narrow: a heading per change, then the values before and after
 * as rows of their own, so each value has the full width. The panel scrolls as a whole, the values do not.
 */
const DiffChangesStack: FC<DiffChangesProps> = ({ changes }) => {
  const { t } = useTranslation();
  return (
    <Stack sx={{ gap: 1 }}>
      {changes.map(change => {
        const label = t(getChangeLabelKey(change));
        return (
          <Stack key={change.id} sx={{ gap: 0.5 }}>
            <Stack direction="row" sx={{ gap: 1, alignItems: "center" }}>
              <DiffChangeTypeIcon changeType={change.changeType} />
              <Typography variant="subtitle2" sx={{ wordBreak: "break-word" }}>
                {change.attributePath ? `${change.attributePath} · ${label}` : label}
              </Typography>
            </Stack>
            {(change.oldValue !== undefined || change.newValue !== undefined) && (
              <Table size="small" sx={{ tableLayout: "fixed" }}>
                <TableBody>
                  {change.oldValue !== undefined && (
                    <DetailRow label={t("diffColumnOldValue")} value={change.oldValue} />
                  )}
                  {change.newValue !== undefined && (
                    <DetailRow label={t("diffColumnNewValue")} value={change.newValue} />
                  )}
                </TableBody>
              </Table>
            )}
          </Stack>
        );
      })}
    </Stack>
  );
};

interface DiffDetailPanelProps {
  /** All changes of the selected object, at least one. */
  changes: DiffChange[];
  /** Narrow, beside the map in fullscreen: the changes one below the other instead of side by side. */
  compact?: boolean;
}

/** Shows the selected object with all its changes, side by side in a table or, where the panel is narrow, stacked. */
export const DiffDetailPanel: FC<DiffDetailPanelProps> = ({ changes, compact = false }) => {
  const { t } = useTranslation();
  // All changes belong to the same object, so the first one names it.
  const [object] = changes;

  return (
    <GeopilotBox
      data-cy="diff-detail"
      sx={{
        width: "100%",
        gap: 1,
        backgroundColor: geopilotTheme.palette.primary.states.selected,
      }}>
      <Typography variant="h6" sx={{ m: 0 }}>
        {t("diffDetailTitle")}
      </Typography>
      <Table size="small" sx={{ tableLayout: "fixed" }}>
        <TableBody>
          <DetailRow label={t("diffColumnClass")} value={object.interlisName} />
          <DetailRow label={t("diffColumnObject")} value={object.objectId} />
        </TableBody>
      </Table>
      {compact ? <DiffChangesStack changes={changes} /> : <DiffChangesTable changes={changes} />}
    </GeopilotBox>
  );
};
