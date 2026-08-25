import { MouseEvent, useCallback, useContext } from "react";
import { ControlledNavigateContext } from "./controlledNavigateContext";

export const useControlledNavigate = () => useContext(ControlledNavigateContext);

/**
 * Click handler for navigation elements rendered as real links (href) that should still
 * navigate through the SPA router and the unsaved-changes check. Unmodified left clicks
 * are intercepted; modified clicks (ctrl/cmd/shift/alt, middle click) keep the browser's
 * native link behavior, e.g. opening a new tab.
 */
export const useControlledLinkClick = () => {
  const { navigateTo } = useControlledNavigate();
  return useCallback(
    (path: string) => (event: MouseEvent<HTMLElement>) => {
      if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
        return;
      }
      event.preventDefault();
      navigateTo(path);
    },
    [navigateTo],
  );
};
