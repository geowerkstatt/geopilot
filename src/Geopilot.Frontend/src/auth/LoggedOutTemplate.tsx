import { useAuth } from ".";
import { FC, ReactNode } from "react";

interface LoggedOutTemplateProps {
  children: ReactNode;
}

export const LoggedOutTemplate: FC<LoggedOutTemplateProps> = ({ children }) => {
  const { user } = useAuth();

  return user ? null : children;
};
