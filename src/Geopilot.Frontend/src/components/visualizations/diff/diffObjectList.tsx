import { FC } from "react";
import { useTranslation } from "react-i18next";
import { Stack, TablePagination, Typography } from "@mui/material";
import { Button } from "../../buttons";
import { DiffLayout, ReferenceTarget } from "./diffChangeRow";
import { DiffObjectGroup } from "./diffObjectGroup";
import { DIFF_PAGE_SIZES, DiffObject, getPropertyChanges } from "./diffObjects";

interface DiffObjectListProps {
  /** The objects to list, already filtered. */
  objects: DiffObject[];
  page: number;
  pageSize: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
  expandedObjectIds: ReadonlySet<string>;
  onToggleObject: (objectId: string) => void;
  /** Expands or collapses the given objects together. */
  onExpandObjects: (objectIds: string[], expanded: boolean) => void;
  /** The objects under the pointer, here or on the map. */
  hoveredObjectIds: ReadonlySet<string>;
  onHoverObject: (objectId: string | null) => void;
  onShowObjectOnMap: (objectId: string) => void;
  lookupReference: (oid: string) => ReferenceTarget;
  onReference: (oid: string) => void;
  registerObject: (objectId: string, element: HTMLDivElement | null) => void;
  layout: DiffLayout;
}

/** The objects of the diff, one group each, a page at a time. */
export const DiffObjectList: FC<DiffObjectListProps> = ({
  objects,
  page,
  pageSize,
  onPageChange,
  onPageSizeChange,
  expandedObjectIds,
  onToggleObject,
  onExpandObjects,
  hoveredObjectIds,
  onHoverObject,
  onShowObjectOnMap,
  lookupReference,
  onReference,
  registerObject,
  layout,
}) => {
  const { t } = useTranslation();
  const pageObjects = objects.slice(page * pageSize, (page + 1) * pageSize);
  const expandableIds = pageObjects
    .filter(object => getPropertyChanges(object).length > 0)
    .map(object => object.objectId);
  const allExpanded = expandableIds.length > 0 && expandableIds.every(id => expandedObjectIds.has(id));
  const changeCount = objects.reduce((count, object) => count + object.changes.length, 0);

  return (
    <Stack sx={{ width: "100%", gap: 1 }}>
      <Stack direction="row" sx={{ alignItems: "center", justifyContent: "space-between", gap: 1 }}>
        <Typography variant="body2" sx={{ fontWeight: 600 }} data-cy="diff-object-count">
          {t("diffObjectCount", { objects: objects.length, changes: changeCount })}
        </Typography>
        {expandableIds.length > 0 && (
          <Button
            size="small"
            variant="text"
            label={allExpanded ? "diffCollapseAll" : "diffExpandAll"}
            onClick={() => onExpandObjects(expandableIds, !allExpanded)}
          />
        )}
      </Stack>
      {pageObjects.map(object => (
        <DiffObjectGroup
          key={object.objectId}
          object={object}
          expanded={expandedObjectIds.has(object.objectId)}
          hovered={hoveredObjectIds.has(object.objectId)}
          layout={layout}
          onToggle={() => onToggleObject(object.objectId)}
          onShowOnMap={object.onMap ? () => onShowObjectOnMap(object.objectId) : undefined}
          onHover={onHoverObject}
          lookupReference={lookupReference}
          onReference={onReference}
          groupRef={element => registerObject(object.objectId, element)}
        />
      ))}
      <TablePagination
        component="div"
        count={objects.length}
        page={page}
        rowsPerPage={pageSize}
        rowsPerPageOptions={DIFF_PAGE_SIZES}
        onPageChange={(_event, newPage) => onPageChange(newPage)}
        onRowsPerPageChange={event => onPageSizeChange(Number(event.target.value))}
        labelRowsPerPage={t("diffObjectsPerPage")}
        labelDisplayedRows={({ from, to, count }) => t("diffPageRange", { from, to, count })}
      />
    </Stack>
  );
};
