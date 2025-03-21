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

export interface ApiContextInterface {
  fetchApi: <T>(url: string, options?: Partial<FetchParams>) => Promise<T>;
  fetchLocalizedMarkdown: (markdown: string, language: string) => Promise<string | undefined>;
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
  fileTypes: string[];
  coordinates: Coordinate[];
  organisations: Organisation[];
  deliveries: Delivery[];
  evaluatePrecursorDelivery?: FieldEvaluationType;
  evaluatePartial?: FieldEvaluationType;
  evaluateComment?: FieldEvaluationType;
}

export interface Organisation {
  id: number;
  name: string;
  mandates: Mandate[];
  users: User[];
}

export interface Delivery {
  id: number;
  date: Date;
  declaringUser: User;
  mandate: Mandate;
  comment: string;
}

export interface User {
  id: number;
  fullName: string;
  isAdmin: boolean;
  email: string;
  organisations: Organisation[];
  deliveries?: Delivery[];
}

export interface ValidationSettings {
  allowedFileExtensions: string[];
}

export enum ValidationStatus {
  Processing = "processing",
  Completed = "completed",
  CompletedWithErrors = "completedWithErrors",
  Failed = "failed",
}

export interface ValidatorResult {
  status: string;
  statusMessage: string;
  logFiles: Record<string, string>;
}

export interface ValidationResponse {
  jobId: string;
  status: ValidationStatus;
  validatorResults: Record<string, ValidatorResult>;
}
