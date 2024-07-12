import { useUser } from ".";
import { FC, ReactNode } from "react";

interface LoggedInTemplateProps {
  children: ReactNode;
}

export const LoggedInTemplate: FC<LoggedInTemplateProps> = ({ children }) => {
  const user = useUser();

  return user ? children : null;
};
