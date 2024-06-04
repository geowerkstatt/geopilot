import { Button, Card, Collapse, Container } from "react-bootstrap";
import { Delivery } from "./Delivery";
import { useAuth } from "@/auth";
import { LoggedInTemplate } from "@/auth/LoggedInTemplate";
import { LoggedOutTemplate } from "@/auth/LoggedOutTemplate";

export const DeliveryContainer = ({ statusData, validationRunning }) => {
  const { login } = useAuth();

  return (
    <>
      <LoggedInTemplate>
        <Delivery statusData={statusData} validationRunning={validationRunning} />
      </LoggedInTemplate>
      <LoggedOutTemplate>
        <Collapse in={statusData?.status === "completed" && !validationRunning}>
          <Container>
            <Card>
              <Button onClick={login}>Zur Lieferung einloggen</Button>
            </Card>
          </Container>
        </Collapse>
      </LoggedOutTemplate>
    </>
  );
};
