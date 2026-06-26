import { createContext } from "react";

interface ControlledNavigateContextValue {
  navigateTo: (path: string) => void;
  checkIsDirty: boolean;
  registerCheckIsDirty: (path: string) => void;
  unregisterCheckIsDirty: (path: string) => void;
  leaveEditingPage: (canLeave: boolean) => void;
}

export const ControlledNavigateContext = createContext<ControlledNavigateContextValue>({
  navigateTo: () => {},
  checkIsDirty: false,
  registerCheckIsDirty: () => {},
  unregisterCheckIsDirty: () => {},
  leaveEditingPage: () => {},
});
