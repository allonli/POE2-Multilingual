# POE2-Multilingual 开发说明

本项目是一个 Windows WPF 桌面工具，用于查询 PoE2DB 简中、繁中、英文名称和内部 `value` 的对应关系，并把选中的结果粘贴回刚才激活的外部输入框。

## 技术约定

- 主项目：`.NET 8 WPF`，入口为 `Poe2DbLookup.csproj`。
- 核心逻辑放在 `Services` 和 `Models` 下，并保持可被 `tests\Poe2DbLookup.Tests.csproj` 直接编译测试。
- 拼音搜索依赖 NuGet 包 `PinYinConverterCore`，主项目和测试项目需要保持同版本引用。
- Raycast 扩展位于 `raycast-poe2db-lookup`，使用 TypeScript、`@raycast/api`、`opencc-js` 和 `pinyin-pro`，核心逻辑需保持可被 `raycast-poe2db-lookup\tests` 直接测试。
- UI 保持紧凑的 Spotlight/Raycast 风格：小窗口、深色、圆角、置顶、低干扰。
- 不使用 AHK。

## 当前交互约定

- 默认 `左 Ctrl+E`：全局呼出窗口，可在设置里修改。应用会先用 UI Automation 读取当前焦点控件里的选中文本并立即显示查询窗口；UIA 不可用且外部窗口不是全屏时，再异步回退到剪贴板 `Ctrl+C`，剪贴板回退会先写入哨兵值并短重试，避免把旧剪贴板误当成选区。全屏外部窗口不会被还原或缩小。没有选中文本时会清空搜索框。
- 搜索支持常用繁简体互通、空格分段模糊匹配和拼音匹配，例如 `远射` / `遠射`、`我 火`、`wjnh` / `woji` 都能匹配对应词条。
- 结果列表显示 `简中`、`繁中`、`英文` 和 `类型`，不显示 `value` 列；被省略号裁剪的文本可通过 Tooltip 查看完整原文。
- `Enter` 或双击结果：在当前选中结果行附近打开二级输出菜单，可选择粘贴完整的 `简中`、`繁中`、`英文` 或 `value` 到刚才激活的外部窗口输入框；按住 `Ctrl` 或 `Alt` 后回车/左键选择菜单项，会打开对应 PoE2DB 语言页面；目标输入框支持 UI Automation 且当前内容被全选或为空时优先直接写入，否则继续走剪贴板和左 Ctrl+V。
- 默认 `F5`：刷新 PoE2DB 数据，可在设置里修改，列表下方状态文案会显示当前刷新快捷键。界面不显示刷新按钮。
- `Esc` 或 `Ctrl+W`：隐藏窗口。
- `上 / 下`：切换选中搜索结果。
- 默认启动后只驻留系统托盘；托盘图标右键菜单提供 `设置` 和 `退出`。
- 主窗口右下角齿轮按钮打开设置界面；设置界面支持监听方式修改呼出快捷键、刷新快捷键，并可设置开机启动。监听新快捷键期间会临时暂停全局呼出热键，避免 `Ctrl+E` 等旧快捷键抢前台。

## Raycast 扩展约定

- 不在扩展内注册单独全局快捷键；用户在 Raycast 里给 `Search PoE2DB Names` 自行设置快捷键。
- `Search PoE2DB Names` 默认不调用 Raycast `getSelectedText`，避免游戏窗口里触发 Raycast 的 `copy_text` 焦点切换导致首按快捷键闪烁消失。只有用户在扩展设置中显式打开 `Prefill Selected Text` 时才读取前台选中文本作为初始查询。
- Raycast 结果列表显示简中、繁中、英文和类型，不把 `value` 当作列表列展示；`value` 保留在粘贴、复制和打开网页动作中。
- 输出动作分为 `粘贴为...`、`复制为...`、`打开 PoE2DB 页面...`，每组都提供简中、繁中、英文和 `value`。
- `Refresh PoE2DB Data` 是 no-view 命令，刷新 PoE2DB autocomplete 数据并写入 Raycast support 目录下的 `cache\poe2db_names.json`。
- Raycast 版不迁移 WPF 托盘、开机启动、低层键盘 hook、UIA 直接写入等 Windows 桌面专属能力；这些由 Raycast 命令、快捷键设置和 `Action.Paste` 承担。

## 常用命令

```powershell
& 'C:\Users\allon\.dotnet\dotnet.exe' run --project .\tests\Poe2DbLookup.Tests.csproj
& 'C:\Users\allon\.dotnet\dotnet.exe' run --project .\tests\Poe2DbLookup.Tests.csproj -- --live
& 'C:\Users\allon\.dotnet\dotnet.exe' run --project .\tests\Poe2DbLookup.Tests.csproj -- --interaction
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\RealDotnetTargetVerification.ps1
& 'C:\Users\allon\.dotnet\dotnet.exe' build .\Poe2DbLookup.csproj -c Release
& 'C:\Users\allon\.dotnet\dotnet.exe' publish .\Poe2DbLookup.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish

cd .\raycast-poe2db-lookup
$env:Path = 'C:\Users\allon\.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin;' + $env:Path
& 'C:\Users\allon\.cache\codex-runtimes\codex-primary-runtime\dependencies\bin\pnpm.cmd' install
& 'C:\Users\allon\.cache\codex-runtimes\codex-primary-runtime\dependencies\bin\pnpm.cmd' test
& 'C:\Users\allon\.cache\codex-runtimes\codex-primary-runtime\dependencies\bin\pnpm.cmd' typecheck
& 'C:\Users\allon\.cache\codex-runtimes\codex-primary-runtime\dependencies\bin\pnpm.cmd' build
& 'C:\Users\allon\.cache\codex-runtimes\codex-primary-runtime\dependencies\bin\pnpm.cmd' lint
```

## 关键模块

- `MainWindow.xaml`：主界面布局和样式。
- `MainWindow.xaml.cs`：窗口显示、搜索结果选择、热键呼出后的窗口状态和粘贴流程。
- `SettingsWindow.xaml` / `SettingsWindow.xaml.cs`：设置界面、快捷键监听和保存。
- `Services\HotkeyService.cs`：通过低层键盘监听注册可配置的全局呼出快捷键，默认 `左 Ctrl+E`。
- `Services\HotkeyGesture.cs`：快捷键解析、显示和匹配。
- `Services\AppSettings.cs`：设置读写，默认保存到用户配置目录。
- `Services\Poe2DbUrlBuilder.cs`：按语言和 `value` 生成 PoE2DB 词条页面 URL。
- `Services\TrayIconService.cs`：系统托盘图标及 `设置` / `退出` 菜单。
- `Services\StartupManager.cs`：当前用户开机启动注册表项。
- `Services\ClipboardBridge.cs`：读取外部选中文本，并把结果粘贴回外部窗口。读取主路径是 UI Automation，剪贴板是带哨兵和短重试的回退路径；粘贴会先尝试安全的 UIA 全选/空值写入，再回退到剪贴板左 Ctrl+V。
- `Services\Poe2DbClient.cs`：PoE2DB 页面、header JS 和 autocomplete JSON 下载解析。
- `Services\NameIndex.cs`：按 `value` 合并三语数据并提供搜索，搜索匹配会做常用繁简体归一化、空格分段模糊匹配和拼音字段缓存。
- `raycast-poe2db-lookup\src\poe2db-data.ts`：Raycast 版 PoE2DB 下载、header JS 解析、autocomplete 文件解析和三语合并。
- `raycast-poe2db-lookup\src\search-index.ts`：Raycast 版搜索索引，支持繁简归一、分段模糊和拼音匹配。
- `raycast-poe2db-lookup\src\search.tsx`：Raycast 查询列表和粘贴、复制、打开网页动作。
- `raycast-poe2db-lookup\src\refresh.ts`：Raycast no-view 刷新命令。
- `raycast-poe2db-lookup\tests\core.test.ts`：Raycast 版核心解析、合并、搜索和输出动作测试。

## 易错点

- 全局热键依赖 `Poe2DbLookup.exe` 进程正在运行并驻留系统托盘。
- 读取外部选中文本时，不要先让查询窗口覆盖当前焦点控件；应先捕获当前焦点控件选区，再置前查询窗口。
- 粘贴目标由最近一次外部前台窗口记录决定，不要在查询窗口自身激活时覆盖它。
- 列表里不显示 `value`，但二级菜单必须保留完整 `value` 输出；列表省略号不能影响实际复制字符串。
- PoE2DB 在 `poe2db.tw` 下需要使用 `autocompletecb_{lang}`，不是 `autocomplete_{lang}`。
- 三语合并必须按 `value` 对齐，不能按 label。
- 刷新 JSON 时必须带 `Referer` 和 `User-Agent`。
- Raycast manifest 的 `author` 必须是有效 Raycast 用户名；当前为 `allonli`，发布到别的账号前要同步修改。
- Raycast 的 `getSelectedText` 会尝试从前台应用复制选中文本；在 Path of Exile 这类游戏窗口里会造成焦点来回切换，默认不要在查询命令启动时调用。
- 本机普通 PATH 可能没有 `node`；运行 Raycast 扩展验证时先把 Codex 自带 Node 加到 PATH。
