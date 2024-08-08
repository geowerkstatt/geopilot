export const runFetch = async (url: string, contentType?: string) => {
  const response = await fetch(url, {
    method: "GET",
  });

  if (response.ok) {
    const responseContentType = response.headers.get("content-type");
    if (responseContentType?.indexOf("application/json") !== -1) {
      return await response.json();
    } else if (!contentType || responseContentType?.includes(contentType)) {
      return await response.text();
    }
  }
  return "";
};
