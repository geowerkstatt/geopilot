import BaseLayer from "ol/layer/Base";

// Custom layer properties read/written by the switcher. The map sets at least TITLE on the layers it
// adds so they have a label here. The others default sensibly when absent, so existing layers work
// without any extra setup:
//   TITLE     - display name shown in the list.
//   DISPLAY   - set to false to hide a layer from the switcher entirely (still rendered on the map).
//   OPEN      - expanded/collapsed state of a group; toggled by the expand button.
//   RESULT    - internal: set by the search filter to mark whether a layer matches the query.
export const LayerSwitcherProperties = {
  TITLE: "title",
  DISPLAY: "displayInLayerSwitcher",
  OPEN: "openInLayerSwitcher",
  RESULT: "searchResultInLayerSwitcher",
} as const;

const { TITLE, DISPLAY, OPEN, RESULT } = LayerSwitcherProperties;

export const getTitle = (layer: BaseLayer): string => layer.get(TITLE) ?? "";
export const getOpen = (layer: BaseLayer): boolean => layer.get(OPEN) ?? false;
// A layer shows in the switcher when it is not explicitly hidden and (when a search is active) matches it.
export const getDisplayed = (layer: BaseLayer): boolean => (layer.get(DISPLAY) ?? true) && (layer.get(RESULT) ?? true);
