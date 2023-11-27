import { Button, Container, Card, Collapse, Spinner } from "react-bootstrap";
import { useState, useEffect } from "react";

const DeliveryState = Object.freeze({
  Unavailable: "unavailable",
  Available: "available",
  Running: "running",
  Completed: "completed",
});

export const Delivery = ({ statusData, validationRunning }) => {
  const [deliveryState, setDeliveryState] = useState(DeliveryState.Unavailable);

  useEffect(() => {
    setDeliveryState(DeliveryState.Unavailable);
  }, [statusData?.jobId]);

  useEffect(() => {
    if (statusData?.status === "completed" && !validationRunning) {
      setDeliveryState(DeliveryState.Available);
    }
  }, [statusData?.status, validationRunning]);

  const executeDelivery = async () => {
    setDeliveryState(DeliveryState.Running);
    try {
      var response = await fetch("api/v1/delivery", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          JobId: statusData.jobId,
          DeliveryMandateId: 1,
        }),
      });
      if (response.status == 201) {
        setDeliveryState(DeliveryState.Completed);
      }
    } catch (error) {
      setDeliveryState(DeliveryState.Available);
    }
  };

  return (
    <Collapse in={deliveryState !== DeliveryState.Unavailable}>
      <Container>
        <Card>
          <Card.Body>
            <p>Die Validierung war erfolgreich. Sie können die Abgabe nun erstellen.</p>
          </Card.Body>
          <Card.Footer>
            {deliveryState === DeliveryState.Available && (
              <Button variant="primary" onClick={executeDelivery}>
                Abgabe erstellen
              </Button>
            )}
            {deliveryState === DeliveryState.Running && (
              <Button variant="primary">
                <Spinner as="span" animation="border" size="sm" aria-hidden="true" />
                <span>Abgabe läuft…</span>
              </Button>
            )}
            {deliveryState === DeliveryState.Completed && (
              <Button variant="success" disabled>
                Abgabe erfolgreich
              </Button>
            )}
          </Card.Footer>
        </Card>
      </Container>
    </Collapse>
  );
};
