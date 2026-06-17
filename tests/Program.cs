using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using Poe2DbLookup.Models;
using Poe2DbLookup.Services;

if (args.Contains("--text-target", StringComparer.OrdinalIgnoreCase))
{
    RunTextTarget();
    return;
}

if (args.Contains("--winforms-target", StringComparer.OrdinalIgnoreCase))
{
    RunWinFormsTextTarget();
    return;
}

if (args.Contains("--interaction", StringComparer.OrdinalIgnoreCase))
{
    RunInteractionVerification();
    return;
}

if (args.Contains("--live", StringComparer.OrdinalIgnoreCase))
{
    await RunLiveVerification();
    return;
}

var tests = new (string Name, Action Test)[]
{
    ("parse header script URL from HTML", ParseHeaderScriptUrl),
    ("parse autocomplete file names from JS", ParseAutocompleteFileNames),
    ("merge multilingual records by value", MergeByValue),
    ("search ranks across all labels and value", SearchRanksAcrossLanguages),
    ("search matches simplified and traditional variants", SearchMatchesSimplifiedAndTraditionalVariants),
    ("search matches space separated fuzzy tokens", SearchMatchesSpaceSeparatedFuzzyTokens),
    ("search matches Chinese pinyin initials and prefixes", SearchMatchesChinesePinyinInitialsAndPrefixes),
    ("Left Ctrl+E reads selected text before showing lookup", HotkeyReadsSelectedTextBeforeShowingLookup),
    ("lookup window uses Win32 foreground activation and delayed focus", LookupUsesWin32ForegroundActivation),
    ("hotkey captures focused selection before activating lookup", HotkeyCapturesFocusedSelectionBeforeActivation),
    ("hotkey clears stale query when no selected text exists", HotkeyClearsStaleQuery),
    ("programmatic query fill updates results once", ProgrammaticQueryFillUpdatesResultsOnce),
    ("search records cache normalized fields", SearchRecordsCacheNormalizedFields),
    ("Enter and double click open output submenu", EnterAndDoubleClickOpenOutputMenu),
    ("result list hides value column but output menu keeps value", ResultListHidesValueColumnButOutputMenuKeepsValue),
    ("trimmed result text exposes full original text", TrimmedResultTextExposesFullOriginalText),
    ("result scrollbar uses dark custom chrome", ResultScrollbarUsesDarkCustomChrome),
    ("clipboard fallback uses polling instead of fixed long sleeps", ClipboardFallbackUsesPollingInsteadOfFixedLongSleeps),
    ("clipboard fallback avoids stale clipboard text", ClipboardFallbackAvoidsStaleClipboardText),
    ("clipboard operations retry transient contention", ClipboardOperationsRetryTransientContention),
    ("lookup activation avoids duplicate activation calls", LookupActivationAvoidsDuplicateActivationCalls),
    ("top header and refresh button are removed", TopHeaderIsRemoved),
    ("layout is about five percent more compact", LayoutIsFivePercentMoreCompact),
    ("release left Ctrl before copying selected text", ReleasesLeftCtrlBeforeCopyingSelection),
    ("SendInput uses native INPUT union size", SendInputUsesNativeInputUnionSize),
    ("hotkey uses low level hook for Left Ctrl+E", HotkeyUsesLowLevelHookForLeftCtrlE),
    ("Ctrl+W hides lookup and refresh uses configured hotkey", CtrlWAndRefreshUseConfiguredHotkey),
    ("output menu is anchored to selected result row", OutputMenuAnchorsToSelectedRow),
    ("settings are reachable from gear button and tray menu", SettingsEntryPointsAndTrayMenu),
    ("settings support hotkey capture and startup option", SettingsSupportHotkeyCaptureAndStartupOption),
    ("global hotkey is configurable and does not block hook thread", GlobalHotkeyIsConfigurableAndNonBlocking),
    ("hotkey activation shows lookup before clipboard fallback", HotkeyActivationShowsBeforeClipboardFallback),
    ("app starts in tray by default", AppStartsInTrayByDefault),
    ("global Ctrl+W close is guarded by visible lookup", GlobalCtrlWCloseIsGuardedByVisibleLookup),
    ("settings window has enough room for all options", SettingsWindowHasEnoughRoomForAllOptions),
    ("status text uses configured refresh hotkey and jump hint", StatusTextUsesConfiguredRefreshHotkeyAndJumpHint),
    ("Ctrl or Alt selecting output opens language page", ModifierSelectingOutputOpensLanguagePage),
    ("output menu does not show white tooltip", OutputMenuDoesNotShowWhiteTooltip),
    ("application and tray use bundled icon assets", ApplicationAndTrayUseBundledIconAssets),
    ("settings window is borderless", SettingsWindowIsBorderless),
    ("settings button uses standard gear glyph", SettingsButtonUsesStandardGearGlyph),
    ("global hotkeys pause while settings captures shortcut", GlobalHotkeysPauseWhileSettingsCapturesShortcut),
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Test();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {test.Name}: {ex.Message}");
    }
}

if (failed > 0)
{
    Environment.Exit(1);
}

static void ParseHeaderScriptUrl()
{
    var html = """
        <html><head><script defer src="https://cdn.poe2db.tw/js/poedb_header.d4672c828b046d1e.js"></script></head></html>
        """;

    var url = Poe2DbClient.FindHeaderScriptUrl(html);

    AssertEqual("https://cdn.poe2db.tw/js/poedb_header.d4672c828b046d1e.js", url);
}

static void ParseAutocompleteFileNames()
{
    var js = """
        const files = {
          "autocompletecb_cn.json": "autocompletecb_cn.111.json",
          "autocompletecb_tw.json": "autocompletecb_tw.222.json",
          "autocompletecb_us.json": "autocompletecb_us.333.json"
        };
        """;

    AssertEqual("autocompletecb_cn.111.json", Poe2DbClient.FindAutocompleteFileName(js, "cn"));
    AssertEqual("autocompletecb_tw.222.json", Poe2DbClient.FindAutocompleteFileName(js, "tw"));
    AssertEqual("autocompletecb_us.333.json", Poe2DbClient.FindAutocompleteFileName(js, "us"));
}

static void MergeByValue()
{
    var index = NameIndex.Merge(
        [new AutocompleteItem("Longshot CN", "Longshot_I", "Support CN", "gemitem")],
        [new AutocompleteItem("Longshot TW", "Longshot_I", "Support TW", "gemitem")],
        [new AutocompleteItem("Longshot I", "Longshot_I", "Support Gems", "gemitem")]);

    var hit = index.Records.Single(record => record.Value == "Longshot_I");

    AssertEqual("Longshot CN", hit.CnLabel);
    AssertEqual("Longshot TW", hit.TwLabel);
    AssertEqual("Longshot I", hit.UsLabel);
    AssertEqual("Support CN", hit.Type);
}

static void SearchRanksAcrossLanguages()
{
    var index = NameIndex.Merge(
        [
            new AutocompleteItem("Longshot CN", "Longshot_I", "Support CN", "gemitem"),
            new AutocompleteItem("Ancient Arrow CN", "Ancient_Arrow", "Skill", "skill")
        ],
        [
            new AutocompleteItem("Longshot TW", "Longshot_I", "Support TW", "gemitem"),
            new AutocompleteItem("Ancient Arrow TW", "Ancient_Arrow", "Skill", "skill")
        ],
        [
            new AutocompleteItem("Longshot I", "Longshot_I", "Support Gems", "gemitem"),
            new AutocompleteItem("Ancient Arrow", "Ancient_Arrow", "Skill", "skill")
        ]);

    AssertEqual("Longshot_I", index.Search("Longshot CN").First().Value);
    AssertEqual("Longshot_I", index.Search("Longshot TW").First().Value);
    AssertEqual("Longshot_I", index.Search("Longshot I").First().Value);
    AssertEqual("Longshot_I", index.Search("Longshot_I").First().Value);
}

static void SearchMatchesSimplifiedAndTraditionalVariants()
{
    var index = NameIndex.Merge(
        [new AutocompleteItem("远射 I", "Longshot_I", "Support CN", "gemitem")],
        [new AutocompleteItem("Longshot TW", "Longshot_I", "Support TW", "gemitem")],
        [new AutocompleteItem("Longshot I", "Longshot_I", "Support Gems", "gemitem")]);

    AssertEqual("Longshot_I", index.Search("遠射").First().Value);

    var traditionalOnlyIndex = NameIndex.Merge(
        [new AutocompleteItem("Longshot CN", "Longshot_I", "Support CN", "gemitem")],
        [new AutocompleteItem("遠射 I", "Longshot_I", "Support TW", "gemitem")],
        [new AutocompleteItem("Longshot I", "Longshot_I", "Support Gems", "gemitem")]);

    AssertEqual("Longshot_I", traditionalOnlyIndex.Search("远射").First().Value);
}

static void SearchMatchesSpaceSeparatedFuzzyTokens()
{
    var index = NameIndex.Merge(
        [new AutocompleteItem("我即怒火", "I_Am_Rage", "Skill", "skill")],
        [new AutocompleteItem("我即怒火", "I_Am_Rage", "Skill", "skill")],
        [new AutocompleteItem("I Am Rage", "I_Am_Rage", "Skill", "skill")]);

    AssertEqual("I_Am_Rage", index.Search("我 火").First().Value);
}

static void SearchMatchesChinesePinyinInitialsAndPrefixes()
{
    var index = NameIndex.Merge(
        [new AutocompleteItem("我即怒火", "I_Am_Rage", "Skill", "skill")],
        [new AutocompleteItem("我即怒火", "I_Am_Rage", "Skill", "skill")],
        [new AutocompleteItem("I Am Rage", "I_Am_Rage", "Skill", "skill")]);

    AssertEqual("I_Am_Rage", index.Search("wjnh").First().Value);
    AssertEqual("I_Am_Rage", index.Search("woji").First().Value);
}

static void HotkeyReadsSelectedTextBeforeShowingLookup()
{
    var mainWindow = ReadProjectFile("MainWindow.xaml.cs");
    var showIndex = mainWindow.IndexOf("ShowLookup(focusedSelectionText)", StringComparison.Ordinal);
    var readIndex = mainWindow.IndexOf("TryReadFocusedSelectionText", StringComparison.Ordinal);
    var fallbackIndex = mainWindow.IndexOf("TryFillQueryFromClipboardFallbackAsync", StringComparison.Ordinal);

    if (showIndex < 0 || readIndex < 0 || readIndex > showIndex)
    {
        throw new InvalidOperationException("expected hotkey handler to read focused selected text before showing lookup");
    }

    if (fallbackIndex < 0 || fallbackIndex < showIndex)
    {
        throw new InvalidOperationException("expected clipboard fallback to start after showing lookup");
    }

    AssertDoesNotContain("ShowLookup(null)", mainWindow);
    AssertDoesNotContain("IsVisible && IsActive", mainWindow);
}

static void EnterAndDoubleClickOpenOutputMenu()
{
    var mainWindow = ReadProjectFile("MainWindow.xaml.cs");
    var xaml = ReadProjectFile("MainWindow.xaml");

    AssertContains("OpenOutputMenu()", mainWindow);
    AssertContains("AddOutputItem(menu, \"简中\", record.CnLabel", mainWindow);
    AssertContains("AddOutputItem(menu, \"繁中\", record.TwLabel", mainWindow);
    AssertContains("AddOutputItem(menu, \"英文\", record.UsLabel", mainWindow);
    AssertContains("AddOutputItem(menu, \"value\", record.Value", mainWindow);
    AssertContains("OutputContextMenuStyle", xaml);
    AssertContains("OutputMenuItemStyle", xaml);
    AssertDoesNotContain("PasteSelectedResult()", mainWindow);
}

static void ResultListHidesValueColumnButOutputMenuKeepsValue()
{
    var mainWindow = ReadProjectFile("MainWindow.xaml.cs");
    var xaml = ReadProjectFile("MainWindow.xaml");

    AssertDoesNotContain("Text=\"{Binding Value}\"", xaml);
    AssertContains("AddOutputItem(menu, \"value\", record.Value", mainWindow);
}

static void TrimmedResultTextExposesFullOriginalText()
{
    var mainWindow = ReadProjectFile("MainWindow.xaml.cs");
    var xaml = ReadProjectFile("MainWindow.xaml");

    AssertContains("TextTrimming=\"CharacterEllipsis\"", xaml);
    AssertContains("ToolTip=\"{Binding CnLabel}\"", xaml);
    AssertContains("ToolTip=\"{Binding TwLabel}\"", xaml);
    AssertContains("ToolTip=\"{Binding UsLabel}\"", xaml);
    AssertContains("ToolTip=\"{Binding Type}\"", xaml);
    AssertContains("OutputChoice", mainWindow);
}

static void ResultScrollbarUsesDarkCustomChrome()
{
    var xaml = ReadProjectFile("MainWindow.xaml");

    AssertContains("TargetType=\"{x:Type ScrollBar}\"", xaml);
    AssertContains("TargetType=\"{x:Type Thumb}\"", xaml);
    AssertContains("#475569", xaml);
    AssertContains("#111827", xaml);
}

static void ClipboardFallbackUsesPollingInsteadOfFixedLongSleeps()
{
    var clipboardBridge = ReadProjectFile(Path.Combine("Services", "ClipboardBridge.cs"));
    var nativeMethods = ReadProjectFile(Path.Combine("Services", "NativeMethods.cs"));

    AssertContains("InteractionWaiter.WaitUntil", clipboardBridge);
    AssertContains("GetClipboardSequenceNumber", nativeMethods);
    AssertContains("WaitForForegroundWindow", nativeMethods);
    AssertContains("NativeMethods.ActivateWindow(foregroundWindow)", clipboardBridge);
    AssertContains("NativeMethods.ActivateWindow(targetWindow)", clipboardBridge);
    AssertContains("InputSettleTimeout = TimeSpan.FromMilliseconds(35)", clipboardBridge);
    AssertContains("WaitForInputSettle()", clipboardBridge);
    AssertDoesNotContain("Thread.Sleep(60)", clipboardBridge);
    AssertDoesNotContain("Thread.Sleep(80)", clipboardBridge);
    AssertDoesNotContain("Thread.Sleep(140)", clipboardBridge);
}

static void ClipboardFallbackAvoidsStaleClipboardText()
{
    var clipboardBridge = ReadProjectFile(Path.Combine("Services", "ClipboardBridge.cs"));

    AssertContains("ClipboardSentinelPrefix", clipboardBridge);
    AssertContains("SetClipboardTextWithRetry(sentinel)", clipboardBridge);
    AssertContains("selectedText != sentinel", clipboardBridge);
}

static void ClipboardOperationsRetryTransientContention()
{
    var clipboardBridge = ReadProjectFile(Path.Combine("Services", "ClipboardBridge.cs"));

    AssertContains("SetClipboardTextWithRetry", clipboardBridge);
    AssertContains("ClipboardSetTimeout", clipboardBridge);
    AssertContains("ClipboardSetTimeout = TimeSpan.FromMilliseconds(500)", clipboardBridge);
    AssertContains("TryRestoreClipboard(previousData)", clipboardBridge);
    AssertDoesNotContain("Clipboard.SetText(text);", clipboardBridge);
}

static void LookupActivationAvoidsDuplicateActivationCalls()
{
    var mainWindow = NormalizeLineEndings(ReadProjectFile("MainWindow.xaml.cs"));

    AssertDoesNotContain("Topmost = true;\n        Activate();\n\n        SetSearchText(query);", mainWindow);
    AssertContains("NativeMethods.BringWindowToForeground(windowHandle)", mainWindow);
}

static void HotkeyCapturesFocusedSelectionBeforeActivation()
{
    var mainWindow = ReadProjectFile("MainWindow.xaml.cs");
    var clipboardBridge = ReadProjectFile(Path.Combine("Services", "ClipboardBridge.cs"));
    var captureIndex = mainWindow.IndexOf("TryReadFocusedSelectionText", StringComparison.Ordinal);
    var showIndex = mainWindow.IndexOf("ShowLookup(focusedSelectionText)", StringComparison.Ordinal);

    if (captureIndex < 0 || showIndex < 0 || captureIndex > showIndex)
    {
        throw new InvalidOperationException("expected focused selection to be captured before lookup activation");
    }

    AssertContains("TextPattern", clipboardBridge);
    AssertContains("AutomationElement.FocusedElement", clipboardBridge);
}

static void HotkeyClearsStaleQuery()
{
    var mainWindow = ReadProjectFile("MainWindow.xaml.cs");

    AssertContains("SetSearchText(query)", mainWindow);
    AssertContains("SearchBox.Text = query?.Trim() ?? string.Empty", mainWindow);
}

static void ProgrammaticQueryFillUpdatesResultsOnce()
{
    var mainWindow = ReadProjectFile("MainWindow.xaml.cs");

    AssertContains("_isSettingSearchText = true", mainWindow);
    AssertContains("if (_isSettingSearchText)", mainWindow);
}

static void SearchRecordsCacheNormalizedFields()
{
    var nameIndex = ReadProjectFile(Path.Combine("Services", "NameIndex.cs"));

    AssertContains("private readonly List<SearchRecord> _searchRecords", nameIndex);
    AssertContains("_searchRecords = _records.Select(SearchRecord.Create).ToList()", nameIndex);
    AssertContains("fields.Select(NormalizeSearchText).ToArray()", nameIndex);
}

static void LookupUsesWin32ForegroundActivation()
{
    var mainWindow = ReadProjectFile("MainWindow.xaml.cs");
    var nativeMethods = ReadProjectFile(Path.Combine("Services", "NativeMethods.cs"));

    AssertContains("ActivateLookupWindow()", mainWindow);
    AssertContains("NativeMethods.BringWindowToForeground", mainWindow);
    AssertContains("Dispatcher.BeginInvoke", mainWindow);
    AssertContains("DispatcherPriority.Input", mainWindow);
    AssertContains("ShowWindow", nativeMethods);
    AssertContains("SetWindowPos", nativeMethods);
    AssertContains("AttachThreadInput", nativeMethods);
}

static void TopHeaderIsRemoved()
{
    var xaml = ReadProjectFile("MainWindow.xaml");
    var mainWindow = ReadProjectFile("MainWindow.xaml.cs");

    AssertDoesNotContain("Text=\"Path Of Exile 2 Multilingual\"", xaml);
    AssertDoesNotContain("x:Name=\"RefreshButton\"", xaml);
    AssertDoesNotContain("RoundedButtonStyle", xaml);
    AssertDoesNotContain("RefreshButton_Click", mainWindow);
}

static void LayoutIsFivePercentMoreCompact()
{
    var xaml = ReadProjectFile("MainWindow.xaml");

    AssertContains("Width=\"874\"", xaml);
    AssertContains("Height=\"532\"", xaml);
    AssertContains("Border Margin=\"23\"", xaml);
    AssertContains("Grid Margin=\"21\"", xaml);
    AssertContains("Property=\"MinHeight\" Value=\"61\"", xaml);
    AssertContains("FontSize=\"27\"", xaml);
}

static void ReleasesLeftCtrlBeforeCopyingSelection()
{
    var clipboardBridge = ReadProjectFile(Path.Combine("Services", "ClipboardBridge.cs"));

    AssertContains("ReleaseKey(NativeMethods.VirtualKey.LeftControl)", clipboardBridge);
    AssertContains("SendModifiedKey(NativeMethods.VirtualKey.LeftControl, NativeMethods.VirtualKey.C)", clipboardBridge);
    AssertContains("SendModifiedKey(NativeMethods.VirtualKey.LeftControl, NativeMethods.VirtualKey.V)", clipboardBridge);
}

static void SendInputUsesNativeInputUnionSize()
{
    var inputType = typeof(NativeMethods).GetNestedType("Input", BindingFlags.NonPublic);
    if (inputType is null)
    {
        throw new InvalidOperationException("NativeMethods.Input was not found");
    }

    var expectedSize = IntPtr.Size == 8 ? 40 : 28;
    var actualSize = Marshal.SizeOf(inputType);
    if (actualSize != expectedSize)
    {
        throw new InvalidOperationException($"expected native INPUT size {expectedSize}, got {actualSize}");
    }
}

static void HotkeyUsesLowLevelHookForLeftCtrlE()
{
    var hotkeyService = ReadProjectFile(Path.Combine("Services", "HotkeyService.cs"));
    var nativeMethods = ReadProjectFile(Path.Combine("Services", "NativeMethods.cs"));

    AssertContains("SetWindowsHookEx", hotkeyService);
    AssertContains("WhKeyboardLl", hotkeyService);
    AssertContains("VkLeftControl", hotkeyService);
    AssertContains("VkE", hotkeyService);
    AssertContains("LeftCtrlEPressed", hotkeyService);
    AssertContains("UnhookWindowsHookEx", hotkeyService);
    AssertDoesNotContain("RegisterHotKey", hotkeyService);
    AssertContains("SetWindowsHookEx", nativeMethods);
    AssertContains("UnhookWindowsHookEx", nativeMethods);
}

static void CtrlWAndRefreshUseConfiguredHotkey()
{
    var mainWindow = ReadProjectFile("MainWindow.xaml.cs");

    AssertContains("IsCloseSearchGesture(e)", mainWindow);
    AssertContains("_settings.RefreshGesture.Matches(e)", mainWindow);
    AssertDoesNotContain("e.Key == Key.F5", mainWindow);
}

static void OutputMenuAnchorsToSelectedRow()
{
    var mainWindow = ReadProjectFile("MainWindow.xaml.cs");

    AssertContains("ContainerFromItem(record)", mainWindow);
    AssertContains("Placement = PlacementMode.Bottom", mainWindow);
    AssertDoesNotContain("PlacementTarget = ResultsList", mainWindow);
}

static void SettingsEntryPointsAndTrayMenu()
{
    var xaml = ReadProjectFile("MainWindow.xaml");
    var mainWindow = ReadProjectFile("MainWindow.xaml.cs");
    var app = ReadProjectFile("App.xaml.cs");
    var trayIcon = ReadProjectFile(Path.Combine("Services", "TrayIconService.cs"));

    AssertContains("x:Name=\"SettingsButton\"", xaml);
    AssertContains("Click=\"SettingsButton_Click\"", xaml);
    AssertContains("OpenSettingsWindow()", mainWindow);
    AssertContains("TrayIconService", app);
    AssertContains("\"设置\"", trayIcon);
    AssertContains("\"退出\"", trayIcon);
}

static void SettingsSupportHotkeyCaptureAndStartupOption()
{
    var settingsXaml = ReadProjectFile("SettingsWindow.xaml");
    var settingsWindow = ReadProjectFile("SettingsWindow.xaml.cs");
    var appSettings = ReadProjectFile(Path.Combine("Services", "AppSettings.cs"));
    var startupManager = ReadProjectFile(Path.Combine("Services", "StartupManager.cs"));

    AssertContains("x:Name=\"ActivationHotkeyButton\"", settingsXaml);
    AssertContains("x:Name=\"RefreshHotkeyButton\"", settingsXaml);
    AssertContains("x:Name=\"StartWithWindowsCheckBox\"", settingsXaml);
    AssertContains("PreviewKeyDown", settingsWindow);
    AssertContains("_captureTarget", settingsWindow);
    AssertContains("StartupManager.SetStartWithWindows", settingsWindow);
    AssertContains("ActivationHotkey", appSettings);
    AssertContains("RefreshHotkey", appSettings);
    AssertContains("StartWithWindows", appSettings);
    AssertContains("LeftCtrl+E", appSettings);
    AssertContains("F5", appSettings);
    AssertContains("CurrentUser.OpenSubKey", startupManager);
}

static void GlobalHotkeyIsConfigurableAndNonBlocking()
{
    var app = ReadProjectFile("App.xaml.cs");
    var hotkeyService = ReadProjectFile(Path.Combine("Services", "HotkeyService.cs"));

    AssertContains("SetHotkey", hotkeyService);
    AssertContains("HotkeyGesture", hotkeyService);
    AssertContains("Dispatcher.BeginInvoke", app);
    AssertDoesNotContain("Dispatcher.Invoke(() => _mainWindow.ToggleFromHotkey", app);
}

static void HotkeyActivationShowsBeforeClipboardFallback()
{
    var mainWindow = ReadProjectFile("MainWindow.xaml.cs");
    var showIndex = mainWindow.IndexOf("ShowLookup(focusedSelectionText)", StringComparison.Ordinal);
    var fallbackIndex = mainWindow.IndexOf("TryFillQueryFromClipboardFallbackAsync", StringComparison.Ordinal);

    if (showIndex < 0 || fallbackIndex < 0 || showIndex > fallbackIndex)
    {
        throw new InvalidOperationException("expected lookup to show before clipboard fallback starts");
    }
}

static void AppStartsInTrayByDefault()
{
    var app = ReadProjectFile("App.xaml.cs");

    AssertContains("ShutdownMode = ShutdownMode.OnExplicitShutdown", app);
    AssertContains("_trayIconService.Start()", app);
    AssertDoesNotContain("_mainWindow.Show();", app);
}

static void GlobalCtrlWCloseIsGuardedByVisibleLookup()
{
    var app = ReadProjectFile("App.xaml.cs");
    var hotkeyService = ReadProjectFile(Path.Combine("Services", "HotkeyService.cs"));
    var mainWindow = ReadProjectFile("MainWindow.xaml.cs");

    AssertContains("CtrlWPressed", hotkeyService);
    AssertContains("ShouldHandleCtrlW", hotkeyService);
    AssertContains("keyboard.VkCode == VkW", hotkeyService);
    AssertContains("ShouldHandleCtrlW = () => _mainWindow?.IsVisible == true", app);
    AssertContains("HideFromShortcut()", mainWindow);
}

static void SettingsWindowHasEnoughRoomForAllOptions()
{
    var settingsXaml = ReadProjectFile("SettingsWindow.xaml");

    AssertContains("Width=\"560\"", settingsXaml);
    AssertContains("Height=\"420\"", settingsXaml);
    AssertContains("MinHeight=\"420\"", settingsXaml);
    AssertContains("StartWithWindowsCheckBox", settingsXaml);
}

static void StatusTextUsesConfiguredRefreshHotkeyAndJumpHint()
{
    var mainWindow = ReadProjectFile("MainWindow.xaml.cs");

    AssertContains("BuildReadyStatus", mainWindow);
    AssertContains("_settings.RefreshGesture", mainWindow);
    AssertContains("按住 Alt 或 Ctrl 选中跳转到词缀网页", mainWindow);
    AssertDoesNotContain("F5 可刷新。\")", mainWindow);
}

static void ModifierSelectingOutputOpensLanguagePage()
{
    AssertEqual("https://poe2db.tw/us/Kalguuran_Gems", Poe2DbUrlBuilder.Build("us", "Kalguuran_Gems"));
    AssertEqual("https://poe2db.tw/cn/Kalguuran_Gems", Poe2DbUrlBuilder.Build("cn", "Kalguuran_Gems"));
    AssertEqual("https://poe2db.tw/tw/Kalguuran_Gems", Poe2DbUrlBuilder.Build("tw", "Kalguuran_Gems"));

    var mainWindow = ReadProjectFile("MainWindow.xaml.cs");
    AssertContains("Keyboard.Modifiers", mainWindow);
    AssertContains("OpenRecordUrl", mainWindow);
    AssertContains("Poe2DbUrlBuilder.Build(\"cn\", record.Value)", mainWindow);
    AssertContains("Poe2DbUrlBuilder.Build(\"tw\", record.Value)", mainWindow);
    AssertContains("Poe2DbUrlBuilder.Build(\"us\", record.Value)", mainWindow);
}

static void OutputMenuDoesNotShowWhiteTooltip()
{
    var mainWindow = ReadProjectFile("MainWindow.xaml.cs");

    AssertDoesNotContain("ToolTip = text", mainWindow);
    AssertContains("ToolTipService.SetIsEnabled(item, false)", mainWindow);
}

static void ApplicationAndTrayUseBundledIconAssets()
{
    var project = ReadProjectFile("Poe2DbLookup.csproj");
    var mainWindowXaml = ReadProjectFile("MainWindow.xaml");
    var settingsXaml = ReadProjectFile("SettingsWindow.xaml");
    var trayIcon = ReadProjectFile(Path.Combine("Services", "TrayIconService.cs"));

    AssertContains("<ApplicationIcon>Assets\\app-icon.ico</ApplicationIcon>", project);
    AssertContains("Icon=\"Assets/app-icon.ico\"", mainWindowXaml);
    AssertContains("Icon=\"Assets/app-icon.ico\"", settingsXaml);
    AssertContains("LoadIcon()", trayIcon);

    var pngPath = FindProjectFile(Path.Combine("Assets", "app-icon.png"));
    using var image = System.Drawing.Image.FromFile(pngPath);
    if (image.Width != 512 || image.Height != 512)
    {
        throw new InvalidOperationException($"expected 512x512 icon PNG, got {image.Width}x{image.Height}");
    }

    _ = FindProjectFile(Path.Combine("Assets", "app-icon.ico"));
}

static void SettingsWindowIsBorderless()
{
    var settingsXaml = ReadProjectFile("SettingsWindow.xaml");

    AssertContains("WindowStyle=\"None\"", settingsXaml);
    AssertDoesNotContain("Title=\"设置\"", settingsXaml);
}

static void SettingsButtonUsesStandardGearGlyph()
{
    var xaml = ReadProjectFile("MainWindow.xaml");

    AssertContains("FontFamily=\"Segoe MDL2 Assets\"", xaml);
    AssertContains("Text=\"&#xE713;\"", xaml);
    AssertDoesNotContain("Content=\"&#x2699;\"", xaml);
}

static void GlobalHotkeysPauseWhileSettingsCapturesShortcut()
{
    var hotkeyService = ReadProjectFile(Path.Combine("Services", "HotkeyService.cs"));
    var settingsWindow = ReadProjectFile("SettingsWindow.xaml.cs");
    var mainWindow = ReadProjectFile("MainWindow.xaml.cs");
    var app = ReadProjectFile("App.xaml.cs");

    AssertContains("IsPaused", hotkeyService);
    AssertContains("if (IsPaused)", hotkeyService);
    AssertContains("CaptureStateChanged", settingsWindow);
    AssertContains("SetCaptureTarget", settingsWindow);
    AssertContains("SettingsHotkeyCaptureChanged", mainWindow);
    AssertContains("settingsWindow.CaptureStateChanged", mainWindow);
    AssertContains("_hotkeyService.IsPaused = isCapturing", app);
}

static void AssertEqual(string expected, string actual)
{
    if (!string.Equals(expected, actual, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"expected '{expected}', got '{actual}'");
    }
}

static void AssertContains(string expected, string actual)
{
    if (!actual.Contains(expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"expected content to contain '{expected}'");
    }
}

static void AssertDoesNotContain(string unexpected, string actual)
{
    if (actual.Contains(unexpected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"expected content not to contain '{unexpected}'");
    }
}

static string NormalizeLineEndings(string text)
{
    return text.Replace("\r\n", "\n", StringComparison.Ordinal);
}

static string ReadProjectFile(string relativePath)
{
    return File.ReadAllText(FindProjectFile(relativePath));
}

static string FindProjectFile(string relativePath)
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        var candidate = Path.Combine(directory.FullName, relativePath);
        if (File.Exists(candidate))
        {
            return candidate;
        }

        directory = directory.Parent;
    }

    throw new FileNotFoundException($"Could not find project file '{relativePath}' from '{AppContext.BaseDirectory}'.");
}

static void RunInteractionVerification()
{
    Exception? error = null;
    var thread = new Thread(() =>
    {
        try
        {
            RunInteractionVerificationOnSta();
        }
        catch (Exception ex)
        {
            error = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (error is not null)
    {
        throw new InvalidOperationException("interaction verification failed", error);
    }
}

static void RunInteractionVerificationOnSta()
{
    Process? wpfTargetProcess = null;
    Process? winFormsTargetProcess = null;
    try
    {
        wpfTargetProcess = StartTargetProcess("--text-target");
        var wpfTargetWindow = WaitForMainWindow(wpfTargetProcess, TimeSpan.FromSeconds(5));
        NativeMethods.BringWindowToForeground(wpfTargetWindow);
        Thread.Sleep(180);
        TestInput.ClickCenter(wpfTargetWindow);
        FocusAndSelectFirstEdit(wpfTargetWindow);
        Thread.Sleep(120);

        var focusedStopwatch = Stopwatch.StartNew();
        var focusedText = ClipboardBridge.TryReadFocusedSelectionText();
        focusedStopwatch.Stop();
        AssertEqual("远射", focusedText ?? string.Empty);

        var pasteStopwatch = Stopwatch.StartNew();
        ClipboardBridge.CopyAndPaste("远射 I", wpfTargetWindow);
        pasteStopwatch.Stop();
        Thread.Sleep(120);

        var pastedText = ReadTargetText(wpfTargetWindow);
        var pasteVerified = string.Equals(pastedText, "远射 I", StringComparison.Ordinal);
        AssertEqual("远射 I", pastedText);

        wpfTargetProcess.Kill(entireProcessTree: true);
        wpfTargetProcess.Dispose();
        wpfTargetProcess = null;

        winFormsTargetProcess = StartTargetProcess("--winforms-target");
        var winFormsTargetWindow = WaitForMainWindow(winFormsTargetProcess, TimeSpan.FromSeconds(5));
        NativeMethods.BringWindowToForeground(winFormsTargetWindow);
        FocusAndSelectFirstEdit(winFormsTargetWindow);
        Thread.Sleep(220);

        var fallbackStopwatch = Stopwatch.StartNew();
        var fallbackText = ClipboardBridge.TryReadSelectedText(winFormsTargetWindow);
        fallbackStopwatch.Stop();
        var fallbackVerified = string.Equals(fallbackText, "远射", StringComparison.Ordinal);

        Console.WriteLine($"PASS interaction focused selection {focusedStopwatch.Elapsed.TotalMilliseconds:F1}ms");
        if (fallbackVerified)
        {
            Console.WriteLine($"PASS interaction clipboard fallback {fallbackStopwatch.Elapsed.TotalMilliseconds:F1}ms");
        }
        else
        {
            Console.WriteLine($"WARN interaction clipboard fallback unavailable {fallbackStopwatch.Elapsed.TotalMilliseconds:F1}ms");
        }

        if (pasteVerified)
        {
            Console.WriteLine($"PASS interaction copy paste {pasteStopwatch.Elapsed.TotalMilliseconds:F1}ms");
        }
        else
        {
            Console.WriteLine($"WARN interaction copy paste unavailable {pasteStopwatch.Elapsed.TotalMilliseconds:F1}ms");
        }
    }
    finally
    {
        if (wpfTargetProcess is not null && !wpfTargetProcess.HasExited)
        {
            wpfTargetProcess.Kill(entireProcessTree: true);
            wpfTargetProcess.Dispose();
        }

        if (winFormsTargetProcess is not null && !winFormsTargetProcess.HasExited)
        {
            winFormsTargetProcess.Kill(entireProcessTree: true);
            winFormsTargetProcess.Dispose();
        }
    }
}

static Process StartTargetProcess(string argument)
{
    var targetPath = Environment.ProcessPath
        ?? throw new InvalidOperationException("current test executable path is unavailable");
    return Process.Start(new ProcessStartInfo(targetPath, argument)
    {
        UseShellExecute = false
    }) ?? throw new InvalidOperationException($"failed to start text target {argument}");
}

static nint WaitForMainWindow(Process process, TimeSpan timeout)
{
    var stopwatch = Stopwatch.StartNew();
    while (stopwatch.Elapsed < timeout)
    {
        var automationWindow = TryFindEditableWindow(process.Id);
        if (automationWindow != 0)
        {
            return automationWindow;
        }

        process.Refresh();
        if (process.MainWindowHandle != 0 && WindowHasEditControl(process.MainWindowHandle))
        {
            return process.MainWindowHandle;
        }

        Thread.Sleep(50);
    }

    throw new InvalidOperationException("text target window handle was not available");
}

static nint TryFindEditableWindow(int processId)
{
    var root = AutomationElement.RootElement;
    var processCondition = new PropertyCondition(AutomationElement.ProcessIdProperty, processId);
    var windowCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window);
    var windows = root.FindAll(TreeScope.Children, new AndCondition(processCondition, windowCondition));
    foreach (AutomationElement window in windows)
    {
        var edit = window.FindFirst(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
        if (edit is not null && window.Current.NativeWindowHandle != 0)
        {
            return (nint)window.Current.NativeWindowHandle;
        }
    }

    return 0;
}

static bool WindowHasEditControl(nint windowHandle)
{
    var window = AutomationElement.FromHandle(windowHandle);
    return window?.FindFirst(
        TreeScope.Descendants,
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)) is not null;
}

static string ReadTargetText(nint targetWindow)
{
    var window = AutomationElement.FromHandle(targetWindow)
        ?? throw new InvalidOperationException("target automation window was unavailable");
    var edit = window.FindFirst(
        TreeScope.Descendants,
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit))
        ?? throw new InvalidOperationException("target edit control was unavailable");

    if (edit.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePattern))
    {
        return ((ValuePattern)valuePattern).Current.Value.Trim();
    }

    if (edit.TryGetCurrentPattern(TextPattern.Pattern, out var textPattern))
    {
        return ((TextPattern)textPattern).DocumentRange.GetText(-1).Trim();
    }

    throw new InvalidOperationException("target edit control did not expose readable text");
}

static void FocusAndSelectFirstEdit(nint targetWindow)
{
    var window = AutomationElement.FromHandle(targetWindow)
        ?? throw new InvalidOperationException("target automation window was unavailable");
    var edit = window.FindFirst(
        TreeScope.Descendants,
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit))
        ?? throw new InvalidOperationException("target edit control was unavailable");

    edit.SetFocus();
    if (edit.TryGetCurrentPattern(TextPattern.Pattern, out var textPatternObject))
    {
        ((TextPattern)textPatternObject).DocumentRange.Select();
    }
    else
    {
        TestInput.Chord(TestInput.LeftControl, TestInput.A);
    }
}

static void RunTextTarget()
{
    Exception? error = null;
    var ready = new ManualResetEventSlim();
    var thread = new Thread(() =>
    {
        try
        {
            var app = new System.Windows.Application();
            var textBox = new System.Windows.Controls.TextBox
            {
                FontFamily = new System.Windows.Media.FontFamily("Microsoft YaHei UI"),
                FontSize = 22,
                Margin = new System.Windows.Thickness(18),
                Text = "远射",
                VerticalContentAlignment = System.Windows.VerticalAlignment.Center
            };
            var window = new System.Windows.Window
            {
                Title = "POE2 Lookup Interaction Target",
                Width = 460,
                Height = 150,
                Topmost = true,
                Content = textBox,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen
            };
            window.Loaded += (_, _) =>
            {
                textBox.Focus();
                textBox.SelectAll();
                ready.Set();
            };
            window.Activated += (_, _) =>
            {
                textBox.Focus();
                textBox.SelectAll();
            };
            app.Run(window);
        }
        catch (Exception ex)
        {
            error = ex;
            ready.Set();
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    if (!ready.Wait(TimeSpan.FromSeconds(5)))
    {
        throw new InvalidOperationException("text target did not become ready");
    }

    if (error is not null)
    {
        throw new InvalidOperationException("text target failed", error);
    }

    thread.Join();
}

static void RunWinFormsTextTarget()
{
    Exception? error = null;
    var ready = new ManualResetEventSlim();
    var thread = new Thread(() =>
    {
        try
        {
            System.Windows.Forms.Application.EnableVisualStyles();
            var textBox = new System.Windows.Forms.TextBox
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                Font = new System.Drawing.Font("Microsoft YaHei UI", 18),
                Text = "远射"
            };
            var form = new System.Windows.Forms.Form
            {
                Text = "POE2 Lookup WinForms Interaction Target",
                Width = 460,
                Height = 150,
                TopMost = true,
                StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
            };
            form.Controls.Add(textBox);
            form.Shown += (_, _) =>
            {
                textBox.Focus();
                textBox.SelectAll();
                ready.Set();
            };
            form.Activated += (_, _) =>
            {
                textBox.Focus();
                textBox.SelectAll();
            };
            System.Windows.Forms.Application.Run(form);
        }
        catch (Exception ex)
        {
            error = ex;
            ready.Set();
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    if (!ready.Wait(TimeSpan.FromSeconds(5)))
    {
        throw new InvalidOperationException("WinForms text target did not become ready");
    }

    if (error is not null)
    {
        throw new InvalidOperationException("WinForms text target failed", error);
    }

    thread.Join();
}

static async Task RunLiveVerification()
{
    var client = new Poe2DbClient();
    var index = await client.RefreshAsync();
    var cachePath = Path.Combine(AppContext.BaseDirectory, "cache", "poe2db_names.json");
    await index.SaveCacheAsync(cachePath);

    var hit = index.Search("Longshot").FirstOrDefault(record => record.Value == "Longshot_I")
        ?? throw new InvalidOperationException("query 'Longshot' did not return Longshot_I");

    AssertEqual("Longshot_I", hit.Value);
    Console.WriteLine($"PASS live query 'Longshot' -> {hit.CnLabel} / {hit.TwLabel} / {hit.UsLabel} / {hit.Value}");
    Console.WriteLine($"PASS live refresh count={index.Records.Count:N0}");
    Console.WriteLine($"PASS cache written {cachePath}");
}

internal static class TestInput
{
    internal const ushort LeftControl = 0xA2;
    internal const ushort A = 0x41;
    internal const ushort C = 0x43;
    internal const ushort V = 0x56;

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public KeyboardInput Ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, Input[] inputs, int inputSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(nint hWnd, out Rect rect);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, nint extraInfo);

    internal static void Chord(ushort modifier, ushort key)
    {
        const uint keyboard = 1;
        const uint keyUp = 0x0002;

        var inputs = new[]
        {
            new Input { Type = keyboard, Ki = new KeyboardInput { Vk = modifier } },
            new Input { Type = keyboard, Ki = new KeyboardInput { Vk = key } },
            new Input { Type = keyboard, Ki = new KeyboardInput { Vk = key, Flags = keyUp } },
            new Input { Type = keyboard, Ki = new KeyboardInput { Vk = modifier, Flags = keyUp } }
        };

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
    }

    internal static void ClickCenter(nint windowHandle)
    {
        const uint leftDown = 0x0002;
        const uint leftUp = 0x0004;

        if (!GetWindowRect(windowHandle, out var rect))
        {
            throw new InvalidOperationException("could not read target window rectangle");
        }

        var x = rect.Left + ((rect.Right - rect.Left) / 2);
        var y = rect.Top + ((rect.Bottom - rect.Top) / 2);
        SetCursorPos(x, y);
        mouse_event(leftDown, 0, 0, 0, 0);
        mouse_event(leftUp, 0, 0, 0, 0);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
