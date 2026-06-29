import { useContext } from "react";
import { ControlledNavigateContext } from "./controlledNavigateContext";

export const useControlledNavigate = () => useContext(ControlledNavigateContext);
