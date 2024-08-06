import { useGeopilotAuth } from ".";
import { FC, ReactNode } from "react";

interface AdminTemplateProps {
  children: ReactNode;
}

export const AdminTemplate: FC<AdminTemplateProps> = ({ children }) => {
  const { enabled, user } = useGeopilotAuth();

  return enabled && user?.isAdmin ? children : null;
};
