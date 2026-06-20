import { environment, showHUD, showToast, Toast } from "@raycast/api";

import { getCachePath, saveCache } from "./cache";
import { refreshPoe2DbData } from "./poe2db-data";

export default async function Command() {
  const toast = await showToast({
    style: Toast.Style.Animated,
    title: "正在刷新 PoE2DB 数据...",
  });

  try {
    const records = await refreshPoe2DbData();
    await saveCache(getCachePath(environment.supportPath), records);
    toast.style = Toast.Style.Success;
    toast.title = "PoE2DB 数据已刷新";
    toast.message = `${records.length.toLocaleString()} 条记录`;
    await showHUD(`PoE2DB 数据已刷新：${records.length.toLocaleString()} 条`);
  } catch (unknownError) {
    const message =
      unknownError instanceof Error
        ? unknownError.message
        : String(unknownError);
    toast.style = Toast.Style.Failure;
    toast.title = "PoE2DB 数据刷新失败";
    toast.message = message;
  }
}
