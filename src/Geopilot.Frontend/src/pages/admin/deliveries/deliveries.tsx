import { DeliveryGrid } from "../../../components/grids/deliveryGrid.tsx";

export const Deliveries = () => {
  return (
    <DeliveryGrid
      fetchUrl="/api/v1/delivery"
      columns={["id", "date", "declarerName", "mandateName", "forwardingState", "notificationState"]}
      rowAction="details"
    />
  );
};
