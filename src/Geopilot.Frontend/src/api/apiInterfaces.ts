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
