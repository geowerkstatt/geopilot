import { Alert, Button, Container, Card, Col, Collapse, Form, Row, Spinner } from "react-bootstrap";
import { useState, useEffect } from "react";

const DeliveryState = Object.freeze({
  Unavailable: "unavailable",
  Available: "available",
  Running: "running",
  Completed: "completed",
  Failed: "failed",
});

export const Delivery = ({ statusData, validationRunning }) => {
  const [deliveryState, setDeliveryState] = useState(DeliveryState.Unavailable);
  const [mandates, setMandates] = useState(undefined);
  const [previousDeliveries, setPreviousDeliveries] = useState(undefined);

  const [selectedMandateId, setSelectedMandateId] = useState(undefined);
  const [partialDelivery, setPartialDelivery] = useState(false);
  const [selectedDeliveryId, setSelectedDeliveryId] = useState(undefined);
  const [comment, setComment] = useState("");

  useEffect(() => {
    setDeliveryState(DeliveryState.Unavailable);
    setMandates(undefined);
    setPreviousDeliveries(undefined);
    setSelectedMandateId(undefined);
    setPartialDelivery(false);
    setSelectedDeliveryId(undefined);
    setComment("");
  }, [statusData?.jobId]);

  useEffect(() => {
    if (statusData?.status === DeliveryState.Completed && !validationRunning) {
      fetch("/api/v1/mandate?" + new URLSearchParams({ jobId: statusData?.jobId }))
        .then(res => res.ok && res.json())
        .then(json => {
          setMandates(json);
          setSelectedMandateId(json[0]?.id);
          setDeliveryState(json?.length > 0 ? DeliveryState.Available : DeliveryState.Unavailable);
        });
    }
  }, [statusData?.jobId, statusData?.status, validationRunning]);

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
          DeliveryMandateId: selectedMandateId,
          PartialDelivery: partialDelivery,
          PrecursorDeliveryId: selectedDeliveryId,
          Comment: comment,
        }),
      });
      if (response.ok) {
        setDeliveryState(DeliveryState.Completed);
      } else {
        throw Error(response.statusText);
      }
    } catch (error) {
      setDeliveryState(DeliveryState.Failed);
    }
  };

  useEffect(() => {
    if (selectedMandateId == undefined) {
      return;
    }

    fetch("/api/v1/delivery?" + new URLSearchParams({ mandateId: selectedMandateId }))
      .then(res => res.ok && res.json())
      .then(setPreviousDeliveries);
  }, [selectedMandateId]);

  return (
    <Collapse in={deliveryState !== DeliveryState.Unavailable}>
      <Container>
        <Card>
          <Card.Body>
            <Form>
              <fieldset disabled={deliveryState !== DeliveryState.Available}>
                <Form.Group as={Row} controlId="DeliveryMandateId">
                  <Form.Label column lg={2}>
                    Operat
                  </Form.Label>
                  <Col>
                    <Form.Select
                      value={selectedMandateId}
                      onChange={e => {
                        setSelectedMandateId(e.target.value);
                      }}>
                      {mandates?.map(mandate => (
                        <option key={mandate.id} value={mandate.id}>
                          {mandate.name}
                        </option>
                      ))}
                    </Form.Select>
                  </Col>
                </Form.Group>
                <Form.Group as={Row} controlId="Partial">
                  <Form.Label column lg={2}>
                    Typ
                  </Form.Label>
                  <Col>
                    <Form.Check
                      type="switch"
                      label="ist Teillieferung"
                      checked={partialDelivery}
                      onChange={e => {
                        setPartialDelivery(e.target.checked);
                      }}
                    />
                  </Col>
                </Form.Group>
                <Form.Group as={Row} controlId="PrecursorDelivery">
                  <Form.Label column lg={2}>
                    Vorgänger
                  </Form.Label>
                  <Col>
                    <Form.Select
                      value={selectedDeliveryId}
                      onChange={e => setSelectedDeliveryId(e.target.value > 0 ? e.target.value : undefined)}>
                      <option value="-1"></option>
                      {previousDeliveries?.map(delivery => (
                        <option key={delivery.id} value={delivery.id}>
                          {delivery.date}
                        </option>
                      ))}
                    </Form.Select>
                  </Col>
                </Form.Group>
                <Form.Group as={Row} controlId="Comment">
                  <Form.Label column lg={2}>
                    Kommentar
                  </Form.Label>
                  <Col>
                    <Form.Control as="textarea" rows={3} value={comment} onChange={e => setComment(e.target.value)} />
                  </Col>
                </Form.Group>
              </fieldset>
            </Form>
          </Card.Body>
          <Card.Footer>
            {deliveryState === DeliveryState.Available && (
              <Button variant="primary" onClick={executeDelivery}>
                Abgabe erstellen
              </Button>
            )}
            {deliveryState === DeliveryState.Running && (
              <Spinner as="span" animation="border" size="sm" aria-hidden="true" />
            )}
            {deliveryState === DeliveryState.Completed && (
              <Button variant="success" disabled>
                Abgabe erfolgreich
              </Button>
            )}
            {deliveryState === DeliveryState.Failed && (
              <Alert variant={"danger"}>
                Bei der Abgabe ist ein unbekannter Fehler aufgetreten, bitte versuchen Sie es mit einer neuen
                Validierung.
              </Alert>
            )}
          </Card.Footer>
        </Card>
      </Container>
    </Collapse>
  );
};
