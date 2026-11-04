import { useEffect, useRef } from "react";
import { useFormContext } from "react-hook-form";

// The rule of the server. An id that starts with a digit would read as an array index in the delivery form.
const metadataIdPattern = /^[a-z][a-z0-9_]{0,63}$/;

/** Checks the id of a metadata field or option against the pattern and against its siblings, the entry included. */
export const validateMetadataId = (id: string, siblings: { id: string }[] | null | undefined): string | boolean => {
  if (!metadataIdPattern.test(id)) {
    return "metadataIdInvalid";
  }
  return (siblings ?? []).filter(sibling => sibling.id === id).length < 2 || "metadataIdDuplicate";
};

const idFieldNames = (listName: string, count: number) =>
  Array.from({ length: count }, (_, index) => `${listName}.${index}.id`);

/**
 * Keeps a fixed duplicate id from leaving its error behind, which would keep saving disabled. Each id input names the
 * other ids of its list as deps, so changing one checks them all. A removal moves the entries after it up, and they
 * take their new names only when they render, so the ids are checked again after that render.
 */
export const useIdRecheck = (listName: string, count: number, remove: (index: number) => void) => {
  const { trigger } = useFormContext();
  const removed = useRef(false);

  useEffect(() => {
    if (removed.current) {
      removed.current = false;
      trigger(idFieldNames(listName, count));
    }
  }, [count, listName, trigger]);

  return {
    otherIds: (index: number) => idFieldNames(listName, count).filter((_, other) => other !== index),
    removeEntry: (index: number) => {
      removed.current = true;
      remove(index);
    },
  };
};
