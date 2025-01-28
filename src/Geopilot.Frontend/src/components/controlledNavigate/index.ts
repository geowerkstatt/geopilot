import { useContext } from "react";
import { ControlledNavigateContext } from "./controlledNavigateProvider.tsx";

export const useControlledNavigate = () => useContext(ControlledNavigateContext);
