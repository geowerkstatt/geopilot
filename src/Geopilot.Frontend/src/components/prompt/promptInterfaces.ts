import { ReactNode } from "react";
import { TranslationKey } from "../../appInterfaces";

export interface PromptContextInterface {
  promptIsOpen: boolean;
  message?: TranslationKey;
  actions?: PromptAction[];
  showPrompt: (message: TranslationKey, actions: PromptAction[]) => void;
  closePrompt: () => void;
}

export interface PromptOptions {
  message: TranslationKey;
  actions: PromptAction[];
}

export interface PromptAction {
  label: TranslationKey;
  icon?: ReactNode;
  action?: () => void | Promise<void>;
  color?: PromptActionColor;
  variant?: PromptActionVariant;
  disabled?: boolean;
}

type PromptActionColor = "inherit" | "primary" | "secondary" | "error" | "info" | "success" | "warning";

type PromptActionVariant = "text" | "outlined" | "contained";

export interface PromptProviderProps {
  children: ReactNode;
}
