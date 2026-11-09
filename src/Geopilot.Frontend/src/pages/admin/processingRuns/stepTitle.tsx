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
 * The name of a step with its id beside it, so that the inputs and conditions referring to the step by its id can be
 * read. A step without a name shows its id once.
 */
export const StepTitle: FC<StepTitleProps> = ({ id, displayName }) => {
  const { localized } = useLocalized();
  const name = localized(displayName, id);

  return (
    <>
      {name}
      {name !== id && (
        <Typography component="span" variant="body2" sx={{ color: "text.secondary", marginLeft: 1 }}>
          <TechnicalText>{id}</TechnicalText>
        </Typography>
      )}
    </>
  );
};
