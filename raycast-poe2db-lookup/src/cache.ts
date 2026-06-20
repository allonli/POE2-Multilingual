import { existsSync } from "node:fs";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";

import type { NameRecord } from "./types";

const CACHE_FILE_NAME = "poe2db_names.json";

export function getCachePath(supportPath: string): string {
  return path.join(supportPath, "cache", CACHE_FILE_NAME);
}

export async function loadCache(cachePath: string): Promise<NameRecord[]> {
  const text = await readFile(cachePath, "utf8");
  const records = JSON.parse(text) as unknown;
  if (!Array.isArray(records) || records.length === 0) {
    throw new Error("本地缓存为空。");
  }

  return records as NameRecord[];
}

export async function saveCache(
  cachePath: string,
  records: NameRecord[],
): Promise<void> {
  await mkdir(path.dirname(cachePath), { recursive: true });
  await writeFile(cachePath, `${JSON.stringify(records, null, 2)}\n`, "utf8");
}

export function hasCache(cachePath: string): boolean {
  return existsSync(cachePath);
}
