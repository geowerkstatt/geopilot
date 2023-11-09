import "./app.css";
import { useState, useRef, useEffect } from "react";
import DayJS from "dayjs";
import { Card, Container } from "react-bootstrap";
import { GoFile } from "react-icons/go";

function getExtension(filename) {
  const index = filename.lastIndexOf(".");
  return index === -1 ? "" : filename.substring(index + 1);
}

const ValidatorResult = ({ jobId, protokollFileName, validatorName, result }) => {
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
                  download={protokollFileName + "." + getExtension(logFile)}
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

export const Protokoll = ({ log, statusData, fileName, validationRunning }) => {
  const [indicateWaiting, setIndicateWaiting] = useState(false);
  const protokollTimestamp = DayJS(new Date()).format("YYYYMMDDHHmm");
  const protokollFileName = "Ilivalidator_output_" + fileName + "_" + protokollTimestamp;
  const logEndRef = useRef(null);

  // Autoscroll protokoll log
  useEffect(() => {
    logEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [log]);

  // Show flash dot to indicate waiting
  useEffect(() => {
    setTimeout(() => {
      if (validationRunning === true) {
        setIndicateWaiting(!indicateWaiting);
      } else {
        setIndicateWaiting(false);
      }
    }, 500);
  });

  return (
    <Container>
      {log.length > 0 && (
        <Card className="protokoll-card">
          <Card.Body>
            <div className="protokoll">
              {log.map((logEntry, index) => (
                <div key={index}>
                  {logEntry}
                  {indicateWaiting && index === log.length - 1 && "."}
                </div>
              ))}
              <div ref={logEndRef} />
            </div>
            {statusData &&
              Object.entries(statusData.validatorResults).map(([validatorName, result]) => (
                <ValidatorResult
                  key={validatorName}
                  jobId={statusData.jobId}
                  protokollFileName={protokollFileName}
                  validatorName={validatorName}
                  result={result}
                />
              ))}
          </Card.Body>
        </Card>
      )}
    </Container>
  );
};
