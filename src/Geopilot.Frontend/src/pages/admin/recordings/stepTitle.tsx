import { FC } from "react";
import { Typography } from "@mui/material";
import { LocalizedText } from "../../../api/apiInterfaces.ts";
import { useLocalized } from "../../../hooks/useLocalized.ts";
import { TechnicalText } from "./technicalText.tsx";

interface StepTitleProps {
  id: string;
  displayName?: LocalizedText;
}

/**
 * The name of a step or a reaction with its id beside it, so that what refers to it by its id, such as an input, a
 * condition or the log, can be read. One without a name shows its id once.
 */
export const StepTitle: FC<StepTitleProps> = ({ id, displayName }) => {
  const { localized } = useLocalized();
  const name = localized(displayName, id);

  // The space keeps name and id apart for a screen reader and in copied text.
  return (
    <>
      {name}
      {name !== id && (
        <>
          {" "}
          <Typography component="span" variant="body2" sx={{ color: "text.secondary", marginLeft: 0.5 }}>
            <TechnicalText>{id}</TechnicalText>
          </Typography>
        </>
      )}
    </>
  );
};
