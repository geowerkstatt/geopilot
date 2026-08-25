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

export enum FieldEvaluationType {
  NotEvaluated = "notEvaluated",
  Optional = "optional",
  Required = "required",
}

export interface Coordinate {
  x: number | undefined;
  y: number | undefined;
}

export interface MandateSummary {
  id: number;
  name: LocalizedText;
  description: LocalizedText;
  allowDelivery: boolean;
  evaluatePrecursorDelivery: FieldEvaluationType;
  evaluatePartial: FieldEvaluationType;
  evaluateComment: FieldEvaluationType;
}

export interface Mandate {
  id: number;
  name: LocalizedText;
  description: LocalizedText;
  isPublic: boolean;
  allowDelivery: boolean;
  fileTypes: string[];
  coordinates: Coordinate[];
  organisations: Organisation[];
  deliveries: Delivery[];
  evaluatePrecursorDelivery?: FieldEvaluationType;
  evaluatePartial?: FieldEvaluationType;
  evaluateComment?: FieldEvaluationType;
  pipelineId?: string;
}

export interface Organisation {
  id: number;
  name: string;
  mandates: Mandate[];
  users: User[];
}

export interface DeliverySummary {
  id: number;
  date: string;
}

export interface Delivery {
  id: number;
  date: string;
  declaringUser: User;
  mandate: Mandate;
  comment: string;
  canDelete?: boolean;
}

export enum UserState {
  Inactive = "inactive",
  Active = "active",
}

export interface User {
  id: number;
  fullName: string;
  isAdmin: boolean;
  state: UserState;
  email: string;
  organisations: Organisation[];
  deliveries?: Delivery[];
}

export interface ProcessingSettings {
  allowedFileExtensions: string[];
}

export enum StepState {
  Enabled = "enabled",
  Pending = "pending",
  Skipped = "skipped",
  Running = "running",
  Success = "success",
  Error = "error",
  Cancelled = "cancelled",
  Warning = "warning",
  DeliveryRestriction = "deliveryRestriction",
}

interface StepDownload {
  originalFileName: string;
  url: string;
}

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

interface StepVisualization {
  originalFileName: string;
  url: string;
}

export interface StepResult {
  id: string;
  name: LocalizedText;
  state: StepState;
  statusMessage?: LocalizedText;
  conditionMessage?: LocalizedText;
  downloads: StepDownload[];
  deliveries: string[];
  visualizations: StepVisualization[];
}

/**
 * The aggregate lifecycle state of a processing job, as serialized by the backend
 * (`Geopilot.Pipeline.ProcessingState`). It is a rollup of the job's pipeline steps, not the state of any
 * single step, and is deliberately kept separate from {@link StepState}. The two diverge by design: a step
 * (a single processor) that does not succeed carries an error, hence {@link StepState.Error}, whereas a job
 * whose pipeline could not run to completion because a step errored has failed, hence the job-level
 * `"failed"`. The job level likewise has no `"skipped"` or `"enabled"`. {@link normalizeProcessingJob} maps it onto
 * {@link StepState} at the fetch boundary so the job state can be shown with the same `StepState`-based UI
 * (the shared `StepIcon`) as the individual pipeline steps.
 */
export type ProcessingState =
  | "pending"
  | "running"
  | "success"
  | "failed"
  | "cancelled"
  | "warning"
  | "deliveryRestriction";

/** A processing job exactly as returned by the processing API, before the job state is normalized. */
export interface RawProcessingJobResponse {
  jobId: string;
  state: ProcessingState;
  mandateId?: number;
  pipelineName: LocalizedText;
  steps: StepResult[];
}

/**
 * A processing job as used throughout the app: identical to {@link RawProcessingJobResponse} except the
 * job-level {@link state} has been normalized to the shared {@link StepState} (see {@link normalizeProcessingJob}).
 */
export interface ProcessingJobResponse extends Omit<RawProcessingJobResponse, "state"> {
  state: StepState;
}

export interface StartJobRequest {
  uploadId: string;
  mandateId: number;
}

export interface PipelineSummary {
  id: string;
  displayName: LocalizedText;
}

export interface AvailablePipelinesResponse {
  pipelines: PipelineSummary[];
}

export interface UploadSettings {
  maxFileSizeMB: number;
  maxFilesPerJob: number;
  maxJobSizeMB: number;
}

export interface InitiateUploadResponse {
  uploadId: string;
  files: { fileName: string; uploadUrl: string }[];
  expiresAt: string;
}
