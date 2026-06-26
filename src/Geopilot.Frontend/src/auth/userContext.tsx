import { createContext } from "react";
import { User } from "../api/apiInterfaces";

export const UserContext = createContext<User | null | undefined>(undefined);
