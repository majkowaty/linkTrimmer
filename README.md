<div align="center">

# LinkTrimmer

**A tiny Windows tray app that cleans up every link you copy.**

![Platform](https://img.shields.io/badge/platform-Windows-0078D6?logo=windows&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-9-512BD4?logo=dotnet&logoColor=white)
![UI](https://img.shields.io/badge/UI-WinForms-blue)
![Languages](https://img.shields.io/badge/UI%20languages-PL%20%7C%20EN%20%7C%20中文-informational)

</div>

LinkTrimmer runs in the background (an "LT" icon in the system tray) and **automatically fixes links the moment you copy them**:

- removes tracking parameters (`utm_*`, `fbclid`, `igsh`, `si`, ...),
- for selected sites, swaps the domain for one that gives a better embed (e.g. `x.com` → `fxtwitter.com`),
- **leaves alone** images, files, plain text, and text that merely contains a link.

| You copy | Clipboard now contains |
|---|---|
| `https://x.com/user/status/123?s=20&t=abc` | `https://fxtwitter.com/user/status/123` |
| `https://www.youtube.com/watch?v=abc&si=track&t=10` | `https://www.youtube.com/watch?v=abc&t=10` |
| `https://example.com/page?id=5&utm_source=x&fbclid=y` | `https://example.com/page?id=5` |
| `look at https://x.com/a?s=1 lol` | *(untouched – it is not just a link)* |

Rules are fully editable from a built-in window (add / edit / delete), and the UI is available in **Polski, English and 中文**.

## Table of contents

1. [Quick start](#quick-start)
2. [How it works](#how-it-works)
3. [Adding a rule](#adding-a-rule)
4. [How rules are applied (exact logic)](#how-rules-are-applied-exact-logic)
5. [Project structure](#project-structure)
6. [Building and publishing](#building-and-publishing)
7. [Data, configuration, language](#data-configuration-language)
8. [Security and privacy](#security-and-privacy)
9. [Limitations](#limitations)

---

## Quick start

Requirements: Windows 10/11 and the [.NET SDK 9 or newer](https://dotnet.microsoft.com/download).

```powershell
git clone <this-repo-url>
cd linkTrimmer
dotnet run --project src/LinkTrimmer.App
```

The window opens; closing it with **X** hides the app to the tray, where it keeps working. Right-click the tray icon and choose **Exit** to quit completely.

To build your own `.exe` that you can start without a terminal, follow [Building the .exe yourself](#building-the-exe-yourself).

---

## How it works

### Listening to the clipboard

Windows can notify an app when the clipboard changes. LinkTrimmer registers a hidden *message-only* window (it draws nothing) with `AddClipboardFormatListener`. From then on, after **every** clipboard change the system sends it a `WM_CLIPBOARDUPDATE` message. There is no polling loop, so between changes the app does nothing and uses almost no CPU.

The app does **not** hook the keyboard (no Ctrl+C hook), does not inject into other processes, and does not scan the system. It reacts the same way to Ctrl+C, a "Copy" menu item, "Share/Copy link" buttons, and programmatic clipboard writes.

### What happens when you copy a link

```text
You copy a link  ->  clipboard holds the ORIGINAL link
                 ->  Windows sends WM_CLIPBOARDUPDATE
                 ->  ClipboardWatcher reads the text from the clipboard
                 ->  LinkProcessor.Process(text)
                        - not a single http(s) link?   -> null (do nothing)
                        - result identical to input?   -> null (do nothing)
                        - otherwise                    -> cleaned link
                 ->  the cleaned link REPLACES the clipboard contents
```

Consequences:

- **The link is fixed inside the clipboard, after you copy it**, not before. For a few milliseconds the original is in the clipboard.
- Windows clipboard history (Win+V) and other clipboard managers will record **both** entries (original and cleaned).
- The app's own write also triggers `WM_CLIPBOARDUPDATE`. To avoid a loop, it remembers the last text it wrote (`_lastWritten`) and skips it. Processing is also idempotent (feeding a cleaned link back in returns `null`).
- Images and files (`CF_BITMAP`, `CF_DIB`, `CF_HDROP` formats) are rejected before any text is read. Text longer than 4096 characters is also skipped without analysis.

### Memory

After the last clipboard change, a timer waits 3 seconds of quiet, then forces a full garbage collection (`GC.Collect`) and returns unused memory to the OS. This keeps the process from holding on to memory after a burst of copying.

### Window and tray icon

- Closing the window with "X" **hides it to the tray**; the app keeps running.
- Right-click the tray icon: Open / Enabled (toggle) / Language / **Exit**. Only "Exit" actually ends the process.
- **Start with Windows:** a checkbox in the window and an item in the tray menu. It adds (or removes) one value named `LinkTrimmer` under `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`, which makes Windows start `LinkTrimmer.exe --minimized` at login (no administrator rights needed). You can also see or remove it in *Task Manager → Startup apps*.
  - The entry points at the exact `.exe` you ran it from. If you move or rename the file, the checkbox shows as off; tick it again from the new location.
- The `--minimized` argument starts the app already hidden in the tray.
- Only one instance runs at a time.

---

## Adding a rule

There are three ways. All of them describe the same thing: **a rule for a site** (matched by domain).

### 1. In the app window (easiest)

Open the window (click the tray icon) and click **Add**. Fields:

| Field | Meaning |
|---|---|
| Name | Any label, for display only. |
| Domains | Comma-separated, e.g. `x.com, twitter.com`. Subdomains match too (`www.x.com`, `mobile.twitter.com`). |
| Rewrite domain to | Optional, e.g. `fxtwitter.com`. Empty = keep the domain. |
| Remove all parameters except those listed | Checked: **only** the parameters in the "keep" field remain. Empty field while checked = remove all parameters. Unchecked: parameters are not filtered (apart from the removals below). |
| Parameters to keep | Comma-separated, e.g. `v, t, list`. |
| Extra parameters to remove | Comma-separated. A trailing asterisk means a prefix, e.g. `utm_*`. |

Changes are saved to the configuration file immediately and take effect right away, no restart needed.

### 2. By hand in `config.json`

File: `%APPDATA%\LinkTrimmer\config.json`. After editing it by hand, **restart the app**. Example rule:

```json
{
  "Name": "My site",
  "Hosts": [ "example.com" ],
  "RewriteHost": "embed.example.com",
  "KeepParams": [ "id", "page" ],
  "RemoveParams": [ "ref*" ],
  "Enabled": true
}
```

Fields:

| Field | Type | Meaning |
|---|---|---|
| `Hosts` | list, required | Domains the rule applies to (every subdomain matches too). |
| `RewriteHost` | string or `null` | The domain to swap in. A rewrite also drops the `#...` fragment of the link. |
| `KeepParams` | list or `null` | `null` = don't filter parameters; **`[]` = remove all**; `["a","b"]` = keep only `a` and `b`. |
| `RemoveParams` | list | Extra parameters to remove; a trailing `*` means a prefix. |
| `Enabled` | `true` / `false` | A disabled rule is ignored. |

Put rules into the `Rules` array. Tracking parameters removed from **every** link (regardless of rules) are in `GlobalRemoveParams`.

### 3. In code (default rules)

The default rules live in `TrimmerConfig.DefaultRules()` (`src/LinkTrimmer.Core/TrimmerConfig.cs`). Add a new `SiteRule` there and **add a test** in `tests/LinkTrimmer.Tests/LinkProcessorTests.cs`.

> [!NOTE]
> Default rules are written to `config.json` only when that file **does not exist yet**. A user who already has the file will not get new rules from code. They have to add them manually or delete `config.json` (it will be recreated with the defaults).

### What rules can NOT do

A rule works on the **domain and query parameters**. If you need to change the link's **path** (e.g. strip `/ref=...` from Amazon URLs, turn `/shorts/ID` into `/watch?v=ID`), extend the logic in `LinkProcessor.Process` (`src/LinkTrimmer.Core/LinkProcessor.cs`) and add a new field to `SiteRule`.

---

## How rules are applied (exact logic)

All of the logic is in `LinkProcessor.Process(string)` and is independent of the UI and of Windows (which makes it easy to test):

1. The text is trimmed. If whitespace (space, newline) remains **inside**, it is plain text → do nothing.
2. It must be a valid absolute URL with the `http` or `https` scheme. Otherwise → do nothing.
3. The **first enabled** rule whose domain matches is chosen (the host equals the entry or ends with `.entry`). List order matters when domains overlap.
4. Query parameters are filtered in this order:
   1. if the rule has `KeepParams` (not `null`) → only the listed ones stay,
   2. `GlobalRemoveParams` are removed (for every link),
   3. the rule's `RemoveParams` are removed.
5. If the rule has `RewriteHost` → the host is swapped and the `#...` fragment is dropped.
6. If the result equals the input (after URI normalization) → `null`, so the clipboard is left untouched.

Parameter names are compared case-insensitively.

---

## Project structure

```text
src/
  LinkTrimmer.Core/            UI-free logic: rules, link processing, configuration
    LinkProcessor.cs             Clipboard text -> cleaned text (or null)
    SiteRule.cs                  Model of a single rule
    TrimmerConfig.cs             Configuration + default rules + JSON load/save
  LinkTrimmer.App/             WinForms app (runs in the background)
    Program.cs                   Startup, tray, menu, single instance
    ClipboardWatcher.cs          Listens for WM_CLIPBOARDUPDATE, calls LinkProcessor
    NativeClipboard.cs           Reads/writes clipboard text via the WinAPI
    Autostart.cs                 "Start with Windows" (HKCU\...\Run registry value)
    MainForm.cs                  Window with the rules table (add/edit/delete)
    RuleEditForm.cs              Rule edit dialog
    Loc.cs                       UI labels (pl/en/zh)
    AppIcon.cs, app.ico          The "LT" icon
tests/
  LinkTrimmer.Tests/           xUnit tests of the logic (LinkProcessorTests.cs)
tools/
  make-icon.ps1                Generates app.ico
```

The split into `Core` and `App` is deliberate: the link logic knows nothing about the UI or the clipboard, so it can be changed and tested without running the app.

---

## Building and publishing

### Prerequisites

- Windows 10 or 11 (the app uses WinForms and the Windows clipboard API, so it does not build or run on Linux/macOS).
- The **.NET SDK 9 or newer**. Check with:

  ```powershell
  dotnet --version
  ```

  If the command is not found or prints a version lower than 9, install the SDK from <https://dotnet.microsoft.com/download>. Visual Studio is **not** required; any terminal (PowerShell, cmd, Windows Terminal) is enough.

### Everyday commands

Run these from the repository root (the folder that contains `LinkTrimmer.sln`):

```powershell
dotnet build                               # compile everything (Debug)
dotnet test                                # run the logic tests
dotnet run --project src/LinkTrimmer.App   # build and start the app
```

Dependencies (only the test framework) are restored automatically on the first build; this needs internet access once.

### Building the .exe yourself

Pick one of the two variants. Both produce a **single file**, `dist\LinkTrimmer.exe`, that you can double-click, copy anywhere, and run without a terminal. No installer is needed.

**Variant A: small (about 200 KB).** Needs the [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) on the computer that runs it (it is already there if you installed the SDK).

```powershell
dotnet publish src/LinkTrimmer.App -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false `
  -p:SatelliteResourceLanguages=en -o dist
```

**Variant B: self-contained (about 100 MB).** Carries .NET inside, so it runs on any Windows 10/11 without installing anything.

```powershell
dotnet publish src/LinkTrimmer.App -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false `
  -p:SatelliteResourceLanguages=en -o dist
```

Notes:

- The backtick `` ` `` at the end of a line is PowerShell's line continuation. In cmd.exe use `^` instead, or put the whole command on one line.
- For an ARM64 PC, replace `win-x64` with `win-arm64`.
- `-o dist` sets the output folder. The `dist/` folder is ignored by git.
- Run `dist\LinkTrimmer.exe`. The first start creates `%APPDATA%\LinkTrimmer\config.json` with the default rules.
- To start it with Windows, run the `.exe` from its **final location** and tick "Start with Windows" (see [Window and tray icon](#window-and-tray-icon)).

### Troubleshooting

| Problem | Cause and fix |
|---|---|
| `dotnet` is not recognized | The .NET SDK is not installed or the terminal was opened before installing it. Install the SDK and open a new terminal. |
| Build error that mentions `net9.0` or an unsupported target | The SDK is older than 9. Install .NET SDK 9 or newer. |
| `build` or `publish` fails with a locked-file error (`MSB3027`, "file is being used by another process") | A previous instance is still running. Close it from the tray ("Exit") or end `LinkTrimmer.exe` in Task Manager, then build again. |
| Double-clicking the Variant A `.exe` shows a "you must install .NET" message | The .NET 9 Desktop Runtime is missing. Install it, or use Variant B. |
| Double-clicking the `.exe` does nothing visible | Another instance is already running (only one is allowed). Look for the "LT" icon in the tray, or the hidden-icons arrow next to the clock. |
| Windows SmartScreen warns about an unknown publisher | The `.exe` is not code-signed. That is expected for a locally built file; choose "More info" and then "Run anyway". |

---

## Data, configuration, language

- Everything (rules, language, global parameters) is in a single file: `%APPDATA%\LinkTrimmer\config.json`. There is no database.
- The file is created automatically on first run with the default rules.
- To reset to defaults: close the app and delete this file.
- A corrupted file (e.g. a typo in the JSON) will stop the app from starting. There is no automatic recovery yet.
- UI language: the `Language` field (`"pl"` / `"en"` / `"zh"`), detected from the system on first start. Change it in the window or from the tray menu. To add a language, add it to the `Languages` list and the dictionary in `src/LinkTrimmer.App/Loc.cs`. All UI texts live there under keys (`Loc.T("main.add")`), so they are not hard-coded in the window code.

---

## Security and privacy

**What the app does with your clipboard**

- It briefly reads **any text** you copy (including, e.g., a password copied from a password manager) in order to check whether it is a link. It then discards it. Nothing is logged, stored on disk, or sent anywhere.
- The app has **no network code at all**, starts no other processes, touches no file other than its own `config.json`, and has no third-party dependencies (only the .NET framework). The only thing it ever writes to the registry is the optional "Start with Windows" entry (see below), and only when you switch that on.
- It only ever *writes* to the clipboard when a link was actually changed. Text longer than 4096 characters, images and files are never read.

**Hardening in the code**

- Only `http`/`https` links are processed (`javascript:`, `file:`, `ftp:` and similar are ignored).
- Domain matching is exact or by real subdomain: `evilx.com`, `x.com.evil.com` and `https://x.com@evil.com/` do **not** match the `x.com` rule.
- A malformed link, or an invalid `RewriteHost` in the config (anything that is not a plain domain name), never crashes the app and never produces a redirect; the clipboard is simply left alone.
- Credentials in a link (`https://user:pass@host/`) are dropped when a link is rewritten.
- The clipboard is read only up to the actual size of the data block, so a program that puts un-terminated text in the clipboard cannot make the app read past it.
- If another program keeps undoing the app's change, the app gives up after 3 repeats within 3 seconds instead of fighting it forever.

**Things you should know**

- **`config.json` is trusted input.** Anyone (or any malware) that can write to `%APPDATA%\LinkTrimmer\config.json` can add a rule that rewrites links to a domain of their choosing. That requires the same permissions as running code as you, but be careful with config files from other people and only import rules you understand.
- **Embed services are third parties.** When you paste a link rewritten to `fxtwitter.com`, `kkinstagram.com` or `vxtiktok.com`, whoever opens it talks to those services, and they see the link. If such a domain is ever abandoned and taken over, links to it could become unsafe. Check the rewrite domains occasionally, or remove the rewrite from a rule if you do not trust the service.
- **Password managers.** Some of them mark copied secrets as "exclude from clipboard history". If a copied secret happens to be a single `http(s)` link that the app changes, that marking is lost for that entry. In practice this is rare, because passwords are rarely URLs.
- The executable is **not code-signed**, so Windows SmartScreen may warn about an unknown publisher. Build it yourself from source if you do not want to trust a downloaded binary.

---

## Limitations

- When the cleaned link is written back, the clipboard contains **plain text only**. Any other formats the source program put in the same entry (e.g. HTML) are lost. This applies only to clipboards holding a single link, and only when the link is actually changed.
- The embed domains (`fxtwitter.com`, `kkinstagram.com`, `vxtiktok.com`) are third-party services. They may disappear or change behavior; if so, change them in the rule.
- The cleaned link replaces the original only after a short moment (see above), and Windows clipboard history keeps both.
- The app is not code-signed, so SmartScreen may show an "unknown publisher" warning.
- The WinAPI clipboard handling (`NativeClipboard.cs`) has **no automated tests** (the tests cover only `LinkProcessor`). After changing that file, manually check copying a link, plain text, an image, and a file.
