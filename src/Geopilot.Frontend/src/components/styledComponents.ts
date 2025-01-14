import { Stack } from "@mui/material";
import { styled } from "@mui/system";

export const FlexBox = styled(Stack)(({ theme }) => ({
  gap: theme.spacing(2),
}));

export const FlexRowBox = styled(FlexBox)({
  flexDirection: "row",
  alignItems: "flex-start",
  flexWrap: "wrap",
});

export const FlexRowSpaceBetweenBox = styled(FlexRowBox)({
  justifyContent: "space-between",
});

export const FlexRowCenterBox = styled(FlexRowBox)({
  justifyContent: "center",
});

export const FlexRowEndBox = styled(FlexRowBox)({
  justifyContent: "flex-end",
});

export const FlexSpaceBetweenBox = styled(FlexBox)({
  justifyContent: "space-between",
});

export const AppBox = styled(FlexBox)({
  height: "100vh",
});

export const LayoutBox = styled(FlexBox)({
  marginTop: "60px",
  flex: "1 1 100%",
  minHeight: "calc(100vh - 60px)",
});

export const PageContentBox = styled(FlexBox)({
  padding: "20px",
  flex: "1 1 100%",
  alignItems: "center",
});

export const CenteredBox = styled(FlexBox)({
  margin: "40px 0",
  width: "100%",
  height: "100%",
  maxWidth: "1000px",
});

export const GeopilotBox = styled(FlexBox)(({ theme }) => ({
  backgroundColor: theme.palette.primary.hover,
  border: `1px solid ${theme.palette.primary.main}`,
  borderRadius: "4px",
  padding: "16px",
}));
