import { useTranslation } from "react-i18next";

export const Users = () => {
  const { t } = useTranslation();

  return <>{t("users")}</>;
};

export default Users;
