import { Box, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import { useEffect, useState } from "react";
import { useApi } from "../../api";
import { MarkdownContent } from "./markdownContent.tsx";
import { ContentType } from "../../api/apiInterfaces.ts";

export const PrivacyPolicy = () => {
  const { t } = useTranslation();
  const [content, setContent] = useState<string>();
  const { fetchApi } = useApi();

  useEffect(() => {
    fetchApi<string>("privacy-policy.md", { responseType: ContentType.Markdown }).then(setContent);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <Box sx={{ maxWidth: "1000px" }}>
      {content ? (
        <MarkdownContent content={content} />
      ) : (
        <>
          <Typography variant="h1">{t("privacyPolicy")}</Typography>
          <p>{t("contentNotFound")}</p>
        </>
      )}
    </Box>
  );
};
