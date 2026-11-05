import { TFunction } from "i18next";
import { ClientKind, PipelineRunDeclarer } from "../../../api/generated";
import { TranslationKey } from "../../../appInterfaces";

/** The time between start and end as minutes and seconds, or undefined while there is no end. */
export const formatDuration = (startedAt?: string | null, endedAt?: string | null): string | undefined => {
  if (!startedAt || !endedAt) {
    return undefined;
  }
  const milliseconds = Math.max(0, new Date(endedAt).getTime() - new Date(startedAt).getTime());
  if (milliseconds < 1000) {
    return `${milliseconds} ms`;
  }
  const seconds = Math.round(milliseconds / 1000);
  const minutes = Math.floor(seconds / 60);
  return minutes > 0 ? `${minutes} min ${seconds % 60} s` : `${seconds} s`;
};

/** The steps that kept a run from allowing a delivery, as the list has room for them: the first and how many more. */
export const formatBlockingSteps = (names: string[]): string =>
  names.length > 1 ? `${names[0]} (+${names.length - 1})` : (names[0] ?? "");

/** A file size in bytes, kilobytes or megabytes. */
export const formatFileSize = (bytes: number): string => {
  if (bytes < 1024) {
    return `${bytes} B`;
  }
  if (bytes < 1024 * 1024) {
    return `${Math.round(bytes / 1024)} KB`;
  }
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
};

const clientKindLabelKey: Record<ClientKind, TranslationKey | undefined> = {
  [ClientKind.WebClient]: "clientKindWeb",
  [ClientKind.ApiClient]: "clientKindApi",
  [ClientKind.Unknown]: undefined,
};

/** The kind of client a run was started over, such as "Web", or undefined when it is unknown. */
export const formatClientKind = (t: TFunction, clientKind: ClientKind): string | undefined => {
  const kindKey = clientKindLabelKey[clientKind];
  return kindKey && t(kindKey);
};

/** Who started a run and over which kind of client, such as "Erika Muster (Web)". */
export const formatDeclarer = (
  t: TFunction,
  declarer: PipelineRunDeclarer | null | undefined,
  clientKind: ClientKind,
): string => {
  const name = declarer?.name ?? t("anonymous");
  const kind = formatClientKind(t, clientKind);
  return kind ? `${name} (${kind})` : name;
};
