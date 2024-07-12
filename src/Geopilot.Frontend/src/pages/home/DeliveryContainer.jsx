import { Button, Card, Collapse, Container } from "react-bootstrap";
import { Delivery } from "./Delivery";
import { useGeopilotAuth } from "@/auth";
import { LoggedInTemplate } from "@/auth/LoggedInTemplate";
import { LoggedOutTemplate } from "@/auth/LoggedOutTemplate";
import { useTranslation } from "react-i18next";

export const DeliveryContainer = ({ statusData, validationRunning }) => {
  const { login } = useGeopilotAuth();
  const { t } = useTranslation();

  return (
    <>
      <LoggedInTemplate>
        <Delivery statusData={statusData} validationRunning={validationRunning} />
      </LoggedInTemplate>
      <LoggedOutTemplate>
        <Collapse in={statusData?.status === "completed" && !validationRunning}>
          <Container>
            <Card>
              <Button onClick={login}>{t("logInForDelivery")}</Button>
            </Card>
          </Container>
        </Collapse>
      </LoggedOutTemplate>
    </>
  );
};
