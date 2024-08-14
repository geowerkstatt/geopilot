export interface FetchParams {
  url: string;
  method?: FetchMethod;
  contentType?: FetchContentType;
  body?: BodyInit | null;
  onSuccess: (response: unknown) => void;
  onError?: (error: string, status?: number) => void;
}

export enum FetchMethod {
  GET = "GET",
  POST = "POST",
  PUT = "PUT",
  DELETE = "DELETE",
}

export enum FetchContentType {
  JSON = "application/json",
  TEXT = "text/plain",
}

export const runFetch = async ({
  url,
  method = FetchMethod.GET,
  contentType = FetchContentType.JSON,
  body = null,
  onSuccess,
  onError,
}: FetchParams): Promise<void> => {
  try {
    const options: RequestInit = {
      method,
      headers: {
        "Content-Type": contentType,
      },
      body,
    };

    const response = await fetch(url, options);

    if (response.ok) {
      const results = contentType === FetchContentType.JSON ? await response.json() : await response.text();
      onSuccess(results);
    } else {
      const errorResponse = await response.json();
      onError?.(errorResponse.detail, response.status);
    }
  } catch (error) {
    onError?.(String(error));
  }
};
