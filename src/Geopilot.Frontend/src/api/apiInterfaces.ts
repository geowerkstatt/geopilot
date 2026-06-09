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

export interface Mandate {
  id: number;
  name: string;
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
  pipelineSteps: Record<string, string>[];
}

export interface Organisation {
  id: number;
  name: string;
  mandates: Mandate[];
  users: User[];
}

export interface Delivery {
  id: number;
  date: string;
  declaringUser: User;
  mandate: Mandate;
  comment: string;
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

export enum ProcessingState {
  Pending = "pending",
  Running = "running",
  Success = "success",
  Failed = "failed",
  Cancelled = "cancelled",
}

export enum StepState {
  Pending = "pending",
  Skipped = "skipped",
  Running = "running",
  Success = "success",
  Error = "error",
  Cancelled = "cancelled",
}

export interface StepDownload {
  originalFileName: string;
  url: string;
}

/** A single feature inside a feature layer of a map visualization. */
export interface MapFeature {
  /** The feature geometry as Well-Known Text (WKT), e.g. "POINT(2600000 1200000)" (EPSG:2056 / LV95). */
  geom: string;
  /** The informational text shown for the feature. */
  info: string;
}

/**
 * A single map layer. Exactly one of {@link wmts} or {@link features} is set.
 */
export interface MapLayer {
  /** The capabilities URL of a WMTS map service. Set for WMTS layers. */
  wmts?: string;
  /** Features rendered directly from the config. Set for feature layers. */
  features?: MapFeature[];
}

/** The map-visualization config produced by the map visualization pipeline step. */
export interface MapVisualizationConfig {
  /** The layers displayed in the map, drawn in order. */
  layers: MapLayer[];
}

export interface StepResult {
  id: string;
  name: Record<string, string>;
  state: StepState;
  statusMessage?: Record<string, string>;
  downloads: StepDownload[];
  mapVisualization: StepDownload[];
}

export interface ProcessingJobResponse {
  jobId: string;
  state: ProcessingState;
  mandateId?: number;
  pipelineName: Record<string, string>;
  steps: StepResult[];
  deliveryRestrictionMessage?: Record<string, string>;
}

export interface LocalisedText {
  language: string;
  text: string;
}

export interface Profile {
  id: string;
  titles: LocalisedText[];
}

export interface StartJobRequest {
  mandateId: number;
}

export interface PipelineSummary {
  id: string;
  displayName: Record<string, string>;
}

export interface AvailablePipelinesResponse {
  pipelines: PipelineSummary[];
}

export interface UploadSettings {
  enabled: boolean;
  maxFileSizeMB: number;
  maxFilesPerJob: number;
  maxJobSizeMB: number;
}

export interface CloudUploadRequest {
  files: { fileName: string; size: number }[];
}

export interface CloudUploadResponse {
  jobId: string;
  files: { fileName: string; uploadUrl: string }[];
  expiresAt: string;
}
