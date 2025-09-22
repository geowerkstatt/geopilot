import { useGeopilotAuth } from "../../../auth/index.ts";
import { DeliveryValidationLoggedIn } from "./deliveryValidationLoggedIn.tsx";
import { DeliveryValidationNotLoggedIn } from "./deliveryValidationNotLoggedIn.tsx";

export const DeliveryValidation = () => {
  const { user } = useGeopilotAuth();
  return user ? <DeliveryValidationLoggedIn /> : <DeliveryValidationNotLoggedIn />;
};
