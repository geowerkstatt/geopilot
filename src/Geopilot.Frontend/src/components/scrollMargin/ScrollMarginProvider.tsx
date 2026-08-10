import { FC, PropsWithChildren } from "react";
import { ScrollMarginContext, ScrollMarginContextInterface } from "./ScrollMarginContext";

export const ScrollMarginProvider: FC<PropsWithChildren<ScrollMarginContextInterface>> = ({ children, ...props }) => {
  return <ScrollMarginContext.Provider value={props}>{children}</ScrollMarginContext.Provider>;
};
