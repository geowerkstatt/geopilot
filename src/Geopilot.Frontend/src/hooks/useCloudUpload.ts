import { useCallback } from "react";
import { ApiError, CloudUploadResponse } from "./apiInterfaces";
import useFetch from "../hooks/useFetch";

const useCloudUpload = () => {
  const { fetchApi } = useFetch();

  const cloudUpload = useCallback(
    async (files: File[], signal?: AbortSignal): Promise<string> => {
      const session = await fetchApi<CloudUploadResponse>("/api/v2/upload", {
        method: "POST",
        body: JSON.stringify({
          files: files.map(f => ({ fileName: f.name, size: f.size })),
        }),
        headers: { "Content-Type": "application/json" },
        signal,
      });

      await Promise.all(
        session.files.map((info, i) =>
          fetch(info.uploadUrl, {
            method: "PUT",
            body: files[i],
            headers: {
              "Content-Type": "application/octet-stream",
              "x-ms-blob-type": "BlockBlob",
            },
            signal,
          }).then(r => {
            if (!r.ok) throw new ApiError(`Upload failed: ${r.statusText}`, r.status);
          }),
        ),
      );

      return session.jobId;
    },
    [fetchApi],
  );

  return { cloudUpload };
};

export default useCloudUpload;
