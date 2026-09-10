import { createContext } from "react";
import { User } from "../api/generated";

export const UserContext = createContext<User | null | undefined>(undefined);
