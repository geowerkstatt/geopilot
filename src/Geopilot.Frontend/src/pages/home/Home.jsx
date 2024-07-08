import "../../app.css";
import { useCallback, useEffect, useRef, useState } from "react";
import { Container, Stack } from "react-bootstrap";
import { useTranslation } from "react-i18next";
import { FileDropzone } from "./FileDropzone";
import { Title } from "./Title";
import { Protokoll } from "./Protokoll";
import { DeliveryContainer } from "./DeliveryContainer";
import Header from "../../Header.tsx";

export const Home = ({
  clientSettings,
  nutzungsbestimmungenAvailable,
  showNutzungsbestimmungen,
  quickStartContent,
  setShowBannerContent,
}) => {
  const { t } = useTranslation();
  const [fileToCheck, setFileToCheck] = useState(null);
  const fileToCheckRef = useRef(fileToCheck);
  const [validationRunning, setValidationRunning] = useState(false);
  const [statusInterval, setStatusInterval] = useState(null);
  const [statusData, setStatusData] = useState(null);
  const [checkedNutzungsbestimmungen, setCheckedNutzungsbestimmungen] = useState(false);
  const [isFirstValidation, setIsFirstValidation] = useState(true);
  const [log, setLog] = useState([]);
  const [uploadLogsInterval, setUploadLogsInterval] = useState(0);
  const [uploadLogsEnabled, setUploadLogsEnabled] = useState(false);
  const [validationSettings, setValidationSettings] = useState({});

  useEffect(() => {
    fetch("/api/v1/validation")
      .then(res => res.headers.get("content-type")?.includes("application/json") && res.json())
      .then(settings => setValidationSettings(settings));
  }, []);

  // Enable Upload logging
  useEffect(() => {
    if (uploadLogsInterval !== 0) setUploadLogsEnabled(true);
  }, [uploadLogsInterval]);
  useEffect(() => {
    if (!uploadLogsEnabled) clearInterval(uploadLogsInterval);
  }, [uploadLogsEnabled, uploadLogsInterval]);

  const resetLog = useCallback(() => setLog([]), [setLog]);
  const updateLog = useCallback(
    (message, { disableUploadLogs = true } = {}) => {
      if (disableUploadLogs) setUploadLogsEnabled(false);
      setLog(log => {
        if (message === log[log.length - 1]) return log;
        else return [...log, message];
      });
    },
    [setUploadLogsEnabled],
  );

  // Reset log and abort upload on file change
  useEffect(() => {
    resetLog();
    setStatusData(null);
    setValidationRunning(false);
    setUploadLogsEnabled(false);
    if (statusInterval) clearInterval(statusInterval);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [fileToCheck]);

  // Show banner on first validation
  useEffect(() => {
    if (validationRunning && isFirstValidation) {
      setTimeout(() => {
        setShowBannerContent(true);
        setIsFirstValidation(false);
      }, 2000);
    }
  }, [validationRunning, isFirstValidation, setShowBannerContent, setIsFirstValidation]);

  const logUploadLogMessages = () =>
    updateLog(`${t("uploadFile", { fileName: fileToCheck.name })}`, { disableUploadLogs: false });
  const setIntervalImmediately = (func, interval) => {
    func();
    return setInterval(func, interval);
  };
  const checkFile = e => {
    e.stopPropagation();
    resetLog();
    setStatusData(null);
    setValidationRunning(true);
    setUploadLogsInterval(setIntervalImmediately(logUploadLogMessages, 2000));
    uploadFile(fileToCheck);
  };

  const uploadFile = async file => {
    const formData = new FormData();
    formData.append("file", file, file.name);
    const response = await fetch(`api/v1/validation`, {
      method: "POST",
      body: formData,
    });
    if (response.ok) {
      // Use ref instead of state to check current file status in async function
      if (fileToCheckRef.current) {
        const data = await response.json();
        const getStatusData = async data => {
          const status = await fetch(`/api/v1/validation/${data.jobId}`, {
            method: "GET",
          });
          return await status.json();
        };

        const interval = setIntervalImmediately(async () => {
          const statusData = await getStatusData(data);
          if (
            statusData.status === "completed" ||
            statusData.status === "completedWithErrors" ||
            statusData.status === "failed"
          ) {
            clearInterval(interval);
            setValidationRunning(false);
            setStatusData(statusData);
          }
        }, 2000);
        setStatusInterval(interval);
      }
    } else {
      console.log("Error while uploading file: " + response.json());
      updateLog(t("uploadNotSuccessful"));
      setValidationRunning(false);
    }
  };

  return (
    <>
      <Header clientSettings={clientSettings} />
      <main>
        <Stack gap={3}>
          <Container className="main-container">
            <Title clientSettings={clientSettings} quickStartContent={quickStartContent} />
            <FileDropzone
              setUploadLogsEnabled={setUploadLogsEnabled}
              setFileToCheck={setFileToCheck}
              fileToCheck={fileToCheck}
              nutzungsbestimmungenAvailable={nutzungsbestimmungenAvailable}
              checkedNutzungsbestimmungen={checkedNutzungsbestimmungen}
              checkFile={checkFile}
              validationRunning={validationRunning}
              setCheckedNutzungsbestimmungen={setCheckedNutzungsbestimmungen}
              showNutzungsbestimmungen={showNutzungsbestimmungen}
              acceptedFileTypes={validationSettings?.allowedFileExtensions}
              fileToCheckRef={fileToCheckRef}
            />
          </Container>
          <Protokoll
            log={log}
            statusData={statusData}
            fileName={fileToCheck ? fileToCheck.name : ""}
            validationRunning={validationRunning}
          />
          <DeliveryContainer statusData={statusData} validationRunning={validationRunning} />
        </Stack>
      </main>
    </>
  );
};

export default Home;
