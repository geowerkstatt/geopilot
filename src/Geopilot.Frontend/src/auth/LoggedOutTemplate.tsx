import { useGeopilotAuth } from ".";
import { FC, ReactNode } from "react";

interface LoggedOutTemplateProps {
  children: ReactNode;
}

export const LoggedOutTemplate: FC<LoggedOutTemplateProps> = ({ children }) => {
  const { enabled, user } = useGeopilotAuth();

  return !enabled || user ? null : children;
};
