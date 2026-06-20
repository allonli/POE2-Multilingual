import type { AutocompleteItem, NameRecord, Poe2DbLanguage } from "./types";

const CN_HOME_URL = "https://poe2db.tw/cn/";
const CDN_JSON_BASE_URL = "https://cdn.poe2db.tw/json/";
const HEADER_SCRIPT_REGEX =
  /(?:(?:https:)?\/\/cdn\.poe2db\.tw\/js\/)?poedb_header[^"'<>]+\.js/i;

export type { AutocompleteItem, NameRecord };

export function findHeaderScriptUrl(html: string): string {
  const match = html.match(HEADER_SCRIPT_REGEX);
  if (!match?.[0]) {
    throw new Error("找不到 poedb_header JS。");
  }

  const value = match[0];
  if (value.startsWith("//")) {
    return `https:${value}`;
  }

  if (/^https?:/i.test(value)) {
    return value;
  }

  return `https://cdn.poe2db.tw/js/${value}`;
}

export function findAutocompleteFileName(
  headerJs: string,
  language: Poe2DbLanguage,
): string {
  const escapedLanguage = escapeRegExp(language);
  const regex = new RegExp(
    `autocompletecb_${escapedLanguage}\\.json["'\`]?\\s*[:=]\\s*["'\`]([^"'\`]+\\.json)`,
    "i",
  );
  const match = headerJs.match(regex);
  if (!match?.[1]) {
    throw new Error(`找不到 autocompletecb_${language} 的真实 JSON 文件名。`);
  }

  return match[1];
}

export function mergeRecords(
  cnItems: AutocompleteItem[],
  twItems: AutocompleteItem[],
  usItems: AutocompleteItem[],
): NameRecord[] {
  const cnByValue = toDictionary(cnItems);
  const twByValue = toDictionary(twItems);
  const usByValue = toDictionary(usItems);
  const values = distinct([
    ...cnByValue.keys(),
    ...twByValue.keys(),
    ...usByValue.keys(),
  ]);

  return values.map((value) => {
    const cn = cnByValue.get(value);
    const tw = twByValue.get(value);
    const us = usByValue.get(value);

    const cnLabel = clean(cn?.label);
    const twLabel = clean(tw?.label);
    const usLabel = clean(us?.label);
    const type = firstNonEmpty(
      cn?.desc,
      tw?.desc,
      us?.desc,
      cn?.class,
      tw?.class,
      us?.class,
    );
    const className = firstNonEmpty(cn?.class, tw?.class, us?.class);

    return {
      value,
      cnLabel: cnLabel || value,
      twLabel: twLabel || value,
      usLabel: usLabel || value,
      type,
      className,
    };
  });
}

export async function refreshPoe2DbData(
  fetchImpl: typeof fetch = fetch,
): Promise<NameRecord[]> {
  const html = await getStringUtf8(CN_HOME_URL, undefined, fetchImpl);
  const headerUrl = findHeaderScriptUrl(html);
  const headerJs = await getStringUtf8(headerUrl, CN_HOME_URL, fetchImpl);

  const [cn, tw, us] = await Promise.all([
    downloadAutocomplete(
      "cn",
      findAutocompleteFileName(headerJs, "cn"),
      fetchImpl,
    ),
    downloadAutocomplete(
      "tw",
      findAutocompleteFileName(headerJs, "tw"),
      fetchImpl,
    ),
    downloadAutocomplete(
      "us",
      findAutocompleteFileName(headerJs, "us"),
      fetchImpl,
    ),
  ]);

  if (cn.length === 0 || tw.length === 0 || us.length === 0) {
    throw new Error("PoE2DB autocomplete 数据为空。");
  }

  const records = mergeRecords(cn, tw, us);
  if (records.length === 0) {
    throw new Error("三语索引为空。");
  }

  return records;
}

async function downloadAutocomplete(
  language: Poe2DbLanguage,
  fileName: string,
  fetchImpl: typeof fetch,
): Promise<AutocompleteItem[]> {
  const url = new URL(fileName, CDN_JSON_BASE_URL).toString();
  const text = await getStringUtf8(
    url,
    `https://poe2db.tw/${language}/`,
    fetchImpl,
  );
  const parsed = JSON.parse(text) as unknown;
  if (!Array.isArray(parsed)) {
    throw new Error(`autocompletecb_${language} JSON 格式无效。`);
  }

  return parsed as AutocompleteItem[];
}

async function getStringUtf8(
  url: string,
  referer: string | undefined,
  fetchImpl: typeof fetch,
): Promise<string> {
  const response = await fetchImpl(url, {
    headers: {
      "User-Agent": "Mozilla/5.0",
      ...(referer ? { Referer: referer } : {}),
    },
  });

  if (response.status === 403 || response.status === 404) {
    throw new Error(
      `下载失败：${url} 返回 ${response.status} ${response.statusText}。`,
    );
  }

  if (!response.ok) {
    throw new Error(
      `下载失败：${url} 返回 ${response.status} ${response.statusText}。`,
    );
  }

  return new TextDecoder("utf-8").decode(await response.arrayBuffer());
}

function toDictionary(
  items: AutocompleteItem[],
): Map<string, AutocompleteItem> {
  const result = new Map<string, AutocompleteItem>();
  for (const item of items) {
    const value = clean(item.value);
    if (value.length > 0 && !result.has(value)) {
      result.set(value, item);
    }
  }

  return result;
}

function distinct(values: string[]): string[] {
  return [...new Set(values)];
}

function clean(value: string | null | undefined): string {
  return value?.trim() ?? "";
}

function firstNonEmpty(...values: Array<string | null | undefined>): string {
  return values.map(clean).find((value) => value.length > 0) ?? "";
}

function escapeRegExp(value: string): string {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}
