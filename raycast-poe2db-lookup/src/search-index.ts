import { Converter } from "opencc-js";
import { pinyin } from "pinyin-pro";

import type { NameRecord } from "./types";

const toSimplified = Converter({ from: "tw", to: "cn" });
const TRADITIONAL_TO_SIMPLIFIED = new Map<string, string>([
  ["遠", "远"],
  ["擊", "击"],
  ["寶", "宝"],
  ["輔", "辅"],
  ["傷", "伤"],
  ["屬", "属"],
  ["閃", "闪"],
  ["電", "电"],
  ["臺", "台"],
  ["術", "术"],
  ["發", "发"],
  ["髮", "发"],
  ["級", "级"],
  ["體", "体"],
  ["點", "点"],
  ["與", "与"],
  ["戰", "战"],
  ["鬥", "斗"],
  ["劍", "剑"],
  ["護", "护"],
  ["轉", "转"],
  ["換", "换"],
  ["啟", "启"],
  ["動", "动"],
  ["雙", "双"],
  ["單", "单"],
  ["靈", "灵"],
  ["氣", "气"],
  ["強", "强"],
  ["無", "无"],
  ["盡", "尽"],
  ["風", "风"],
  ["龍", "龙"],
  ["壓", "压"],
  ["獄", "狱"],
  ["燒", "烧"],
  ["錄", "录"],
  ["選", "选"],
  ["擇", "择"],
  ["輸", "输"],
  ["貼", "贴"],
]);

export const SEARCH_INDEX_CACHE_VERSION = 1;

export type PreparedSearchRecord = {
  record: NameRecord;
  fields: string[];
  normalizedFields: string[];
  pinyinFields: string[];
};

export type PreparedSearchIndexData = {
  version: typeof SEARCH_INDEX_CACHE_VERSION;
  records: NameRecord[];
  searchRecords: PreparedSearchRecord[];
};

export type SearchIndex = {
  records: NameRecord[];
  search(query: string, limit?: number): NameRecord[];
};

export function createSearchIndex(records: NameRecord[]): SearchIndex {
  return createSearchIndexFromData(prepareSearchIndexData(records));
}

export function prepareSearchIndexData(
  records: NameRecord[],
): PreparedSearchIndexData {
  const sortedRecords = records
    .filter((record) => record.value.trim().length > 0)
    .sort((left, right) => {
      const labelComparison = left.usLabel.localeCompare(
        right.usLabel,
        undefined,
        { sensitivity: "base" },
      );
      return labelComparison !== 0
        ? labelComparison
        : left.value.localeCompare(right.value);
    });
  const searchRecords = sortedRecords.map(createSearchRecord);

  return {
    version: SEARCH_INDEX_CACHE_VERSION,
    records: sortedRecords,
    searchRecords,
  };
}

export function createSearchIndexFromData(
  data: PreparedSearchIndexData,
): SearchIndex {
  if (
    data.version !== SEARCH_INDEX_CACHE_VERSION ||
    data.records.length !== data.searchRecords.length
  ) {
    throw new Error("Unsupported PoE2DB search index cache.");
  }

  const sortedRecords = data.records;
  const searchRecords = data.searchRecords;

  return {
    records: sortedRecords,
    search(query: string, limit = 80): NameRecord[] {
      const trimmedQuery = query.trim();
      if (trimmedQuery.length === 0) {
        return sortedRecords.slice(0, limit);
      }

      const normalizedQuery = normalizeSearchText(trimmedQuery);
      const queryTokens = splitSearchTokens(normalizedQuery);
      const pinyinQuery = normalizePinyinText(trimmedQuery);

      return searchRecords
        .map((item) => ({
          record: item.record,
          score: matchScore(
            item,
            trimmedQuery,
            normalizedQuery,
            queryTokens,
            pinyinQuery,
          ),
          labelLength: shortestMatchingLength(
            item,
            trimmedQuery,
            normalizedQuery,
            queryTokens,
            pinyinQuery,
          ),
        }))
        .filter((item) => item.score < Number.MAX_SAFE_INTEGER)
        .sort((left, right) => {
          if (left.score !== right.score) {
            return left.score - right.score;
          }

          if (left.labelLength !== right.labelLength) {
            return left.labelLength - right.labelLength;
          }

          const labelComparison = left.record.usLabel.localeCompare(
            right.record.usLabel,
            undefined,
            {
              sensitivity: "base",
            },
          );
          return labelComparison !== 0
            ? labelComparison
            : left.record.value.localeCompare(right.record.value);
        })
        .slice(0, limit)
        .map((item) => item.record);
    },
  };
}

export function normalizeSearchText(value: string): string {
  const simplified = toSimplified(value.trim().toLowerCase());
  return [...simplified]
    .map((char) => TRADITIONAL_TO_SIMPLIFIED.get(char) ?? char)
    .join("");
}

function createSearchRecord(record: NameRecord): PreparedSearchRecord {
  const fields = [record.cnLabel, record.twLabel, record.usLabel, record.value];
  return {
    record,
    fields,
    normalizedFields: fields.map(normalizeSearchText),
    pinyinFields: [...new Set(fields.flatMap(buildPinyinSearchFields))],
  };
}

function matchScore(
  item: PreparedSearchRecord,
  query: string,
  normalizedQuery: string,
  queryTokens: string[],
  pinyinQuery: string,
): number {
  if (
    item.fields.some((field) => equalsIgnoreCase(field, query)) ||
    item.normalizedFields.some((field) => field === normalizedQuery)
  ) {
    return 0;
  }

  if (
    item.fields.some((field) => startsWithIgnoreCase(field, query)) ||
    item.normalizedFields.some((field) => field.startsWith(normalizedQuery))
  ) {
    return 1;
  }

  if (
    item.fields.some((field) => containsIgnoreCase(field, query)) ||
    item.normalizedFields.some((field) => field.includes(normalizedQuery))
  ) {
    return 2;
  }

  if (
    queryTokens.length > 1 &&
    item.normalizedFields.some((field) =>
      containsOrderedTokens(field, queryTokens),
    )
  ) {
    return 3;
  }

  if (
    pinyinQuery.length > 0 &&
    item.pinyinFields.some((field) => field === pinyinQuery)
  ) {
    return 3;
  }

  if (
    pinyinQuery.length > 0 &&
    item.pinyinFields.some((field) => field.startsWith(pinyinQuery))
  ) {
    return 4;
  }

  if (
    pinyinQuery.length > 0 &&
    item.pinyinFields.some((field) => field.includes(pinyinQuery))
  ) {
    return 5;
  }

  return Number.MAX_SAFE_INTEGER;
}

function shortestMatchingLength(
  item: PreparedSearchRecord,
  query: string,
  normalizedQuery: string,
  queryTokens: string[],
  pinyinQuery: string,
): number {
  const matchingLengths = item.fields
    .map((field, index) => ({
      field,
      normalizedField: item.normalizedFields[index] ?? "",
    }))
    .filter(
      ({ field, normalizedField }) =>
        containsIgnoreCase(field, query) ||
        normalizedField.includes(normalizedQuery) ||
        (queryTokens.length > 1 &&
          containsOrderedTokens(normalizedField, queryTokens)),
    )
    .map(({ field }) => field.length);

  const labelLength = Math.min(...matchingLengths, Number.MAX_SAFE_INTEGER);
  if (
    labelLength < Number.MAX_SAFE_INTEGER ||
    pinyinQuery.length === 0 ||
    !item.pinyinFields.some((field) => field.includes(pinyinQuery))
  ) {
    return labelLength;
  }

  return Math.min(
    ...item.fields.map((field) => field.length),
    Number.MAX_SAFE_INTEGER,
  );
}

function splitSearchTokens(value: string): string[] {
  return value.split(/\s+/).filter(Boolean);
}

function containsOrderedTokens(field: string, queryTokens: string[]): boolean {
  let searchStart = 0;
  for (const token of queryTokens) {
    const tokenIndex = field.indexOf(token, searchStart);
    if (tokenIndex < 0) {
      return false;
    }

    searchStart = tokenIndex + token.length;
  }

  return true;
}

function normalizePinyinText(value: string): string {
  return [...value.toLowerCase()]
    .filter((char) => /[a-z0-9]/.test(char))
    .join("");
}

function buildPinyinSearchFields(field: string): string[] {
  const normalizedField = normalizeSearchText(field);
  if (!hasChinese(normalizedField)) {
    return [];
  }

  let full = "";
  let initials = "";
  for (const char of normalizedField) {
    if (hasChinese(char)) {
      const syllable = getPinyinSyllable(char);
      if (syllable.length > 0) {
        full += syllable;
        initials += syllable[0] ?? "";
      }
      continue;
    }

    if (/[a-z0-9]/i.test(char)) {
      full += char.toLowerCase();
      initials += char.toLowerCase();
    }
  }

  return [...new Set([full, initials].filter(Boolean))];
}

function getPinyinSyllable(char: string): string {
  try {
    const syllables = pinyin(char, {
      toneType: "none",
      type: "array",
    }) as string[];
    return normalizePinyinSyllable(syllables[0] ?? "");
  } catch {
    return "";
  }
}

function normalizePinyinSyllable(value: string): string {
  return value
    .trim()
    .toLowerCase()
    .replaceAll("u:", "v")
    .replaceAll("ü", "v")
    .split("")
    .filter((char) => /[a-z]/.test(char))
    .join("");
}

function hasChinese(value: string): boolean {
  return /[\u3400-\u9fff]/.test(value);
}

function equalsIgnoreCase(left: string, right: string): boolean {
  return left.localeCompare(right, undefined, { sensitivity: "accent" }) === 0;
}

function startsWithIgnoreCase(left: string, right: string): boolean {
  return left.toLowerCase().startsWith(right.toLowerCase());
}

function containsIgnoreCase(left: string, right: string): boolean {
  return left.toLowerCase().includes(right.toLowerCase());
}
