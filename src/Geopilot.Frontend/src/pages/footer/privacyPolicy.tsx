import { Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { useEffect, useState } from "react";
import { useApi } from "../../api";
import { MarkdownContent } from "./markdownContent.tsx";
import { ContentType } from "../../api/apiInterfaces.ts";
import { CenteredBox } from "../../components/styledComponents.ts";

export const PrivacyPolicy = () => {
  const { t } = useTranslation();
  const [content, setContent] = useState<string>();
  const { fetchApi } = useApi();

  useEffect(() => {
    fetchApi<string>("privacy-policy.md", { responseType: ContentType.Markdown }).then(setContent);
  }, [fetchApi]);

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
