import { useUser } from ".";
import { FC, ReactNode } from "react";

interface LoggedOutTemplateProps {
  children: ReactNode;
}

export const LoggedOutTemplate: FC<LoggedOutTemplateProps> = ({ children }) => {
  const user = useUser();

  return user ? null : children;
};
