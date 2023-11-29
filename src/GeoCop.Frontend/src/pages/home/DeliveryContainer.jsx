import { AuthenticatedTemplate, UnauthenticatedTemplate } from "@azure/msal-react";
import { Button, Card, Collapse, Container } from "react-bootstrap";
import { Delivery } from "./Delivery";
import { useAuth } from "../../contexts/auth";

export const DeliveryContainer = ({ statusData, validationRunning }) => {
  const { login } = useAuth();

  return (
    <>
      <AuthenticatedTemplate>
        <Delivery statusData={statusData} validationRunning={validationRunning} />
      </AuthenticatedTemplate>
      <UnauthenticatedTemplate>
        <Collapse in={statusData?.status === "completed" && !validationRunning}>
          <Container>
            <Card>
              <Button onClick={login}>Zur Abgabe einloggen</Button>
            </Card>
          </Container>
        </Collapse>
      </UnauthenticatedTemplate>
    </>
  );
};
