import { useUser } from ".";
import { FC, ReactNode } from "react";

interface AdminTemplateProps {
  children: ReactNode;
}

export const AdminTemplate: FC<AdminTemplateProps> = ({ children }) => {
  const user = useUser();

  return user?.isAdmin ? children : null;
};
