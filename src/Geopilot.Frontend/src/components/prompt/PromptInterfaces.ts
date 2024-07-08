import { ReactNode } from "react";

export interface PromptContextInterface {
  promptIsOpen: boolean;
  title: string | undefined;
  message: string | undefined;
  actions: PromptAction[] | undefined;
  showPrompt: (title: string, message: string, actions: PromptAction[]) => void;
  closePrompt: () => void;
}

export interface PromptOptions {
  title: string;
  message: string;
  actions: PromptAction[];
}

export interface PromptAction {
  label: string;
  action?: () => never;
  color?: PromptActionColor;
  variant?: PromptActionVariant;
  disabled?: boolean;
}

export type PromptActionColor = "inherit" | "primary" | "secondary" | "error" | "info" | "success" | "warning";

export type PromptActionVariant = "text" | "outlined" | "contained";

export interface PromptProviderProps {
  children: ReactNode;
}
