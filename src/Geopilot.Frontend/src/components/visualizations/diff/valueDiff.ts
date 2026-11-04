export interface ValueSegment {
  text: string;
  changed: boolean;
}

// Beyond this the token table gets too large to build while rendering; both values are then marked as a whole.
const MAX_COMPARED_TOKEN_PAIRS = 250_000;

const tokenize = (value: string): string[] => value.split(/(\s+)/).filter(token => token.length > 0);

const isWhitespace = (token: string) => /^\s+$/.test(token);

const commonPrefixLength = (a: string, b: string) => {
  let length = 0;
  while (length < a.length && length < b.length && a[length] === b[length]) length++;
  return length;
};

const commonSuffixLength = (a: string, b: string, prefixLength: number) => {
  let length = 0;
  while (
    length < a.length - prefixLength &&
    length < b.length - prefixLength &&
    a[a.length - 1 - length] === b[b.length - 1 - length]
  )
    length++;
  return length;
};

const push = (segments: ValueSegment[], text: string, changed: boolean) => {
  if (text.length === 0) return;
  const last = segments.at(-1);
  if (last && last.changed === changed) last.text += text;
  else segments.push({ text, changed });
};

/**
 * Adds the tokens removed from the old value and inserted into the new one between two common tokens. A single word
 * replaced by a single word is compared by character, so "12" to "12a" marks only the "a".
 */
const pushReplacement = (
  oldSegments: ValueSegment[],
  newSegments: ValueSegment[],
  removed: string[],
  inserted: string[],
) => {
  if (removed.length === 1 && inserted.length === 1 && !isWhitespace(removed[0]) && !isWhitespace(inserted[0])) {
    const [oldWord, newWord] = [removed[0], inserted[0]];
    const prefix = commonPrefixLength(oldWord, newWord);
    const suffix = commonSuffixLength(oldWord, newWord, prefix);
    push(oldSegments, oldWord.slice(0, prefix), false);
    push(oldSegments, oldWord.slice(prefix, oldWord.length - suffix), true);
    push(oldSegments, oldWord.slice(oldWord.length - suffix), false);
    push(newSegments, newWord.slice(0, prefix), false);
    push(newSegments, newWord.slice(prefix, newWord.length - suffix), true);
    push(newSegments, newWord.slice(newWord.length - suffix), false);
    return;
  }
  push(oldSegments, removed.join(""), true);
  push(newSegments, inserted.join(""), true);
};

/**
 * Splits two values of a change into the parts they share and the parts that differ: the longest common sequence of
 * words, and within a single replaced word the characters that differ.
 */
export const diffValueSegments = (oldValue: string, newValue: string): { old: ValueSegment[]; new: ValueSegment[] } => {
  const oldTokens = tokenize(oldValue);
  const newTokens = tokenize(newValue);
  if (oldTokens.length * newTokens.length > MAX_COMPARED_TOKEN_PAIRS) {
    return { old: [{ text: oldValue, changed: true }], new: [{ text: newValue, changed: true }] };
  }

  // lengths[i][j]: the length of the longest common sequence of oldTokens from i and newTokens from j.
  const lengths = Array.from({ length: oldTokens.length + 1 }, () => new Uint32Array(newTokens.length + 1));
  for (let i = oldTokens.length - 1; i >= 0; i--) {
    for (let j = newTokens.length - 1; j >= 0; j--) {
      lengths[i][j] =
        oldTokens[i] === newTokens[j] ? lengths[i + 1][j + 1] + 1 : Math.max(lengths[i + 1][j], lengths[i][j + 1]);
    }
  }

  const oldSegments: ValueSegment[] = [];
  const newSegments: ValueSegment[] = [];
  let removed: string[] = [];
  let inserted: string[] = [];
  let i = 0;
  let j = 0;
  while (i < oldTokens.length || j < newTokens.length) {
    if (i < oldTokens.length && j < newTokens.length && oldTokens[i] === newTokens[j]) {
      pushReplacement(oldSegments, newSegments, removed, inserted);
      removed = [];
      inserted = [];
      push(oldSegments, oldTokens[i], false);
      push(newSegments, newTokens[j], false);
      i++;
      j++;
    } else if (j >= newTokens.length || (i < oldTokens.length && lengths[i + 1][j] >= lengths[i][j + 1])) {
      removed.push(oldTokens[i++]);
    } else {
      inserted.push(newTokens[j++]);
    }
  }
  pushReplacement(oldSegments, newSegments, removed, inserted);
  return { old: oldSegments, new: newSegments };
};

/** The object ids of a reference value, a JSON array of them; undefined when the value is not one. */
export const parseReferences = (value: string): string[] | undefined => {
  try {
    const parsed: unknown = JSON.parse(value);
    return Array.isArray(parsed) && parsed.every(item => typeof item === "string") ? parsed : undefined;
  } catch {
    return undefined;
  }
};
