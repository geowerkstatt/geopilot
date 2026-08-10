import { createContext } from "react";

export interface ScrollMarginContextInterface {
  scrollMarginTop: string;
  scrollMarginBottom: string;
}

export const ScrollMarginContext = createContext<ScrollMarginContextInterface>({
  scrollMarginTop: "0px",
  scrollMarginBottom: "0px",
});
