import { createContext, FC, PropsWithChildren, useState } from "react";
import { useNavigate } from "react-router-dom";
interface ControlledNavigateContextValue {
  navigateTo: (path: string) => void;
  checkIsDirty: boolean;
  registerCheckIsDirty: (path: string) => void;
  unregisterCheckIsDirty: (path: string) => void;
  leaveEditingPage: (canLeave: boolean) => void;
}

export const ControlledNavigateContext = createContext<ControlledNavigateContextValue>({
  navigateTo: () => {},
  checkIsDirty: false,
  registerCheckIsDirty: () => {},
  unregisterCheckIsDirty: () => {},
  leaveEditingPage: () => {},
});

export const ControlledNavigateProvider: FC<PropsWithChildren> = ({ children }) => {
  const [path, setPath] = useState<string>();
  const [registeredEditPages, setRegisteredEditPages] = useState<string[]>([]);
  const [checkIsDirty, setCheckIsDirty] = useState<boolean>(false);
  const navigate = useNavigate();

  const registerCheckIsDirty = (path: string) => {
    if (!registeredEditPages.includes(path)) {
      setRegisteredEditPages([...registeredEditPages, path]);
    }
  };

  const unregisterCheckIsDirty = (path: string) => {
    setRegisteredEditPages(registeredEditPages.filter(value => value !== path));
  };

  const navigateTo = (path: string) => {
    if (
      registeredEditPages.find(value => {
        return window.location.pathname.includes(value);
      })
    ) {
      setPath(path);
      setCheckIsDirty(true);
    } else {
      navigate(path);
    }
  };

  const leaveEditingPage = (canLeave: boolean) => {
    if (canLeave && path) {
      navigate(path);
    }
    setCheckIsDirty(false);
    setPath(undefined);
  };

  return (
    <ControlledNavigateContext.Provider
      value={{
        navigateTo,
        registerCheckIsDirty,
        unregisterCheckIsDirty,
        checkIsDirty,
        leaveEditingPage,
      }}>
      {children}
    </ControlledNavigateContext.Provider>
  );
};
