import { Alert } from "react-bootstrap";
import ReactMarkdown from "react-markdown";
import { IoClose } from "react-icons/io5";
import rehypeExternalLinks from "rehype-external-links";

export const BannerContent = ({ content, onHide }) => (
  <Alert className="banner" variant="primary">
    <ReactMarkdown rehypePlugins={[() => rehypeExternalLinks({ target: "_blank" })]}>{content || ""}</ReactMarkdown>
    <span className="close-icon">
      <IoClose onClick={onHide} />
    </span>
  </Alert>
);

export default BannerContent;
