import { createContext, FC, PropsWithChildren, useState } from "react";
import { useNavigate } from "react-router-dom";

export const ControlledNavigateContext = createContext({
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  navigateTo: (path: string) => {},
  checkIsDirty: false,
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  registerCheckIsDirty: (path: string) => {},
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  unregisterCheckIsDirty: (path: string) => {},
  // eslint-disable-next-line @typescript-eslint/no-unused-vars
  leaveEditingPage: (canLeave: boolean) => {},
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
