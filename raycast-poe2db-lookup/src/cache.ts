import { existsSync } from "node:fs";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";

import type { PreparedSearchIndexData } from "./search-index";
import type { NameRecord } from "./types";

const CACHE_FILE_NAME = "poe2db_names.json";
const SEARCH_INDEX_CACHE_FILE_NAME = "poe2db_search_index.json";

export function getCachePath(supportPath: string): string {
  return path.join(supportPath, "cache", CACHE_FILE_NAME);
}

export function getSearchIndexCachePath(supportPath: string): string {
  return path.join(supportPath, "cache", SEARCH_INDEX_CACHE_FILE_NAME);
}

export async function loadCache(cachePath: string): Promise<NameRecord[]> {
  const text = await readFile(cachePath, "utf8");
  const records = JSON.parse(text) as unknown;
  if (!Array.isArray(records) || records.length === 0) {
    throw new Error("本地缓存为空。");
  }

  return records as NameRecord[];
}

export async function loadSearchIndexCache(
  cachePath: string,
): Promise<PreparedSearchIndexData> {
  const text = await readFile(cachePath, "utf8");
  const data = JSON.parse(text) as PreparedSearchIndexData;
  if (
    !data ||
    data.version !== 1 ||
    !Array.isArray(data.records) ||
    !Array.isArray(data.searchRecords) ||
    data.records.length !== data.searchRecords.length
  ) {
    throw new Error("Unsupported PoE2DB search index cache.");
  }

  return data;
}

export async function saveCache(
  cachePath: string,
  records: NameRecord[],
): Promise<void> {
  await mkdir(path.dirname(cachePath), { recursive: true });
  await writeFile(cachePath, `${JSON.stringify(records, null, 2)}\n`, "utf8");
}

export async function saveSearchIndexCache(
  cachePath: string,
  data: PreparedSearchIndexData,
): Promise<void> {
  await mkdir(path.dirname(cachePath), { recursive: true });
  await writeFile(cachePath, `${JSON.stringify(data)}\n`, "utf8");
}

export function hasCache(cachePath: string): boolean {
  return existsSync(cachePath);
}
