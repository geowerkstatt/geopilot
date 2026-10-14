import { DiffChange, DiffChangeType, DiffValueType } from "../../../api/apiInterfaces";

/** The change types in the order the filter offers them. */
export const DIFF_CHANGE_TYPES: readonly DiffChangeType[] = ["added", "deleted", "changed"];

/** The label (translation key) per change type, for the filter, which spans every kind of property. */
export const DIFF_CHANGE_TYPE_LABEL_KEYS: Record<DiffChangeType, string> = {
  added: "diffChangeAdded",
  deleted: "diffChangeDeleted",
  changed: "diffChangeChanged",
};

/**
 * The label (translation key) of a change by what changed and how, so an object deleted as a whole reads differently
 * from a reference removed from an object that stays.
 */
const DIFF_CHANGE_LABEL_KEYS: Record<DiffValueType, Record<DiffChangeType, string>> = {
  object: { added: "diffChangeObjectAdded", deleted: "diffChangeObjectDeleted", changed: "diffChangeObjectChanged" },
  reference: {
    added: "diffChangeReferenceAdded",
    deleted: "diffChangeReferenceDeleted",
    changed: "diffChangeReferenceChanged",
  },
  attribute: {
    added: "diffChangeAttributeAdded",
    deleted: "diffChangeAttributeDeleted",
    changed: "diffChangeAttributeChanged",
  },
  geometry: {
    added: "diffChangeGeometryAdded",
    deleted: "diffChangeGeometryDeleted",
    changed: "diffChangeGeometryChanged",
  },
};

/** The label (translation key) of a change, e.g. "Objekt gelöscht" or "Referenz entfernt". */
export const getChangeLabelKey = (change: DiffChange): string =>
  DIFF_CHANGE_LABEL_KEYS[change.valueType][change.changeType];

/** The class of a qualified INTERLIS name, e.g. "Class" of "Model.Topic.Class". */
export const getClassName = (interlisName: string): string => interlisName.split(".").at(-1) ?? interlisName;

/** How many changes there are of each change type. */
export const countByChangeType = (changes: DiffChange[]): Record<DiffChangeType, number> => {
  const counts: Record<DiffChangeType, number> = { added: 0, deleted: 0, changed: 0 };
  for (const change of changes) counts[change.changeType]++;
  return counts;
};

/**
 * The changes of the selected change types (all of them when none is selected) that contain the query, ignoring
 * case, in the object id, the class, the attribute or one of the values.
 */
export const filterChanges = (
  changes: DiffChange[],
  query: string,
  changeTypes: readonly DiffChangeType[],
): DiffChange[] => {
  const normalizedQuery = query.trim().toLowerCase();
  return changes.filter(
    change =>
      (changeTypes.length === 0 || changeTypes.includes(change.changeType)) &&
      (normalizedQuery.length === 0 ||
        [change.objectId, change.interlisName, change.attributePath, change.oldValue, change.newValue].some(text =>
          text?.toLowerCase().includes(normalizedQuery),
        )),
  );
};
