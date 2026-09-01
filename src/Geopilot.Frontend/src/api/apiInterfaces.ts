import { ProcessingJobResponse, StepState } from "./generated";

export enum ContentType {
  Json = "application/json",
  Markdown = "ext/markdown",
  PlainText = "text/plain",
}

export interface FetchParams extends RequestInit {
  errorMessageLabel?: string;
  responseType?: ContentType;
}

export class ApiError extends Error {
  status?: number;

  constructor(message: string, status?: number) {
    super(message);
    this.name = "ApiError";
    this.message = message;
    this.status = status;
  }
}

/**
 * The state a step of the delivery wizard can be in. A wizard step never carries `Cancelled`
 * (a cancelled job is reported on the processing step as `Error`), but can also be `Enabled`
 * (ready for the user to click) while `Enabled` is not a valid step state.
 */
export const DeliveryStepState = {
  Pending: StepState.Pending,
  Skipped: StepState.Skipped,
  Running: StepState.Running,
  Success: StepState.Success,
  Error: StepState.Error,
  Warning: StepState.Warning,
  DeliveryRestriction: StepState.DeliveryRestriction,
  Enabled: "enabled",
} as const;

export type DeliveryStepState = (typeof DeliveryStepState)[keyof typeof DeliveryStepState];

/** A backend multilingual string, keyed by ISO 639 language code ("de", "fr", "it", "en"). */
export type LocalizedText = Record<string, string>;

/** A single feature inside a feature layer of a map visualization. */
interface MapFeature {
  /** Stable id of the validation error this feature represents, shared with its tree node for cross-select. */
  errorId: string;
  /** The feature geometry as Well-Known Text (WKT), e.g. "POINT(2600000 1200000)" (EPSG:2056 / LV95). */
  geom: string;
  /** The informational text shown for the feature. */
  info: string;
}

/**
 * A single map layer. Exactly one of {@link wmts} or {@link features} is set.
 */
export interface MapLayer {
  /** Localized display title of the layer. Shown in the layer switcher. */
  title?: LocalizedText;
  /** The capabilities URL of a WMTS map service. Set for WMTS layers. */
  wmts?: string;
  /**
   * Identifiers of the layers to display from the WMTS service referenced by {@link wmts}. When omitted
   * or empty, all layers the service advertises are displayed (wrapped in a group layer if more than one).
   * Only meaningful for WMTS layers.
   */
  layerIds?: string[];
  /** Attribution / data-owner credit for the layer (e.g. "swisstopo"); shown as a copyright credit, the client prepends a localized "©" label. */
  attribution?: string;
  /** Optional URL the attribution links to; when set, the credit is rendered as a link. */
  attributionUrl?: string;
  /** Features rendered directly from the config. Set for feature layers. */
  features?: MapFeature[];
}

/** The map-visualization payload produced by the map visualization pipeline step. */
export interface MapVisualizationConfig {
  /** The layers displayed in the map, drawn in order. */
  layers: MapLayer[];
}

/** A categorical field of an error-tree item the tree can be grouped and filtered by. */
export type TreeField = "errorType" | "model" | "topic" | "class";

/** A flat item of the error-tree visualization; the frontend groups the items into the displayed hierarchy. */
export interface TreeItem {
  /** Stable id correlating this item with its map feature for cross-select. Absent on items without a feature. */
  id?: string;
  /** Error or warning; drives the leaf's icon and colour. */
  severity: "error" | "warning";
  /** The classified error category as a localized label. Absent when the message matches no known category. */
  errorType?: LocalizedText;
  /** The TID of the failing object. Shown as the leaf text when present. */
  tid?: string;
  /** The INTERLIS model of the failing object; model, topic and class are either all set or all absent. */
  model?: string;
  /** The INTERLIS topic of the failing object. */
  topic?: string;
  /** The INTERLIS class of the failing object. */
  class?: string;
  /** The validator message. Shown as the leaf text when there is no TID. */
  message: string;
  /** The line number in the validated file. */
  line?: number;
  /** The error's coordinates as a preformatted "C1, C2" display string. */
  coordinates?: string;
}

/** The error-tree visualization payload produced by the pipeline step. */
export interface TreeVisualizationConfig {
  /** The flat items; the frontend groups them on {@link groupBy} to build the displayed tree. */
  items: TreeItem[];
  /** The fields to group by, outermost first (e.g. ["model", "topic", "class"]). */
  groupBy: TreeField[];
}

/**
 * A processing job as used throughout the app: identical to {@link ProcessingJobResponse} except the
 * job-level {@link state} has been normalized to the shared {@link StepState} (see {@link normalizeProcessingJob}).
 */
export interface NormalizedProcessingJobResponse extends Omit<ProcessingJobResponse, "state"> {
  state: StepState;
}
