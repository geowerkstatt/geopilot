import BaseLayer from "ol/layer/Base";

// Custom layer properties read/written by the switcher. The map sets at least TITLE on the layers it
// adds so they have a label here. The others default sensibly when absent, so existing layers work
// without any extra setup:
//   TITLE     - display name shown in the list.
//   OPEN      - expanded/collapsed state of a group; toggled by the expand button.
//   RESULT    - internal: set by the search filter to mark whether a layer matches the query.
//   HIDDEN    - keeps a layer out of the list, e.g. one that only decorates the others.
export const LayerSwitcherProperties = {
  TITLE: "title",
  OPEN: "openInLayerSwitcher",
  RESULT: "searchResultInLayerSwitcher",
  HIDDEN: "hiddenInLayerSwitcher",
} as const;

const { TITLE, OPEN, RESULT, HIDDEN } = LayerSwitcherProperties;

export const getTitle = (layer: BaseLayer): string => layer.get(TITLE) ?? "";
export const getOpen = (layer: BaseLayer): boolean => layer.get(OPEN) ?? false;
// A layer shows in the switcher when it is not hidden and matches the active search.
export const getDisplayed = (layer: BaseLayer): boolean => layer.get(HIDDEN) !== true && (layer.get(RESULT) ?? true);
