import type { Poe2DbLanguage } from "./types";

export function buildPoe2DbUrl(
  language: Poe2DbLanguage,
  value: string,
): string {
  const normalizedLanguage = language.trim().toLowerCase() as Poe2DbLanguage;
  const normalizedValue = value.trim();

  if (normalizedLanguage.length === 0) {
    throw new Error("language is required");
  }

  if (normalizedValue.length === 0) {
    throw new Error("value is required");
  }

  return `https://poe2db.tw/${normalizedLanguage}/${encodeURIComponent(normalizedValue)}`;
}
