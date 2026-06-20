import assert from "node:assert/strict";
import { test } from "node:test";

import { buildPoe2DbUrl } from "../src/poe2db-url";
import { findAutocompleteFileName, findHeaderScriptUrl, mergeRecords, type AutocompleteItem } from "../src/poe2db-data";
import { createSearchIndex } from "../src/search-index";
import { getOutputChoices } from "../src/output";

test("parses PoE2DB header script URL from HTML", () => {
  const html = '<script defer src="https://cdn.poe2db.tw/js/poedb_header.d4672c828b046d1e.js"></script>';

  assert.equal(findHeaderScriptUrl(html), "https://cdn.poe2db.tw/js/poedb_header.d4672c828b046d1e.js");
});

test("parses autocompletecb file names from header JS", () => {
  const js = `
    const files = {
      "autocompletecb_cn.json": "autocompletecb_cn.111.json",
      "autocompletecb_tw.json": "autocompletecb_tw.222.json",
      "autocompletecb_us.json": "autocompletecb_us.333.json"
    };
  `;

  assert.equal(findAutocompleteFileName(js, "cn"), "autocompletecb_cn.111.json");
  assert.equal(findAutocompleteFileName(js, "tw"), "autocompletecb_tw.222.json");
  assert.equal(findAutocompleteFileName(js, "us"), "autocompletecb_us.333.json");
});

test("merges multilingual records by value and preserves output fields", () => {
  const records = mergeRecords(
    [item("远射 I", "Longshot_I", "辅助宝石", "gemitem")],
    [item("遠射 I", "Longshot_I", "輔助寶石", "gemitem")],
    [item("Longshot I", "Longshot_I", "Support Gems", "gemitem")],
  );

  assert.deepEqual(records, [
    {
      value: "Longshot_I",
      cnLabel: "远射 I",
      twLabel: "遠射 I",
      usLabel: "Longshot I",
      type: "辅助宝石",
      className: "gemitem",
    },
  ]);
});

test("search ranks labels, values, simplified/traditional variants, fuzzy tokens, and pinyin", () => {
  const index = createSearchIndex([
    {
      value: "Longshot_I",
      cnLabel: "远射 I",
      twLabel: "遠射 I",
      usLabel: "Longshot I",
      type: "辅助宝石",
      className: "gemitem",
    },
    {
      value: "I_Am_Rage",
      cnLabel: "我即怒火",
      twLabel: "我即怒火",
      usLabel: "I Am Rage",
      type: "Skill",
      className: "skill",
    },
  ]);

  assert.equal(index.search("Longshot_I")[0]?.value, "Longshot_I");
  assert.equal(index.search("遠射")[0]?.value, "Longshot_I");
  assert.equal(index.search("远射")[0]?.value, "Longshot_I");
  assert.equal(index.search("我 火")[0]?.value, "I_Am_Rage");
  assert.equal(index.search("wjnh")[0]?.value, "I_Am_Rage");
  assert.equal(index.search("woji")[0]?.value, "I_Am_Rage");
});

test("builds language URLs and output choices for copy, paste, and browser actions", () => {
  const record = {
    value: "Kalguuran_Gems",
    cnLabel: "卡古兰宝石",
    twLabel: "卡古蘭寶石",
    usLabel: "Kalguuran Gems",
    type: "Gems",
    className: "gemitem",
  };

  assert.equal(buildPoe2DbUrl("us", record.value), "https://poe2db.tw/us/Kalguuran_Gems");
  assert.deepEqual(getOutputChoices(record), [
    { label: "简中", text: "卡古兰宝石", url: "https://poe2db.tw/cn/Kalguuran_Gems" },
    { label: "繁中", text: "卡古蘭寶石", url: "https://poe2db.tw/tw/Kalguuran_Gems" },
    { label: "英文", text: "Kalguuran Gems", url: "https://poe2db.tw/us/Kalguuran_Gems" },
    { label: "value", text: "Kalguuran_Gems", url: "https://poe2db.tw/us/Kalguuran_Gems" },
  ]);
});

function item(label: string, value: string, desc: string, className: string): AutocompleteItem {
  return { label, value, desc, class: className };
}
