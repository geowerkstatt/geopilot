import { useAuth } from ".";
import { FC, ReactNode } from "react";

interface AdminTemplateProps {
  children: ReactNode;
}

export const AdminTemplate: FC<AdminTemplateProps> = ({ children }) => {
  const { user } = useAuth();

  return user?.isAdmin ? children : null;
};
