import { FC, useCallback, useState } from "react";
import { TranslationKey } from "../../appInterfaces";
import { PromptContext } from "./promptContext";
import { PromptAction, PromptOptions, PromptProviderProps } from "./promptInterfaces";

export const PromptProvider: FC<PromptProviderProps> = ({ children }) => {
  const [prompt, setPrompt] = useState<PromptOptions | null>(null);

  const showPrompt = useCallback(
    (message: TranslationKey, actions: PromptAction[], messageValues?: Record<string, string>) => {
      setPrompt({
        message: message,
        messageValues: messageValues,
        actions: actions,
      });
    },
    [],
  );

  const closePrompt = useCallback(() => {
    setPrompt(null);
  }, []);

  return (
    <PromptContext.Provider
      value={{
        promptIsOpen: prompt?.message != null,
        message: prompt?.message,
        messageValues: prompt?.messageValues,
        actions: prompt?.actions,
        showPrompt,
        closePrompt,
      }}>
      {children}
    </PromptContext.Provider>
  );
};
