import { FC, useCallback, useState } from "react";
import { PromptContext } from "./promptContext";
import { PromptAction, PromptOptions, PromptProviderProps } from "./promptInterfaces";

export const PromptProvider: FC<PromptProviderProps> = ({ children }) => {
  const [prompt, setPrompt] = useState<PromptOptions | null>(null);

  const showPrompt = useCallback((message: string, actions: PromptAction[]) => {
    setPrompt({
      message: message,
      actions: actions,
    });
  }, []);

  const closePrompt = useCallback(() => {
    setPrompt(null);
  }, []);

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
