import { useTranslation } from "react-i18next";

export const Organisations = () => {
  const { t } = useTranslation();

  return <>{t("organisations")}</>;
};

export default Organisations;
