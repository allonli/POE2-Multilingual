# POE2-Multilingual

一个 Windows 原生 WPF 小工具，用于查询 PoE2DB 的简中、繁中、英文名称和内部 `value` 对应关系。应用启动后默认驻留系统托盘，界面采用紧凑的 Spotlight/Raycast 风格：置顶小窗口、快速输入、方向键选择、回车粘贴；结果列表只展示简中、繁中、英文和类型，`value` 保留在二级输出菜单里。

## 运行

仓库提供两种 Windows x64 发布包：

- `dist\Poe2DbLookup-win-x64-self-contained.zip`：直接运行版，不需要预装 .NET，体积较大。解压后运行 `Poe2DbLookup.exe`。
- `dist\Poe2DbLookup-win-x64-dotnet8.zip`：小包，适合已经安装 `.NET 8 Desktop Runtime` 的机器。解压后运行 `Poe2DbLookup.exe`。

如果不确定目标机器有没有 .NET 环境，优先使用直接运行版。仓库里也保留了一份直接运行的单文件 EXE：

```powershell
.\publish\Poe2DbLookup.exe
```

开发环境运行：

```powershell
& 'C:\Users\allon\.dotnet\dotnet.exe' run --project .\Poe2DbLookup.csproj
```

## Raycast 扩展

仓库内新增 `raycast-poe2db-lookup`，提供一套 Raycast 版查询能力。它不注册全局快捷键；在 Raycast 里给 `Search PoE2DB Names` 命令设置你自己的快捷键即可。

### 最简单安装方式

1. 下载只包含 Raycast 插件的压缩包：  
   [raycast-poe2db-lookup-v0.1.2.zip](https://github.com/allonli/POE2-Multilingual/releases/download/raycast-poe2db-lookup-v0.1.2/raycast-poe2db-lookup-v0.1.2.zip)
2. 解压后进入 `raycast-poe2db-lookup` 目录。
3. 在该目录运行：

```powershell
npm install
npm run dev
```

如果你已经安装了 `pnpm`，也可以运行 `pnpm install` 和 `pnpm dev`。Raycast 会打开这个本地扩展。第一次打开成功后，可以在 Raycast 搜索 `Search PoE2DB Names`，并在 Raycast 设置里给这个命令绑定你自己的快捷键。后续不改代码时，可以停止 dev 命令，扩展仍会保留在 Raycast 中。

Raycast 版包含两个命令：

- `Search PoE2DB Names`：打开查询列表；支持简中、繁中、英文、`value`、空格分段模糊和拼音搜索。结果列表显示简中、繁中、英文和类型，`value` 只出现在输出动作里。动作菜单可选择粘贴、复制或打开对应 PoE2DB 页面。
- `Refresh PoE2DB Data`：刷新 PoE2DB autocomplete 数据并写入 Raycast 扩展缓存。

默认情况下，`Search PoE2DB Names` 不会自动读取前台选中文本。Raycast 的 `getSelectedText` 会通过临时复制和窗口切换读取前台应用选区，在游戏窗口里容易造成 Raycast 首次呼出时闪烁或被游戏抢回焦点。如果确实要在浏览器、编辑器等普通文本应用中使用选中文本预填，可以在 Raycast 扩展设置里打开 `Prefill Selected Text`。

Raycast 版缓存位于 Raycast 分配的扩展 support 目录下：

```text
cache\poe2db_names.json
cache\poe2db_search_index.json
```

`poe2db_names.json` 保存原始三语词条；`poe2db_search_index.json` 保存已预计算的搜索字段和拼音字段。升级后第一次打开或运行 `Refresh PoE2DB Data` 会生成搜索索引缓存，后续打开查询命令时不再每次重建拼音索引。

如果要从完整仓库里的源码运行，进入插件目录后执行同样的命令：

```powershell
cd .\raycast-poe2db-lookup
npm install
npm run dev
```

也可以运行 `pnpm install` 和 `pnpm dev`。开发验证仍推荐用仓库锁定的 pnpm 流程：`pnpm install`、`pnpm test`、`pnpm typecheck`、`pnpm build`、`pnpm lint`。`package.json` 的 `author` 当前使用 Raycast 用户名 `allonli`；发布到其他账号前需要改成对应 Raycast 用户名。

## 交互

- 默认 `左 Ctrl+E`：从任意窗口呼出查询窗口，可在设置里修改。应用会优先读取当前焦点控件里的选中文本，并立即显示查询窗口；UIA 不可用且外部窗口不是全屏时，再异步回退到剪贴板 `Ctrl+C`，并用哨兵值避免误读旧剪贴板。全屏外部窗口不会被还原或缩小。没有选中文本时会清空搜索框，避免沿用上一次查询。
- 搜索支持常用繁简体互通、空格分段模糊匹配和拼音匹配，例如 `远射` / `遠射`、`我 火`、`wjnh` / `woji` 都能匹配对应词条。
- 结果列表里出现省略号只是显示裁剪；悬停 Tooltip 和二级菜单复制都会使用完整原文。
- `Esc` 或 `Ctrl+W`：隐藏查询窗口。
- 默认 `F5`：刷新 PoE2DB 数据，可在设置里修改。列表下方状态文案会显示当前刷新快捷键。
- `上 / 下`：选择搜索结果。
- `Enter` 或双击结果：在当前选中结果行附近打开二级输出菜单，可选择粘贴 `简中`、`繁中`、`英文` 或 `value`。按住 `Ctrl` 或 `Alt` 后回车/左键选择菜单项，会打开对应 PoE2DB 语言页面，例如 `https://poe2db.tw/us/Kalguuran_Gems`。如果目标输入框支持 UI Automation 且当前内容被全选或为空，会直接写入完整输出；否则继续使用剪贴板和左 Ctrl+V。
- 查询窗口右下角齿轮按钮：打开设置窗口。
- 系统托盘图标右键菜单：`设置` 打开设置窗口，`退出` 关闭应用。
- 设置窗口支持监听方式修改呼出快捷键、刷新快捷键，并可勾选 `开机启动`；监听新快捷键期间会临时暂停全局呼出热键。

注意：全局热键只有在 `Poe2DbLookup.exe` 正在运行并驻留系统托盘时才生效。

## 数据刷新和缓存

首次启动时，如果本地没有缓存，会自动刷新数据。缓存文件位于 EXE 目录下：

```text
cache\poe2db_names.json
```

刷新流程：

1. 请求 `https://poe2db.tw/cn/`。
2. 从 HTML 解析当前 `poedb_header...js`。
3. 请求 header JS。
4. 从 JS 解析 `autocompletecb_cn/tw/us` 的真实 JSON 文件名。
5. 分别下载三份 JSON。
6. 用 `value` 合并为三语索引并写入缓存。

## 构建和验证

运行核心测试：

```powershell
& 'C:\Users\allon\.dotnet\dotnet.exe' run --project .\tests\Poe2DbLookup.Tests.csproj
```

运行 Raycast 扩展测试和构建：

```powershell
cd .\raycast-poe2db-lookup
$env:Path = 'C:\Users\allon\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin;' + $env:Path
& 'C:\Users\allon\.cache\codex-runtimes\codex-primary-runtime\dependencies\bin\pnpm.cmd' test
& 'C:\Users\allon\.cache\codex-runtimes\codex-primary-runtime\dependencies\bin\pnpm.cmd' build
```

运行联网最小验证：

```powershell
& 'C:\Users\allon\.dotnet\dotnet.exe' run --project .\tests\Poe2DbLookup.Tests.csproj -- --live
```

运行真实窗口交互探针：

```powershell
& 'C:\Users\allon\.dotnet\dotnet.exe' run --project .\tests\Poe2DbLookup.Tests.csproj -- --interaction
```

发布并启动 `.\publish\Poe2DbLookup.exe` 后，运行发布版真实窗口验收：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\RealDotnetTargetVerification.ps1
```

发布直接运行版：

```powershell
& 'C:\Users\allon\.dotnet\dotnet.exe' publish .\Poe2DbLookup.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish-self-contained
```

发布 `.NET 8 Desktop Runtime` 小包：

```powershell
& 'C:\Users\allon\.dotnet\dotnet.exe' publish .\Poe2DbLookup.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=false -o .\publish-dotnet8
```

压缩发布包：

```powershell
Compress-Archive -Path .\publish-self-contained\* -DestinationPath .\dist\Poe2DbLookup-win-x64-self-contained.zip -Force
Compress-Archive -Path .\publish-dotnet8\* -DestinationPath .\dist\Poe2DbLookup-win-x64-dotnet8.zip -Force
```

如果要给 ARM64 Windows 使用，把 `win-x64` 改为 `win-arm64`，并同步调整 zip 文件名。

## 常见问题

- 呼出快捷键没反应：先确认 `.\publish\Poe2DbLookup.exe` 正在运行并且系统托盘里有应用图标；如果改过快捷键，以设置窗口显示为准。
- 小包无法启动：先安装 `.NET 8 Desktop Runtime`，或者改用直接运行版。
- 无法覆盖发布文件：通常是旧版 `Poe2DbLookup.exe` 仍在运行，先关闭进程后重新发布；如果旧进程是管理员权限启动，普通权限无法停止它。
- 找不到 `poedb_header JS`：PoE2DB 页面结构可能变化。
- 找不到 `autocompletecb_*`：header JS 内文件映射格式可能变化。
- JSON 返回 `403/404`：CDN 拒绝或文件 hash 已更新，按 `F5` 重新刷新。
