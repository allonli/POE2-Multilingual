import {
  Action,
  ActionPanel,
  Color,
  environment,
  getSelectedText,
  Icon,
  List,
  showToast,
  Toast,
} from "@raycast/api";
import { useEffect, useMemo, useState } from "react";

import { getCachePath, hasCache, loadCache, saveCache } from "./cache";
import { refreshPoe2DbData } from "./poe2db-data";
import { getOutputChoices } from "./output";
import { createSearchIndex, type SearchIndex } from "./search-index";
import type { NameRecord } from "./types";

export default function Command() {
  const [searchText, setSearchText] = useState("");
  const [index, setIndex] = useState<SearchIndex | null>(null);
  const [status, setStatus] = useState("正在加载 PoE2DB 数据...");
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const cachePath = useMemo(() => getCachePath(environment.supportPath), []);
  const results = useMemo(
    () => index?.search(searchText) ?? [],
    [index, searchText],
  );

  useEffect(() => {
    let cancelled = false;

    async function initialize() {
      try {
        const selectedText = await readSelectedText();
        if (!cancelled && selectedText) {
          setSearchText(selectedText);
        }

        const records = hasCache(cachePath)
          ? await loadCache(cachePath)
          : await refreshAndSave(cachePath);
        if (cancelled) {
          return;
        }

        setIndex(createSearchIndex(records));
        setStatus(`已加载 ${records.length.toLocaleString()} 条 PoE2DB 记录`);
      } catch (unknownError) {
        if (!cancelled) {
          const message = errorMessage(unknownError);
          setError(message);
          setStatus(`错误：${message}`);
          await showToast({
            style: Toast.Style.Failure,
            title: "PoE2DB 数据加载失败",
            message,
          });
        }
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    }

    void initialize();
    return () => {
      cancelled = true;
    };
  }, [cachePath]);

  async function refreshData() {
    setIsLoading(true);
    setError(null);
    const toast = await showToast({
      style: Toast.Style.Animated,
      title: "正在刷新 PoE2DB 数据...",
    });
    try {
      const records = await refreshAndSave(cachePath);
      setIndex(createSearchIndex(records));
      setStatus(`刷新完成：${records.length.toLocaleString()} 条 PoE2DB 记录`);
      toast.style = Toast.Style.Success;
      toast.title = "PoE2DB 数据已刷新";
      toast.message = `${records.length.toLocaleString()} 条记录`;
    } catch (unknownError) {
      const message = errorMessage(unknownError);
      setError(message);
      setStatus(`刷新失败：${message}`);
      toast.style = Toast.Style.Failure;
      toast.title = "PoE2DB 数据刷新失败";
      toast.message = message;
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <List
      isLoading={isLoading}
      filtering={false}
      navigationTitle="PoE2DB Lookup"
      searchBarPlaceholder="搜索简中、繁中、英文、value 或拼音"
      searchText={searchText}
      onSearchTextChange={setSearchText}
    >
      <List.Section title={status}>
        {error ? (
          <List.Item
            title="数据不可用"
            subtitle={error}
            icon={{ source: Icon.Warning, tintColor: Color.Red }}
          />
        ) : null}
        {results.map((record) => (
          <RecordItem
            key={record.value}
            record={record}
            onRefresh={refreshData}
          />
        ))}
      </List.Section>
    </List>
  );
}

function RecordItem({
  record,
  onRefresh,
}: {
  record: NameRecord;
  onRefresh: () => Promise<void>;
}) {
  return (
    <List.Item
      title={record.cnLabel}
      subtitle={record.usLabel}
      accessories={[
        { text: record.twLabel },
        record.type
          ? { tag: { value: record.type, color: Color.SecondaryText } }
          : { text: record.className },
      ]}
      actions={<RecordActions record={record} onRefresh={onRefresh} />}
    />
  );
}

function RecordActions({
  record,
  onRefresh,
}: {
  record: NameRecord;
  onRefresh: () => Promise<void>;
}) {
  const choices = getOutputChoices(record);
  return (
    <ActionPanel title={record.usLabel}>
      <ActionPanel.Submenu title="粘贴为…" icon={Icon.TextCursor}>
        {choices.map((choice) => (
          <Action.Paste
            key={choice.label}
            title={`粘贴${choice.label}`}
            content={choice.text}
          />
        ))}
      </ActionPanel.Submenu>
      <ActionPanel.Submenu title="复制为…" icon={Icon.Clipboard}>
        {choices.map((choice) => (
          <Action.CopyToClipboard
            key={choice.label}
            title={`复制${choice.label}`}
            content={choice.text}
          />
        ))}
      </ActionPanel.Submenu>
      <ActionPanel.Submenu title="打开 PoE2DB 页面…" icon={Icon.Globe}>
        {choices.map((choice) => (
          <Action.OpenInBrowser
            key={choice.label}
            title={`打开${choice.label}页面`}
            url={choice.url}
          />
        ))}
      </ActionPanel.Submenu>
      <Action
        title="刷新 PoE2DB 数据"
        icon={Icon.ArrowClockwise}
        onAction={onRefresh}
      />
    </ActionPanel>
  );
}

async function refreshAndSave(cachePath: string): Promise<NameRecord[]> {
  const records = await refreshPoe2DbData();
  await saveCache(cachePath, records);
  return records;
}

async function readSelectedText(): Promise<string> {
  try {
    return (await getSelectedText()).trim();
  } catch {
    return "";
  }
}

function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}
