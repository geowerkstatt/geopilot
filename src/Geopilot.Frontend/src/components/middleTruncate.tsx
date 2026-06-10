import { FC } from "react";

interface MiddleTruncateProps {
  text: string;
  endLength: number;
}

export const MiddleTruncate: FC<MiddleTruncateProps> = ({ text, endLength }) => {
  return (
    <span style={{ display: "flex", flexDirection: "row", width: "100%" }}>
      {text.length <= endLength ? (
        text
      ) : (
        <>
          <span
            style={{
              overflow: "hidden",
              textOverflow: "ellipsis",
              whiteSpace: "nowrap",
            }}>
            {text.substring(0, text.length - endLength)}
          </span>
          <span
            style={{
              whiteSpace: "nowrap",
            }}>
            {text.substring(text.length - endLength)}
          </span>
        </>
      )}
    </span>
  );
};
