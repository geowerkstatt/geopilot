import { createContext, FC, useState } from "react";
import { PromptAction, PromptContextInterface, PromptOptions, PromptProviderProps } from "./promptInterfaces";

export const PromptContext = createContext<PromptContextInterface>({
  message: undefined,
  actions: [],
  promptIsOpen: false,
  showPrompt: () => {},
  closePrompt: () => {},
});

export const PromptProvider: FC<PromptProviderProps> = ({ children }) => {
  const [prompt, setPrompt] = useState<PromptOptions | null>(null);

  const showPrompt = (message: string, actions: PromptAction[]) => {
    setPrompt({
      message: message,
      actions: actions,
    });
  };

  const closePrompt = () => {
    setPrompt(null);
  };

  return (
    <PromptContext.Provider
      value={{
        promptIsOpen: prompt?.message != null,
        message: prompt?.message,
        actions: prompt?.actions,
        showPrompt,
        closePrompt,
      }}>
      {children}
    </PromptContext.Provider>
  );
};
