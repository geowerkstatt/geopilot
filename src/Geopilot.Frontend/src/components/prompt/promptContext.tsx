import { createContext, FC, useState } from "react";
import { PromptAction, PromptContextInterface, PromptOptions, PromptProviderProps } from "./promptInterfaces";

export const PromptContext = createContext<PromptContextInterface>({
  title: undefined,
  message: undefined,
  actions: [],
  promptIsOpen: false,
  showPrompt: () => {},
  closePrompt: () => {},
});

export const PromptProvider: FC<PromptProviderProps> = ({ children }) => {
  const [prompt, setPrompt] = useState<PromptOptions | null>(null);

  const showPrompt = (title: string, message: string, actions: PromptAction[]) => {
    setPrompt({
      title: title,
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
        promptIsOpen: prompt?.title != null,
        title: prompt?.title,
        message: prompt?.message,
        actions: prompt?.actions,
        showPrompt,
        closePrompt,
      }}>
      {children}
    </PromptContext.Provider>
  );
};
