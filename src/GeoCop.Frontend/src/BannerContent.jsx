import { Alert } from "react-bootstrap";
import ReactMarkdown from "react-markdown";
import { IoClose } from "react-icons/io5";
import rehypeExternalLinks from "rehype-external-links";

export const BannerContent = (props) => {
  const { content } = props;

  return (
    <Alert className="banner" variant="primary">
      <ReactMarkdown rehypePlugins={[() => rehypeExternalLinks({ target: "_blank" })]}>{content || ""}</ReactMarkdown>
      <span className="close-icon">
        <IoClose onClick={props.onHide} />
      </span>
    </Alert>
  );
};

export default BannerContent;
