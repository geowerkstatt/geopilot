import { FC } from "react";
import { useTranslation } from "react-i18next";
import UnfoldLessIcon from "@mui/icons-material/UnfoldLess";
import UnfoldMoreIcon from "@mui/icons-material/UnfoldMore";
import { Stack, Typography } from "@mui/material";
import { DiffChange, DiffState } from "../../../api/apiInterfaces";
import { Button } from "../../buttons";
import { DiffLayout, ReferenceTarget } from "./diffChangeRow";
import { DiffObjectGroup } from "./diffObjectGroup";
import { DiffObject, getPropertyChanges } from "./diffObjects";

interface DiffObjectListProps {
  /** The objects to list, already filtered. */
  objects: DiffObject[];
  totalChangeCount: number;
  filterActive: boolean;
  expandedObjectIds: ReadonlySet<string>;
  onExpandObjects: (objectIds: string[], expanded: boolean) => void;
  /** The objects under the pointer, here or on the map. */
  hoveredObjectIds: ReadonlySet<string>;
  onHoverObjects: (objectIds: string[]) => void;
  onShowObjectOnMap: (objectId: string) => void;
  onShowChangeOnMap: (change: DiffChange, state: DiffState) => void;
  lookupReference: (oid: string) => ReferenceTarget;
  onReference: (oid: string) => void;
  registerObject: (objectId: string, element: HTMLDivElement | null) => void;
  layout: DiffLayout;
}

export const DiffObjectList: FC<DiffObjectListProps> = ({
  objects,
  totalChangeCount,
  filterActive,
  expandedObjectIds,
  onExpandObjects,
  hoveredObjectIds,
  onHoverObjects,
  onShowObjectOnMap,
  onShowChangeOnMap,
  lookupReference,
  onReference,
  registerObject,
  layout,
}) => {
  const { t } = useTranslation();
  const expandableIds = objects.filter(object => getPropertyChanges(object).length > 0).map(object => object.objectId);
  const anyExpanded = expandableIds.some(id => expandedObjectIds.has(id));
  const shownChangeCount = objects.reduce((count, object) => count + object.changes.length, 0);

  return (
    <Stack sx={{ width: "100%", gap: 1 }}>
      <Stack direction="row" sx={{ alignItems: "center", justifyContent: "space-between", gap: 1 }}>
        <Typography variant="body2" data-cy="diff-change-count">
          {filterActive
            ? t("diffChangeCountFiltered", { count: totalChangeCount, shown: shownChangeCount })
            : t("diffChangeCount", { count: totalChangeCount })}
        </Typography>
        {expandableIds.length > 0 && (
          <Button
            size="small"
            variant="text"
            label={anyExpanded ? "collapseAll" : "expandAll"}
            endIcon={anyExpanded ? <UnfoldLessIcon /> : <UnfoldMoreIcon />}
            onClick={() => onExpandObjects(expandableIds, !anyExpanded)}
          />
        )}
      </Stack>
      {objects.map(object => (
        <DiffObjectGroup
          key={object.objectId}
          object={object}
          expanded={expandedObjectIds.has(object.objectId)}
          hovered={hoveredObjectIds.has(object.objectId)}
          layout={layout}
          onExpand={onExpandObjects}
          onShowOnMap={object.onMap ? onShowObjectOnMap : undefined}
          onShowChangeOnMap={object.onMap ? onShowChangeOnMap : undefined}
          onHover={onHoverObjects}
          lookupReference={lookupReference}
          onReference={onReference}
          registerObject={registerObject}
        />
      ))}
    </Stack>
  );
};
