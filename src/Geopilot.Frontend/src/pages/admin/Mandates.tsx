import { useTranslation } from "react-i18next";

export const Mandates = () => {
  const { t } = useTranslation();

  return <>{t("mandates")}</>;
};

export default Mandates;
