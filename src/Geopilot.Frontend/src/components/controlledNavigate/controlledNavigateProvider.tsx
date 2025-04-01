import { createContext, FC, PropsWithChildren, useCallback, useRef, useState } from "react";
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
  const pathRef = useRef<string | undefined>(undefined);
  const [registeredEditPages, setRegisteredEditPages] = useState<string[]>([]);
  const [checkIsDirty, setCheckIsDirty] = useState<boolean>(false);
  const navigate = useNavigate();

  const registerCheckIsDirty = useCallback((path: string) => {
    setRegisteredEditPages(prevPages => {
      if (!prevPages.includes(path)) {
        return [...prevPages, path];
      }
      return prevPages;
    });
  }, []);

  const unregisterCheckIsDirty = useCallback((path: string) => {
    setRegisteredEditPages(prevPages => {
      return prevPages.filter(value => value !== path);
    });
  }, []);

  const navigateTo = useCallback(
    (path: string) => {
      if (
        registeredEditPages.find(value => {
          return window.location.pathname.includes(value);
        })
      ) {
        pathRef.current = path;
        setCheckIsDirty(true);
      } else {
        navigate(path);
      }
    },
    [navigate, registeredEditPages],
  );

  const leaveEditingPage = useCallback(
    (canLeave: boolean) => {
      if (canLeave && pathRef.current) {
        navigate(pathRef.current);
      }
      setCheckIsDirty(false);
      pathRef.current = undefined;
    },
    [navigate],
  );

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
