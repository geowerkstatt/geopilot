// eslint-disable-next-line @typescript-eslint/ban-ts-comment
// @ts-nocheck

import ReactMarkdown from "react-markdown";
import rehypeExternalLinks from "rehype-external-links";
import { FC } from "react";
import { Typography } from "@mui/material";

interface MarkdownContentProps {
  content: string;
  routeHash?: string;
}

export const MarkdownContent: FC<MarkdownContentProps> = ({ content, routeHash }) => {
  return (
    <ReactMarkdown
      rehypePlugins={[() => rehypeExternalLinks({ target: "_blank" })]}
      components={{
        h1: props => <Typography component="h1" variant="h1" id={routeHash} {...props} />,
        h2: props => <Typography component="h2" variant="h3" {...props} />,
        h3: props => <Typography component="h3" variant="h3" {...props} />,
        h4: props => <Typography component="h4" variant="h4" {...props} />,
        h5: props => <Typography component="h5" variant="h6" {...props} />,
        h6: props => <Typography component="h6" variant="h5" {...props} />,
        p: props => <Typography component="body1" variant="body1" {...props} />,
      }}>
      {content}
    </ReactMarkdown>
  );
};
