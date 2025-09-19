import { useGeopilotAuth } from "../../auth/index.ts";
import { DeliveryValidationNotLoggedIn } from "./deliveryValidationNotLoggedIn.tsx";
import { DeliveryValidationLoggedIn } from "./deliveryValidationLoggedIn.tsx";

export const DeliveryValidation = () => {
  const { user } = useGeopilotAuth();
  return user ? <DeliveryValidationLoggedIn /> : <DeliveryValidationNotLoggedIn />;
};
