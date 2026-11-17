import { useEffect, useRef } from "react";
import { useFormContext } from "react-hook-form";
import { Language } from "../../../appInterfaces.ts";

// The rule of the server. An id is a path segment in the delivery form: react-hook-form reads a leading digit as an
// array index and refuses constructor and prototype.
const metadataIdPattern = /^(?!(?:constructor|prototype)$)[a-z][a-z0-9_]{0,63}$/;

/** Checks the id of a metadata field or option against the pattern and against its siblings, the entry included. */
export const validateMetadataId = (id: string, siblings: { id: string }[] | null | undefined): string | boolean => {
  if (!metadataIdPattern.test(id)) {
    return "metadataIdInvalid";
  }
  return (siblings ?? []).filter(sibling => sibling.id === id).length < 2 || "metadataIdDuplicate";
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
 * render.
 */
export const useDuplicateRecheck = (listName: string, count: number, remove: (index: number) => void) => {
  const { trigger } = useFormContext();
  const removed = useRef(false);

  useEffect(() => {
    if (removed.current) {
      removed.current = false;
      trigger([...idFieldNames(listName, count), ...titleFieldNames(listName, count).flat()]);
    }
  }, [count, listName, trigger]);

  return {
    otherIds: (index: number) => idFieldNames(listName, count).filter((_, other) => other !== index),
    otherTitles: (index: number) =>
      titleFieldNames(listName, count)
        .filter((_, other) => other !== index)
        .flat(),
    removeEntry: (index: number) => {
      removed.current = true;
      remove(index);
    },
  };
};
