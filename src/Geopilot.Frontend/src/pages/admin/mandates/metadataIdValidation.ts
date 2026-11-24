import { useEffect, useRef } from "react";
import { useFormContext } from "react-hook-form";
import { TFunction } from "i18next";
import { Language } from "../../../appInterfaces.ts";

// The rule of the server. An id is a path segment in the delivery form: react-hook-form reads a leading digit as an
// array index and refuses constructor and prototype.
const metadataIdPattern = /^(?!(?:constructor|prototype)$)[a-z][a-z0-9_]{0,63}$/;

/** Checks the id of a metadata field or option against the pattern and against its siblings, the entry included. */
export const validateMetadataId = (
  t: TFunction,
  id: string,
  siblings: { id: string }[] | null | undefined,
): string | boolean => {
  if (!metadataIdPattern.test(id)) {
    return t("metadataIdInvalid");
  }
  return (siblings ?? []).filter(sibling => sibling.id === id).length < 2 || t("metadataIdDuplicate");
};

export const idFieldNames = (listName: string, count: number) =>
  Array.from({ length: count }, (_, index) => `${listName}.${index}.id`);

const titleFieldNames = (listName: string, count: number) =>
  Array.from({ length: count }, (_, index) =>
    Object.values(Language).map(language => `${listName}.${index}.title.${language}`),
  );

/**
 * Keeps a fixed duplicate id or title from leaving its error behind, which would keep applying blocked. Each id and
 * title input names the ids or titles of the other entries as deps, so changing one checks them all. A removal moves
 * the entries after it up, and they take their new names only when they render, so they are checked again after that
 * render. The removed entry takes the focus along with its delete button, so the id of the entry now in its place, or
 * of the new last entry, gets it.
 */
export const useDuplicateRecheck = (listName: string, count: number, remove: (index: number) => void) => {
  const { setFocus, trigger } = useFormContext();
  const removedIndex = useRef<number | undefined>(undefined);

  useEffect(() => {
    if (removedIndex.current !== undefined) {
      setFocus(`${listName}.${Math.min(removedIndex.current, count - 1)}.id`);
      removedIndex.current = undefined;
      trigger([...idFieldNames(listName, count), ...titleFieldNames(listName, count).flat()]);
    }
  }, [count, listName, setFocus, trigger]);

  return {
    otherIds: (index: number) => idFieldNames(listName, count).filter((_, other) => other !== index),
    otherTitles: (index: number) =>
      titleFieldNames(listName, count)
        .filter((_, other) => other !== index)
        .flat(),
    removeEntry: (index: number) => {
      removedIndex.current = index;
      remove(index);
    },
  };
};
