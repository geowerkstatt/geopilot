import ReactMarkdown from "react-markdown";
import { Modal, Button } from "react-bootstrap";
import rehypeExternalLinks from "rehype-external-links";
import { useTranslation } from "react-i18next";

export const ModalContent = props => {
  const { content, type, onHide } = props;
  const { t } = useTranslation();

  return (
    <Modal {...props} size="lg" aria-labelledby="contained-modal-title-vcenter" centered>
      <Modal.Body>
        {type === "markdown" && (
          <ReactMarkdown rehypePlugins={[() => rehypeExternalLinks({ target: "_blank" })]}>
            {content || ""}
          </ReactMarkdown>
        )}
        {type === "raw" && content}
      </Modal.Body>
      <Modal.Footer>
        <Button variant="outline-dark" onClick={onHide}>
          {t("close")}
        </Button>
      </Modal.Footer>
    </Modal>
  );
};

export default ModalContent;
