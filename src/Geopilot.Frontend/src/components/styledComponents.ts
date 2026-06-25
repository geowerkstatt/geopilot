import { Stack } from "@mui/material";
import { styled } from "@mui/system";

export const FlexBox = styled(Stack)(({ theme }) => ({
  gap: theme.spacing(2),
}));

export const FlexRowBox = styled(FlexBox)({
  flexDirection: "row",
  alignItems: "center",
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

export const AppBox = styled(FlexBox)({
  height: "100vh",
});

export const LayoutBox = styled(FlexBox)(({ theme }) => ({
  backgroundColor: theme.palette.primary.background,
  paddingTop: "60px",
  flex: "1",
}));

export const pageContentPadding = "40px";

export const PageContentBox = styled(FlexBox)({
  height: "100%",
  padding: pageContentPadding,
  flex: "1",
  alignItems: "center",
});

export const CenteredBox = styled(FlexBox)({
  width: "100%",
  maxWidth: "1200px",
});

export const GeopilotBox = styled(FlexBox)(({ theme }) => ({
  backgroundColor: "white",
  border: `1px solid ${theme.palette.primary.light}`,
  borderRadius: "4px",
  padding: "16px",
}));
