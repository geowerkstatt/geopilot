import { useGeopilotAuth } from "../auth";
import { FC, ReactNode } from "react";

interface LoggedInTemplateProps {
  children: ReactNode;
}

export const LoggedInTemplate: FC<LoggedInTemplateProps> = ({ children }) => {
  const { enabled, user } = useGeopilotAuth();

  return enabled && user ? children : null;
};
