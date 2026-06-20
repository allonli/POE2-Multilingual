import { buildPoe2DbUrl } from "./poe2db-url";
import type { NameRecord, OutputChoice } from "./types";

export function getOutputChoices(record: NameRecord): OutputChoice[] {
  return [
    {
      label: "简中",
      text: record.cnLabel,
      url: buildPoe2DbUrl("cn", record.value),
    },
    {
      label: "繁中",
      text: record.twLabel,
      url: buildPoe2DbUrl("tw", record.value),
    },
    {
      label: "英文",
      text: record.usLabel,
      url: buildPoe2DbUrl("us", record.value),
    },
    {
      label: "value",
      text: record.value,
      url: buildPoe2DbUrl("us", record.value),
    },
  ];
}
