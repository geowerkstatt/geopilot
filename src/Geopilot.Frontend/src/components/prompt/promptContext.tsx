import { createContext } from "react";
import { PromptContextInterface } from "./promptInterfaces";

export const PromptContext = createContext<PromptContextInterface>({
  message: undefined,
  actions: [],
  promptIsOpen: false,
  showPrompt: () => {},
  closePrompt: () => {},
});
