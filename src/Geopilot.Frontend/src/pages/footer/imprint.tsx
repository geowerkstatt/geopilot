import { Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { useEffect, useState } from "react";
import { useApi } from "../../api";
import { MarkdownContent } from "./markdownContent.tsx";
import { ContentType } from "../../api/apiInterfaces.ts";
import { CenteredBox } from "../../components/styledComponents.ts";

export const Imprint = () => {
  const { t } = useTranslation();
  const [content, setContent] = useState<string>();
  const { fetchApi } = useApi();

  useEffect(() => {
    fetchApi<string>("imprint.md", { responseType: ContentType.Markdown }).then(setContent);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <CenteredBox>
      {content ? (
        <MarkdownContent content={content} />
      ) : (
        <>
          <Typography variant="h1">{t("imprint")}</Typography>
          <p>{t("contentNotFound")}</p>
        </>
      )}
    </CenteredBox>
  );
};
