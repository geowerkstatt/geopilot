import "../../app.css";
import { ValidatorResult } from "./ValidatorResult";
import { useState, useRef, useEffect } from "react";
import DayJS from "dayjs";
import { Card, Collapse, Container } from "react-bootstrap";

export const Protokoll = ({ log, statusData, fileName, validationRunning }) => {
  const [indicateWaiting, setIndicateWaiting] = useState(false);
  const protokollTimestamp = DayJS(new Date()).format("YYYYMMDDHHmm");
  const protokollFileName =
    "Ilivalidator_output_" + fileName + "_" + protokollTimestamp;
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
    <Collapse in={log.length > 0}>
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
                Object.entries(statusData.validatorResults).map(
                  ([validatorName, result]) => (
                    <ValidatorResult
                      key={validatorName}
                      jobId={statusData.jobId}
                      protokollFileName={protokollFileName}
                      validatorName={validatorName}
                      result={result}
                    />
                  ),
                )}
            </Card.Body>
          </Card>
        )}
      </Container>
    </Collapse>
  );
};
