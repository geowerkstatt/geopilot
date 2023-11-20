import { Card } from "react-bootstrap";
import { GoFile } from "react-icons/go";

function getExtension(filename) {
  const index = filename.lastIndexOf(".");
  return index === -1 ? "" : filename.substring(index);
}

export const ValidatorResult = ({ jobId, protokollFileName, validatorName, result }) => {
  const statusClass = result && result.status === "completed" ? "valid" : "errors";
  const statusText = result && result.status === "completed" ? "Keine Fehler!" : "Fehler!";

  return (
    <>
      <hr />
      <h4>{validatorName}</h4>
      <p>{result.statusMessage}</p>
      <Card.Title className={statusClass}>
        {statusText}
        <span>
          {result.logFiles &&
            Object.entries(result.logFiles).map(([logFileType, logFile]) => (
              <span key={logFileType} className="icon-tooltip">
                <a
                  download={protokollFileName + getExtension(logFile)}
                  className={statusClass + " download-icon"}
                  href={`/api/v1/download/${jobId}/${logFile}`}
                >
                  <GoFile />
                  <span className="download-description">{logFileType}</span>
                </a>
                <span className="icon-tooltip-text">{logFileType}-Datei herunterladen</span>
              </span>
            ))}
        </span>
      </Card.Title>
    </>
  );
};
