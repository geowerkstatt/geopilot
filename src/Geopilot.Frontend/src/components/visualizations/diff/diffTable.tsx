import { FC, MouseEvent, RefObject, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { Box, Stack, useTheme } from "@mui/material";
import { GridActionsCell, GridActionsCellItem, GridApi, GridColDef } from "@mui/x-data-grid";
import { DiffChange, DiffChangeType, MapLayerStyle } from "../../../api/apiInterfaces";
import { TranslationKey } from "../../../appInterfaces";
import { px2rem } from "../../../appTheme";
import { stopStepSwipePropagation } from "../../../hooks/useStepSwipe";
import GeopilotDataGrid from "../../grids/geopilotDataGrid";
import { getChangeLabelKey, getClassName } from "./diffChanges";
import { DiffChangeTypeIcon } from "./diffChangeTypeIcon";
import { DiffStateSwatches } from "./diffStateSwatches";

const SELECTED_OBJECT_CLASS = "diff-selected-object";
const HOVERED_OBJECT_CLASS = "diff-hovered-object";
const MAX_CELL_VALUE_LENGTH = 200;

/** Cuts a long value, a BLACKBOX for example, for its cell; the detail shows it in full. */
const shortenValue = (value?: string) =>
  value && value.length > MAX_CELL_VALUE_LENGTH ? `${value.slice(0, MAX_CELL_VALUE_LENGTH)}…` : value;

/** The label (translation key) of the button that shows an object on the map, by the states it is drawn in. */
const showOnMapLabelKey = (states: readonly MapLayerStyle[]): TranslationKey => {
  if (states.length > 1) return "diffShowOnMapBoth";
  return states[0] === "old" ? "diffShowOnMapOld" : "diffShowOnMapNew";
};

interface DiffTableProps {
  /** Lets the visualization turn the table to the page of an object selected on the map. */
  apiRef: RefObject<GridApi | null>;
  /** The changes to list, already filtered. */
  changes: DiffChange[];
  selectedObjectId: string | null;
  /** The objects under the pointer, on the map or in the table. */
  hoveredObjectIds: readonly string[];
  /** The states each object is drawn in on the map; an object without an entry is not on the map. */
  objectStates: ReadonlyMap<string, readonly MapLayerStyle[]>;
  onSelectObject: (objectId: string) => void;
  /** Called with the object of the row under the pointer, and with null once the pointer leaves the row. */
  onHoverObject: (objectId: string | null) => void;
  onShowObjectOnMap: (objectId: string) => void;
  /** Narrow, beside the map in fullscreen: the object and the values are left to the detail below the table. */
  compact?: boolean;
}

const ALL_COLUMNS = {};
const COMPACT_HIDDEN_COLUMNS = { objectId: false, oldValue: false, newValue: false };

/**
 * Lists the changes, one row per change. The rows of the selected object and of the object under the pointer are
 * highlighted, and each row shows in the map colours in which states its object is drawn.
 */
export const DiffTable: FC<DiffTableProps> = ({
  apiRef,
  changes,
  selectedObjectId,
  hoveredObjectIds,
  objectStates,
  onSelectObject,
  onHoverObject,
  onShowObjectOnMap,
  compact = false,
}) => {
  const { t } = useTranslation();
  const theme = useTheme();
  const objectIdByChangeId = useMemo(() => new Map(changes.map(change => [change.id, change.objectId])), [changes]);

  const columns = useMemo<GridColDef<DiffChange>[]>(
    () => [
      {
        field: "changeType",
        headerName: t("diffColumnChange"),
        ...(compact ? { flex: 1, minWidth: 120 } : { width: 200 }),
        valueGetter: (_value: DiffChangeType, row: DiffChange) => t(getChangeLabelKey(row)),
        renderCell: params => (
          <Stack direction="row" sx={{ gap: 1, alignItems: "center", height: "100%" }}>
            <DiffChangeTypeIcon changeType={params.row.changeType} />
            {params.value}
          </Stack>
        ),
      },
      {
        field: "interlisName",
        headerName: t("diffColumnClass"),
        flex: 1,
        minWidth: compact ? 90 : 140,
        valueGetter: (value: string) => getClassName(value),
      },
      { field: "objectId", headerName: t("diffColumnObject"), flex: 1, minWidth: 140 },
      {
        field: "attributePath",
        headerName: t("diffColumnAttribute"),
        flex: 1,
        minWidth: compact ? 90 : 120,
        valueGetter: (value: string | undefined) => value ?? t("diffWholeObject"),
      },
      {
        field: "oldValue",
        headerName: t("diffColumnOldValue"),
        flex: 1,
        minWidth: 120,
        valueFormatter: shortenValue,
      },
      {
        field: "newValue",
        headerName: t("diffColumnNewValue"),
        flex: 1,
        minWidth: 120,
        valueFormatter: shortenValue,
      },
      {
        field: "states",
        type: "actions",
        headerName: t("diffColumnLocation"),
        width: compact ? 64 : 104,
        resizable: false,
        renderCell: params => {
          const states = objectStates.get(params.row.objectId) ?? [];
          const label = t(showOnMapLabelKey(states));
          return (
            <GridActionsCell {...params}>
              {states.length > 0 && (
                <GridActionsCellItem
                  icon={<DiffStateSwatches states={states} title={label} />}
                  label={label}
                  onClick={() => onShowObjectOnMap(params.row.objectId)}
                />
              )}
            </GridActionsCell>
          );
        },
      },
    ],
    [t, objectStates, onShowObjectOnMap, compact],
  );

  const getRowClassName = (objectId: string) => {
    if (objectId === selectedObjectId) return SELECTED_OBJECT_CLASS;
    return hoveredObjectIds.includes(objectId) ? HOVERED_OBJECT_CLASS : "";
  };

  const hoverRow = (event: MouseEvent<HTMLDivElement>) =>
    onHoverObject(objectIdByChangeId.get(event.currentTarget.dataset.id ?? "") ?? null);

  return (
    <Box
      // A horizontal swipe scrolls the table instead of turning the delivery step.
      {...stopStepSwipePropagation}
      sx={{
        width: "100%",
        flexShrink: 0,
        height: compact ? px2rem(320) : px2rem(448),
        [`& .${HOVERED_OBJECT_CLASS}`]: { backgroundColor: theme.palette.primary.states.hover },
        [`& .${SELECTED_OBJECT_CLASS}`]: { backgroundColor: theme.palette.primary.states.selected },
      }}>
      <GeopilotDataGrid
        name="xtfDiff"
        apiRef={apiRef}
        rows={changes}
        columns={columns}
        columnVisibilityModel={compact ? COMPACT_HIDDEN_COLUMNS : ALL_COLUMNS}
        disableColumnFilter
        disableRowSelectionOnClick
        onRowClick={params => onSelectObject(params.row.objectId)}
        // Enter and space select as a click does, so objects without a location are reachable from the keyboard too.
        // The location column leaves the keys to its button.
        onCellKeyDown={(params, event) => {
          if (params.field === "states" || (event.key !== "Enter" && event.key !== " ")) return;
          event.preventDefault();
          onSelectObject(params.row.objectId);
        }}
        getRowClassName={params => getRowClassName(params.row.objectId)}
        slotProps={{
          // Replaces the slot props of the wrapper, so the root keeps its data-cy here.
          root: { "data-cy": "xtfDiff-grid" },
          row: { onMouseEnter: hoverRow, onMouseLeave: () => onHoverObject(null) },
        }}
      />
    </Box>
  );
};
