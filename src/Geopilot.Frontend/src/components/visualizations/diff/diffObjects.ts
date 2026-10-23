import { DiffChange, DiffChangeType, DiffValueType } from "../../../api/apiInterfaces";
import { FieldFilters, FilterAttribute } from "../filterBar";

export const DIFF_CHANGE_TYPE_LABEL_KEYS: Record<DiffChangeType, string> = {
  added: "diffChangeAdded",
  deleted: "diffChangeDeleted",
  changed: "diffChangeChanged",
};

export const DIFF_VALUE_TYPE_LABEL_KEYS: Record<DiffValueType, string> = {
  object: "diffValueTypeObject",
  reference: "diffValueTypeReference",
  attribute: "diffValueTypeAttribute",
  geometry: "diffValueTypeGeometry",
};

export const DIFF_OBJECT_LABEL_KEYS: Record<Exclude<DiffChangeType, "changed">, string> = {
  added: "diffChangeObjectAdded",
  deleted: "diffChangeObjectDeleted",
};

/** The class of a qualified INTERLIS name, e.g. "Class" of "Model.Topic.Class". */
export const getClassName = (interlisName: string): string => interlisName.split(".").at(-1) ?? interlisName;

export interface DiffObject {
  objectId: string;
  interlisName: string;
  kind: DiffChangeType;
  /** In diff order, including the change of the object as a whole, which the filter needs. */
  changes: DiffChange[];
  onMap: boolean;
}

export const getPropertyChanges = (object: DiffObject): DiffChange[] =>
  object.changes.filter(change => change.valueType !== "object");

/**
 * Groups the changes by their object, the objects in the order they first appear in the diff.
 * @param mappedObjectIds The ids of the features on the map, which are the ids of their objects.
 */
export const groupByObject = (changes: DiffChange[], mappedObjectIds: ReadonlySet<string>): DiffObject[] => {
  const objects = new Map<string, DiffObject>();
  for (const change of changes) {
    const object = objects.get(change.objectId);
    if (object) object.changes.push(change);
    else
      objects.set(change.objectId, {
        objectId: change.objectId,
        interlisName: change.interlisName,
        kind: "changed",
        changes: [change],
        onMap: mappedObjectIds.has(change.objectId),
      });
  }
  return [...objects.values()].map(object => {
    const objectChange = object.changes.find(change => change.valueType === "object");
    return objectChange ? { ...object, kind: objectChange.changeType } : object;
  });
};

// The minus is the sign U+2212, not a hyphen.
export const SUMMARY_PREFIXES: Record<DiffChangeType, string> = { added: "+", deleted: "−", changed: "" };

export interface SummaryItem {
  path: string;
  changeType: DiffChangeType;
}

/**
 * The properties an object's changes touch, each once in the order of the diff. A property with changes of
 * different types, a reference removed and another one added for example, counts as changed.
 */
export const summarize = (object: DiffObject): SummaryItem[] => {
  const items = new Map<string, DiffChangeType>();
  for (const change of getPropertyChanges(object)) {
    const path = change.attributePath ?? "";
    const known = items.get(path);
    items.set(path, known && known !== change.changeType ? "changed" : change.changeType);
  }
  return [...items].map(([path, changeType]) => ({ path, changeType }));
};

export type DiffFilterField = "changeType" | "valueType" | "class" | "attribute";

const DIFF_FILTER_FIELDS: readonly DiffFilterField[] = ["changeType", "valueType", "class", "attribute"];

const DIFF_FILTER_LABEL_KEYS: Record<DiffFilterField, string> = {
  changeType: "diffFieldChangeType",
  valueType: "diffFieldValueType",
  class: "diffColumnClass",
  attribute: "diffColumnAttribute",
};

export type ChangeLabeler = (field: DiffFilterField, change: DiffChange) => string | undefined;

const matchesQuery = (object: DiffObject, change: DiffChange, query: string) =>
  [object.objectId, object.interlisName, change.attributePath, change.oldValue, change.newValue].some(text =>
    text?.toLowerCase().includes(query),
  );

const matchesFilters = (change: DiffChange, filters: FieldFilters<DiffFilterField>, label: ChangeLabeler) =>
  DIFF_FILTER_FIELDS.every(field => {
    const selected = filters[field];
    if (!selected || selected.length === 0) return true;
    const value = label(field, change);
    return value !== undefined && selected.includes(value);
  });

/**
 * Keeps the objects with at least one change that matches every selected filter and the query, ignoring case, in
 * the object id, the class, the attribute or a value. Each object keeps only its matching changes.
 */
export const filterObjects = (
  objects: DiffObject[],
  query: string,
  filters: FieldFilters<DiffFilterField>,
  label: ChangeLabeler,
): DiffObject[] => {
  const normalizedQuery = query.trim().toLowerCase();
  return objects.flatMap(object => {
    const changes = object.changes.filter(
      change =>
        matchesFilters(change, filters, label) &&
        (normalizedQuery.length === 0 || matchesQuery(object, change, normalizedQuery)),
    );
    return changes.length > 0 ? [{ ...object, changes }] : [];
  });
};

/** The filter fields with the distinct values of the changes, sorted; a field without any value is left out. */
export const filterOptions = (objects: DiffObject[], label: ChangeLabeler): FilterAttribute<DiffFilterField>[] =>
  DIFF_FILTER_FIELDS.map(field => {
    const values = objects.flatMap(object => object.changes.flatMap(change => label(field, change) ?? []));
    return {
      field,
      label: DIFF_FILTER_LABEL_KEYS[field],
      options: [...new Set(values)].sort((a, b) => a.localeCompare(b)),
    };
  }).filter(attribute => attribute.options.length > 0);
