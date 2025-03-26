import { Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { useEffect, useState } from "react";
import { useApi } from "../../api";
import { MarkdownContent } from "../../components/markdownContent.tsx";
import { CenteredBox } from "../../components/styledComponents.ts";

export const PrivacyPolicy = () => {
  const { t, i18n } = useTranslation();
  const [content, setContent] = useState<string | null>();
  const { fetchLocalizedMarkdown } = useApi();

  useEffect(() => {
    fetchLocalizedMarkdown("privacy-policy", i18n.language).then(setContent);
  }, [fetchLocalizedMarkdown, i18n.language]);

  return (
    <CenteredBox>
      {content ? (
        <MarkdownContent content={content} />
      ) : (
        <>
          <Typography variant="h1">{t("privacyPolicy")}</Typography>
          <p>{t("contentNotFound")}</p>
        </>
      )}
    </CenteredBox>
  );
};
