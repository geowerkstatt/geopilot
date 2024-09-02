import { ReactNode } from "react";

export interface PromptContextInterface {
  promptIsOpen: boolean;
  message?: string;
  actions?: PromptAction[];
  showPrompt: (message: string, actions: PromptAction[]) => void;
  closePrompt: () => void;
}

export interface PromptOptions {
  message: string;
  actions: PromptAction[];
}

export interface PromptAction {
  label: string;
  action?: () => void | Promise<void>;
  color?: PromptActionColor;
  variant?: PromptActionVariant;
  disabled?: boolean;
}

export type PromptActionColor = "inherit" | "primary" | "secondary" | "error" | "info" | "success" | "warning";

export type PromptActionVariant = "text" | "outlined" | "contained";

export interface PromptProviderProps {
  children: ReactNode;
}
