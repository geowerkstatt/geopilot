export interface FetchParams extends RequestInit {
  errorMessageLabel?: string;
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
}

export interface Coordinate {
  x: number | null;
  y: number | null;
}

export interface Mandate {
  id: number;
  name: string;
  fileTypes: string[];
  coordinates: Coordinate[];
  organisations: Organisation[] | number[];
  deliveries: Delivery[];
}

export interface Organisation {
  id: number;
  name: string;
  mandates: Mandate[] | number[];
  users: User[] | number[];
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
  organisations: Organisation[] | number[];
  deliveries: Delivery[];
}
