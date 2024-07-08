import { useAuth } from ".";
import { FC, ReactNode } from "react";

interface LoggedInTemplateProps {
  children: ReactNode;
}

export const LoggedInTemplate: FC<LoggedInTemplateProps> = ({ children }) => {
  const { user } = useAuth();

  return user ? children : null;
};
