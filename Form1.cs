using System;
using System.Drawing;
using System.IO;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using System.Reflection;

namespace RadEdit
{
    public partial class Form1 : Form
    {
        private enum CopyDataCommand : long
        {
            SetRtfText = 1,
            InsertRtfText = 2,
            SetRtfFile = 3,
            InsertRtfFile = 4,
            RequestTempFile = 5,
            TempFileResponse = 6,
            ErrorResponse = 7,
            SetTitle = 8,
            GetTitle = 9,
            TitleResponse = 10,
            SetName = 11,
            GetName = 12,
            NameResponse = 13,
            GotoEnd = 14,
            FixFont = 15,
            CleanUpEnd = 16,
            SetHtmlFile = 17,
            RequestHtmlFile = 18,
            HtmlFileResponse = 19,
            SetDataContext = 20,
            GetDataContext = 21,
            DataContextResponse = 22
        }

        private const int WM_COPYDATA = 0x004A;
        private const string HtmlRegionMessageType = "regionUpdate";
        private const string DataContextModeKey = "__mode";
        private const string DataContextReplaceMode = "replace";
        private const string DataContextDataKey = "data";
        private static readonly JsonSerializerOptions HtmlMessageJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };
        private static readonly object DebugLogLock = new();
        private static readonly string DebugLogPath = InitializeDebugLogPath();
        private const string HtmlRoutingScript = @"(() => {
    if (window.__radeditRoutingAttached) {
        return;
    }
    window.__radeditRoutingAttached = true;

    const send = (payload) => {
        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage(payload);
        }
    };

    const parseMap = (el) => {
        if (!el) return null;
        const raw = el.getAttribute('data-map');
        if (!raw) return null;
        try {
            const parsed = JSON.parse(raw);
            return parsed && typeof parsed === 'object' ? parsed : null;
        } catch (err) {
            return null;
        }
    };

    const getValue = (el) => {
        if (!el || !el.tagName) return '';
        const tag = el.tagName.toLowerCase();
        if (tag === 'select') {
            const opt = el.options[el.selectedIndex];
            if (!opt) return '';
            return opt.value || opt.text || '';
        }
        if (tag === 'textarea') {
            return el.value || '';
        }
        if (tag === 'input') {
            const type = (el.getAttribute('type') || 'text').toLowerCase();
            if (type === 'checkbox') {
                return el.checked ? (el.value || 'true') : '';
            }
            if (type === 'radio') {
                return el.checked ? (el.value || '') : '';
            }
            return el.value || '';
        }
        return '';
    };

    const resolveText = (el, value, map) => {
        if (!map) return value || '';
        if (Object.prototype.hasOwnProperty.call(map, value)) {
            return map[value];
        }
        if (typeof value === 'string') {
            const lower = value.toLowerCase();
            if (Object.prototype.hasOwnProperty.call(map, lower)) {
                return map[lower];
            }
        }
        if (el && el.tagName && el.tagName.toLowerCase() === 'input') {
            const type = (el.getAttribute('type') || 'text').toLowerCase();
            if (type === 'checkbox') {
                const stateKey = el.checked ? 'true' : 'false';
                if (Object.prototype.hasOwnProperty.call(map, stateKey)) {
                    return map[stateKey];
                }
            }
        }
        return value || '';
    };

    const handleEvent = (ev) => {
        const target = ev.target;
        if (!target || !target.closest) return;

        const regionEl = target.closest('[data-target-region]');
        if (!regionEl) return;

        const region = regionEl.getAttribute('data-target-region');
        if (!region) return;

        const control = regionEl.matches('input, select, textarea') ? regionEl : target;
        if (control.matches && control.matches('input[type=radio]') && !control.checked) {
            return;
        }

        const field = regionEl.getAttribute('data-field') || control.getAttribute('data-field') || '';
        const map = parseMap(regionEl) || parseMap(control);
        const value = getValue(control);
        const text = resolveText(control, value, map);

        send({
            type: 'regionUpdate',
            region: region,
            field: field,
            value: value,
            text: text
        });
    };

    document.addEventListener('change', handleEvent, true);
    document.addEventListener('input', handleEvent, true);
})();";

        private sealed class HtmlRegionUpdate
        {
            public string? Type { get; set; }
            public string? Region { get; set; }
            public string? Field { get; set; }
            public string? Value { get; set; }
            public string? Text { get; set; }
        }

        private static class RichEditNative
        {
            public const int WM_USER = 0x0400;
            public const int EM_SETCHARFORMAT = WM_USER + 68; // 0x0444
            public const int SCF_DEFAULT = 0x0000;
            public const int SCF_ALL = 0x0004;
            public const uint CFM_SIZE = 0x80000000;
            public const uint CFM_FACE = 0x20000000;
            public const uint CFM_CHARSET = 0x08000000;
            public const byte DEFAULT_CHARSET = 1;

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            public struct CHARFORMAT2W
            {
                public uint cbSize;
                public uint dwMask;
                public uint dwEffects;
                public int yHeight;
                public int yOffset;
                public int crTextColor;
                public byte bCharSet;
                public byte bPitchAndFamily;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
                public string szFaceName;
                public ushort wWeight;
                public ushort sSpacing;
                public int crBackColor;
                public int lcid;
                public int dwReserved;
                public short sStyle;
                public short wKerning;
                public byte bUnderlineType;
                public byte bAnimation;
                public byte bRevAuthor;
                public byte bReserved1;
            }

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern IntPtr SendMessage(
                IntPtr hWnd, int msg, IntPtr wParam, ref CHARFORMAT2W lParam);
        }

        private enum HtmlViewMode
        {
            Split,
            Pop,
            Full
        }

        private sealed class HtmlViewOptions
        {
            public HtmlViewMode Mode { get; set; } = HtmlViewMode.Split;
            public double? SplitPercent { get; set; }
            public int? MonitorIndex { get; set; }
            public int? X { get; set; }
            public int? Y { get; set; }
            public int? Width { get; set; }
            public int? Height { get; set; }
        }

        private const string HtmlHostName = "radedit.local";
        private const string HtmlLanguage = "fr-CA";
        private const int HtmlBarHeightFallback = 30;
        private const double DefaultHtmlSplitPercent = 0.5;
        private const int HtmlMetaSampleLimit = 65536;
        private Task? webView2Initialization;
        private string? htmlRootFolder;
        private bool isHtmlMode;
        private HtmlViewMode htmlViewMode = HtmlViewMode.Split;
        private double htmlSplitPercent = DefaultHtmlSplitPercent;
        private string lastPlainText = string.Empty;
        private readonly SemaphoreSlim htmlInsertGate = new(1, 1);
        private Form? detachedHtmlHost;
        private Form? detachedRtfHost;
        private bool isClosing;
        private string lastRtfSnapshot = string.Empty;
        private bool suppressRtfEvents;
        private bool allowRtfUpdatesWhileHtmlFocus;
        private readonly object dataContextLock = new();
        private JsonObject dataContext = new();

        public Form1()
        {
            InitializeComponent();
            Text = $"RadEdit V{GetAppVersion()}";
            SetDefaultTypingFont("Arial", 10f);
            richTextBox1.SelectionChanged += RichTextBox1_SelectionChanged;
            richTextBox1.TextChanged += RichTextBox1_TextChanged;
            splitContainer1.SizeChanged += SplitContainer1_SizeChanged;
            lastPlainText = richTextBox1.Text;
            lastRtfSnapshot = richTextBox1.Rtf ?? string.Empty;
            UpdateFormattingButtons();
            SetHtmlMode(false);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_COPYDATA)
            {
                IntPtr senderHandle = m.WParam;

                try
                {
                    var copyData = NativeMethods.GetCopyData(m.LParam);
                    var command = (CopyDataCommand)copyData.dwData.ToInt64();
                    string payload = NativeMethods.CopyDataToString(copyData);

                    bool handled = HandleCopyDataCommand(command, payload, senderHandle);
                    m.Result = handled ? new IntPtr(1) : IntPtr.Zero;
                }
                catch (Exception ex)
                {
                    NativeMethods.SendCopyData(senderHandle, CopyDataCommand.ErrorResponse, ex.Message);
                    m.Result = IntPtr.Zero;
                }

                return;
            }

            base.WndProc(ref m);
        }

        private bool HandleCopyDataCommand(CopyDataCommand command, string payload, IntPtr senderHandle)
        {
            switch (command)
            {
                case CopyDataCommand.SetRtfText:
                    return TrySetRtf(payload);
                case CopyDataCommand.InsertRtfText:
                    return TryInsertRtf(payload);
                case CopyDataCommand.SetRtfFile:
                    return TryApplyRtfFile(payload, replaceContent: true);
                case CopyDataCommand.InsertRtfFile:
                    return TryApplyRtfFile(payload, replaceContent: false);
                case CopyDataCommand.RequestTempFile:
                    return TrySendTempFilePath(senderHandle, payload);
                case CopyDataCommand.SetHtmlFile:
                    return TrySetHtmlFile(payload, senderHandle);
                case CopyDataCommand.RequestHtmlFile:
                    return TrySendHtmlFilePath(senderHandle, payload);
                case CopyDataCommand.SetTitle:
                    return TrySetTitle(payload);
                case CopyDataCommand.GetTitle:
                    return TrySendTitle(senderHandle);
                case CopyDataCommand.SetName:
                    return TrySetName(payload);
                case CopyDataCommand.TempFileResponse:
                case CopyDataCommand.HtmlFileResponse:
                case CopyDataCommand.TitleResponse:
                case CopyDataCommand.DataContextResponse:
                case CopyDataCommand.ErrorResponse:
                    // Responses are handled upstream by callers.
                    return true;
                case CopyDataCommand.GetName:
                    return TrySendName(senderHandle);
                case CopyDataCommand.SetDataContext:
                    return TrySetDataContext(payload);
                case CopyDataCommand.GetDataContext:
                    return TrySendDataContext(senderHandle, payload);
                case CopyDataCommand.GotoEnd:
                    return TryGotoEnd();
                case CopyDataCommand.FixFont:
                    return TryFixFontCommand(payload);
                case CopyDataCommand.CleanUpEnd:
                    return TryCleanUpEnd();
                default:
                    NativeMethods.SendCopyData(senderHandle, CopyDataCommand.ErrorResponse, "Unknown command.");
                    return false;
            }
        }

        private bool TrySetRtf(string? rtf)
        {
            SetHtmlMode(false);
            RunProgrammaticRtfUpdate(() =>
            {
                if (string.IsNullOrEmpty(rtf))
                {
                    richTextBox1.Clear();
                    return;
                }

                richTextBox1.Rtf = rtf;
            });
            return true;
        }

        private bool TryInsertRtf(string? rtf)
        {
            SetHtmlMode(false);
            if (string.IsNullOrEmpty(rtf))
            {
                return false;
            }

            RunProgrammaticRtfUpdate(() => richTextBox1.SelectedRtf = rtf);
            return true;
        }

        private bool TryApplyRtfFile(string? path, bool replaceContent)
        {
            SetHtmlMode(false);
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("RTF file not found.", fullPath);
            }

            if (replaceContent)
            {
                RunProgrammaticRtfUpdate(() => richTextBox1.LoadFile(fullPath, RichTextBoxStreamType.RichText));
            }
            else
            {
                using var buffer = new RichTextBox();
                buffer.LoadFile(fullPath, RichTextBoxStreamType.RichText);
                RunProgrammaticRtfUpdate(() => richTextBox1.SelectedRtf = buffer.Rtf);
            }

            return true;
        }

        private bool TrySendTempFilePath(IntPtr recipient, string? requestedPath)
        {
            if (recipient == IntPtr.Zero)
            {
                return false;
            }

            SetHtmlMode(false);
            string tempFilePath = CreateTempRtfFile(requestedPath);
            return NativeMethods.SendCopyData(recipient, CopyDataCommand.TempFileResponse, tempFilePath);
        }

        private string CreateTempRtfFile(string? requestedPath)
        {
            string? resolvedPath = null;
            string trimmedRequest = requestedPath?.Trim() ?? string.Empty;

            if (!string.IsNullOrEmpty(trimmedRequest))
            {
                resolvedPath = Path.IsPathRooted(trimmedRequest)
                    ? trimmedRequest
                    : Path.Combine(Path.GetTempPath(), trimmedRequest);
            }
            else
            {
                string title = toolStripTitleLabel.Text?.Trim() ?? string.Empty;
                string safeName = SanitizeFileName(title);
                resolvedPath = Path.Combine(Path.GetTempPath(), safeName + ".rtf");
            }

            if (!string.Equals(Path.GetExtension(resolvedPath), ".rtf", StringComparison.OrdinalIgnoreCase))
            {
                resolvedPath = Path.ChangeExtension(resolvedPath, ".rtf");
            }

            string? directory = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            richTextBox1.SaveFile(resolvedPath, RichTextBoxStreamType.RichText);
            return resolvedPath;
        }

        private bool TrySetHtmlFile(string? path, IntPtr senderHandle)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                NativeMethods.SendCopyData(senderHandle, CopyDataCommand.ErrorResponse, "HTML file path is required.");
                return false;
            }

            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                NativeMethods.SendCopyData(senderHandle, CopyDataCommand.ErrorResponse, "HTML file not found.");
                return false;
            }

            BeginInvoke(async () =>
            {
                try
                {
                    await LoadHtmlFileAsync(fullPath);
                }
                catch (Exception ex)
                {
                    NativeMethods.SendCopyData(senderHandle, CopyDataCommand.ErrorResponse, ex.Message);
                }
            });

            return true;
        }

        private bool TrySendHtmlFilePath(IntPtr recipient, string? requestedPath)
        {
            if (recipient == IntPtr.Zero)
            {
                return false;
            }

            BeginInvoke(async () =>
            {
                try
                {
                    if (!isHtmlMode)
                    {
                        string tempRtfPath = CreateTempRtfFile(requestedPath);
                        NativeMethods.SendCopyData(recipient, CopyDataCommand.TempFileResponse, tempRtfPath);
                        return;
                    }

                    string html = await ExportHtmlAsync();
                    string tempFilePath = CreateTempHtmlFile(requestedPath, html);
                    NativeMethods.SendCopyData(recipient, CopyDataCommand.HtmlFileResponse, tempFilePath);
                }
                catch (Exception ex)
                {
                    NativeMethods.SendCopyData(recipient, CopyDataCommand.ErrorResponse, ex.Message);
                }
            });

            return true;
        }

        private string CreateTempHtmlFile(string? requestedPath, string htmlContent)
        {
            string? resolvedPath = null;
            string trimmedRequest = requestedPath?.Trim() ?? string.Empty;

            if (!string.IsNullOrEmpty(trimmedRequest))
            {
                resolvedPath = Path.IsPathRooted(trimmedRequest)
                    ? trimmedRequest
                    : Path.Combine(Path.GetTempPath(), trimmedRequest);
            }
            else
            {
                string title = toolStripTitleLabel.Text?.Trim() ?? string.Empty;
                string safeName = SanitizeFileName(title);
                resolvedPath = Path.Combine(Path.GetTempPath(), safeName + ".html");
            }

            if (!string.Equals(Path.GetExtension(resolvedPath), ".html", StringComparison.OrdinalIgnoreCase))
            {
                resolvedPath = Path.ChangeExtension(resolvedPath, ".html");
            }

            string? directory = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(resolvedPath, htmlContent, Encoding.UTF8);
            return resolvedPath;
        }

        private async Task LoadHtmlFileAsync(string fullPath)
        {
            HtmlViewOptions? viewOptions = TryReadHtmlViewOptions(fullPath, out HtmlViewOptions? parsed)
                ? parsed
                : null;

            await EnsureWebView2InitializedAsync();

            string? folder = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(folder))
            {
                throw new InvalidOperationException("HTML file folder could not be resolved.");
            }

            ConfigureHtmlMapping(folder);
            ApplyHtmlViewOptions(viewOptions);

            string fileName = Path.GetFileName(fullPath);
            string url = $"https://{HtmlHostName}/{Uri.EscapeDataString(fileName)}";
            webView2.Source = new Uri(url);
            textBoxHtmlUrl.Text = fullPath;
            SetHtmlMode(true);
        }

        private void ConfigureHtmlMapping(string folder)
        {
            if (webView2.CoreWebView2 == null)
            {
                throw new InvalidOperationException("WebView2 is not initialized.");
            }

            if (!string.Equals(htmlRootFolder, folder, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(htmlRootFolder))
                {
                    webView2.CoreWebView2.ClearVirtualHostNameToFolderMapping(HtmlHostName);
                }

                webView2.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    HtmlHostName,
                    folder,
                    CoreWebView2HostResourceAccessKind.Allow);
                htmlRootFolder = folder;
            }
        }

        private async Task EnsureWebView2InitializedAsync()
        {
            if (webView2Initialization == null)
            {
                webView2Initialization = InitializeWebView2Async();
            }

            await webView2Initialization;
        }

        private async Task InitializeWebView2Async()
        {
            var options = new CoreWebView2EnvironmentOptions
            {
                Language = HtmlLanguage
            };

            var environment = await CoreWebView2Environment.CreateAsync(null, null, options);
            await webView2.EnsureCoreWebView2Async(environment);
            if (webView2.CoreWebView2 != null)
            {
                webView2.CoreWebView2.Settings.IsWebMessageEnabled = true;
                webView2.CoreWebView2.WebMessageReceived += WebView2_WebMessageReceived;
                webView2.CoreWebView2.HistoryChanged += WebView2_HistoryChanged;
                webView2.CoreWebView2.NavigationCompleted += WebView2_NavigationCompleted;
                await webView2.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(HtmlRoutingScript);
                LogDebug("WebView2 initialized. Routing script injected.");
            }
        }

        private async Task<string> ExportHtmlAsync()
        {
            await EnsureWebView2InitializedAsync();

            if (webView2.CoreWebView2 == null)
            {
                throw new InvalidOperationException("WebView2 is not initialized.");
            }

            const string script = @"(() => {
    const inputs = document.querySelectorAll('input');
    inputs.forEach((input) => {
        const type = (input.getAttribute('type') || '').toLowerCase();
        if (type === 'checkbox' || type === 'radio') {
            if (input.checked) {
                input.setAttribute('checked', '');
            } else {
                input.removeAttribute('checked');
            }
        } else {
            input.setAttribute('value', input.value ?? '');
        }
    });

    document.querySelectorAll('textarea').forEach((area) => {
        area.textContent = area.value ?? '';
    });

    document.querySelectorAll('select').forEach((select) => {
        Array.from(select.options).forEach((option) => {
            if (option.selected) {
                option.setAttribute('selected', '');
            } else {
                option.removeAttribute('selected');
            }
        });
    });

    const doctype = document.doctype ? `<!DOCTYPE ${document.doctype.name}>` : '<!DOCTYPE html>';
    return doctype + '\n' + document.documentElement.outerHTML;
})();";

            string json = await webView2.ExecuteScriptAsync(script);
            string? html = JsonSerializer.Deserialize<string>(json);
            if (string.IsNullOrEmpty(html))
            {
                throw new InvalidOperationException("HTML export returned no content.");
            }

            return html;
        }

        private void WebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            LogDebug("Web message received: " + e.WebMessageAsJson);
            HtmlRegionUpdate? message;
            try
            {
                message = JsonSerializer.Deserialize<HtmlRegionUpdate>(e.WebMessageAsJson, HtmlMessageJsonOptions);
            }
            catch (JsonException)
            {
                LogDebug("Web message JSON failed to parse.");
                return;
            }

            if (message == null ||
                !string.Equals(message.Type, HtmlRegionMessageType, StringComparison.OrdinalIgnoreCase))
            {
                LogDebug("Web message ignored. Type=" + (message?.Type ?? "<null>"));
                return;
            }

            string region = message.Region?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(region))
            {
                LogDebug("Web message missing region.");
                return;
            }

            string text = message.Text ?? string.Empty;
            LogDebug("Routing update. Region=" + region + " TextLength=" + text.Length);
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => TryUpdateRtfRegion(region, text)));
                return;
            }

            TryUpdateRtfRegion(region, text);
        }

        private void WebView2_HistoryChanged(object? sender, object e)
        {
            UpdateHtmlNavButtons();
        }

        private void WebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            UpdateHtmlNavButtons();
        }

        private void SetHtmlMode(bool enable)
        {
            if (isHtmlMode != enable)
            {
                isHtmlMode = enable;
                lastPlainText = richTextBox1.Text;

                toolStripBoldButton.Enabled = !enable;
                toolStripItalicButton.Enabled = !enable;
                toolStripUnderlineButton.Enabled = !enable;
            }

            webView2.Visible = enable;
            UpdateHtmlPanelLayout();
            UpdateHtmlNavButtons();
        }

        private void SplitContainer1_SizeChanged(object? sender, EventArgs e)
        {
            if (!isHtmlMode || IsControlDetached(webView2, detachedHtmlHost))
            {
                UpdateHtmlPanelLayout();
            }
        }

        private void UpdateHtmlPanelLayout()
        {
            if (splitContainer1.Height <= 0)
            {
                return;
            }

            int barHeight = panelHtmlBar.Height > 0 ? panelHtmlBar.Height : HtmlBarHeightFallback;
            splitContainer1.Panel2MinSize = barHeight;

            bool showDockedHtml = isHtmlMode && !IsControlDetached(webView2, detachedHtmlHost);
            int desiredPanel2Height = barHeight;
            if (showDockedHtml)
            {
                double percent = Math.Clamp(htmlSplitPercent, 0, 1);
                int targetHeight = (int)Math.Round(splitContainer1.Height * percent);
                desiredPanel2Height = Math.Max(barHeight, targetHeight);
            }
            SetPanel2Height(desiredPanel2Height);
        }

        private void ResetHtmlViewOptions()
        {
            htmlViewMode = HtmlViewMode.Split;
            htmlSplitPercent = DefaultHtmlSplitPercent;
        }

        private void ApplyHtmlViewOptions(HtmlViewOptions? options)
        {
            ResetHtmlViewOptions();

            if (options != null)
            {
                htmlViewMode = options.Mode;
                if (options.SplitPercent.HasValue)
                {
                    htmlSplitPercent = Math.Clamp(options.SplitPercent.Value, 0, 1);
                }
            }

            switch (htmlViewMode)
            {
                case HtmlViewMode.Split:
                    if (IsControlDetached(webView2, detachedHtmlHost))
                    {
                        DockWebView2();
                    }
                    break;
                case HtmlViewMode.Pop:
                    EnsureHtmlDetached();
                    if (options != null)
                    {
                        ApplyDetachedBounds(options);
                    }
                    break;
                case HtmlViewMode.Full:
                    EnsureHtmlDetached();
                    ApplyFullScreenBounds(options);
                    break;
            }
        }

        private void EnsureHtmlDetached()
        {
            if (!IsControlDetached(webView2, detachedHtmlHost))
            {
                DetachWebView2();
            }
        }

        private void ApplyDetachedBounds(HtmlViewOptions options)
        {
            if (detachedHtmlHost == null)
            {
                return;
            }

            Screen screen = ResolveTargetScreen(options.MonitorIndex);
            Rectangle workingArea = screen.WorkingArea;

            int width = options.Width ?? Math.Min(workingArea.Width, 900);
            int height = options.Height ?? Math.Min(workingArea.Height, 700);
            width = Math.Clamp(width, 200, workingArea.Width);
            height = Math.Clamp(height, 200, workingArea.Height);

            int x = options.X ?? workingArea.Left + (workingArea.Width - width) / 2;
            int y = options.Y ?? workingArea.Top + (workingArea.Height - height) / 2;

            Rectangle desired = new Rectangle(x, y, width, height);
            Rectangle bounded = ConstrainBounds(desired, workingArea);

            detachedHtmlHost.StartPosition = FormStartPosition.Manual;
            detachedHtmlHost.WindowState = FormWindowState.Normal;
            detachedHtmlHost.Bounds = bounded;
        }

        private void ApplyFullScreenBounds(HtmlViewOptions? options)
        {
            if (detachedHtmlHost == null)
            {
                return;
            }

            Screen screen = ResolveTargetScreen(options?.MonitorIndex);
            detachedHtmlHost.StartPosition = FormStartPosition.Manual;
            detachedHtmlHost.WindowState = FormWindowState.Normal;
            detachedHtmlHost.Bounds = screen.Bounds;
        }

        private static Screen ResolveTargetScreen(int? monitorIndex)
        {
            Screen[] screens = Screen.AllScreens;
            if (monitorIndex.HasValue)
            {
                int index = monitorIndex.Value;
                if (index >= 0 && index < screens.Length)
                {
                    return screens[index];
                }
            }

            return Screen.PrimaryScreen ?? screens[0];
        }

        private static Rectangle ConstrainBounds(Rectangle desired, Rectangle container)
        {
            int x = Math.Clamp(desired.X, container.Left, container.Right - desired.Width);
            int y = Math.Clamp(desired.Y, container.Top, container.Bottom - desired.Height);
            return new Rectangle(x, y, desired.Width, desired.Height);
        }

        private static bool TryReadHtmlViewOptions(string fullPath, out HtmlViewOptions? options)
        {
            options = null;

            string sample = ReadFileSample(fullPath, HtmlMetaSampleLimit);
            if (string.IsNullOrEmpty(sample))
            {
                return false;
            }

            string? content = ExtractMetaContent(sample, "radedit:view");
            if (string.IsNullOrWhiteSpace(content))
            {
                return false;
            }

            return TryParseHtmlViewOptions(content, out options);
        }

        private static string ReadFileSample(string fullPath, int limit)
        {
            using var stream = File.OpenRead(fullPath);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            char[] buffer = new char[limit];
            int read = reader.Read(buffer, 0, buffer.Length);
            return read > 0 ? new string(buffer, 0, read) : string.Empty;
        }

        private static string? ExtractMetaContent(string html, string metaName)
        {
            var metaRegex = new Regex("<meta\\s+[^>]*>", RegexOptions.IgnoreCase);
            var nameRegex = new Regex("name\\s*=\\s*[\"'](?<name>[^\"']+)[\"']", RegexOptions.IgnoreCase);
            var contentRegex = new Regex("content\\s*=\\s*[\"'](?<content>[^\"']*)[\"']", RegexOptions.IgnoreCase);

            foreach (Match match in metaRegex.Matches(html))
            {
                string tag = match.Value;
                Match nameMatch = nameRegex.Match(tag);
                if (!nameMatch.Success)
                {
                    continue;
                }

                string name = nameMatch.Groups["name"].Value.Trim();
                if (!string.Equals(name, metaName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Match contentMatch = contentRegex.Match(tag);
                if (!contentMatch.Success)
                {
                    continue;
                }

                return contentMatch.Groups["content"].Value;
            }

            return null;
        }

        private static bool TryParseHtmlViewOptions(string content, out HtmlViewOptions? options)
        {
            options = null;
            bool hasValue = false;
            var parsed = new HtmlViewOptions();

            string[] pairs = content.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawPair in pairs)
            {
                string pair = rawPair.Trim();
                if (pair.Length == 0)
                {
                    continue;
                }

                string[] parts = pair.Split(new[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                {
                    continue;
                }

                string key = parts[0].Trim().ToLowerInvariant();
                string value = parts.Length > 1 ? parts[1].Trim() : string.Empty;

                switch (key)
                {
                    case "mode":
                        if (TryParseMode(value, out HtmlViewMode mode))
                        {
                            parsed.Mode = mode;
                            hasValue = true;
                        }
                        break;
                    case "size":
                        if (TryParsePercent(value, out double percent))
                        {
                            parsed.SplitPercent = percent;
                            hasValue = true;
                        }
                        break;
                    case "monitor":
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int monitor) &&
                            monitor > 0)
                        {
                            parsed.MonitorIndex = monitor - 1;
                            hasValue = true;
                        }
                        break;
                    case "x":
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int x))
                        {
                            parsed.X = x;
                            hasValue = true;
                        }
                        break;
                    case "y":
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
                        {
                            parsed.Y = y;
                            hasValue = true;
                        }
                        break;
                    case "width":
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int width))
                        {
                            parsed.Width = width;
                            hasValue = true;
                        }
                        break;
                    case "height":
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int height))
                        {
                            parsed.Height = height;
                            hasValue = true;
                        }
                        break;
                }
            }

            if (!hasValue)
            {
                return false;
            }

            options = parsed;
            return true;
        }

        private static bool TryParseMode(string value, out HtmlViewMode mode)
        {
            switch (value.Trim().ToLowerInvariant())
            {
                case "split":
                case "docked":
                    mode = HtmlViewMode.Split;
                    return true;
                case "pop":
                case "popup":
                    mode = HtmlViewMode.Pop;
                    return true;
                case "full":
                case "fullscreen":
                    mode = HtmlViewMode.Full;
                    return true;
                default:
                    mode = HtmlViewMode.Split;
                    return false;
            }
        }

        private static bool TryParsePercent(string value, out double percent)
        {
            percent = 0;
            string cleaned = value.Trim().TrimEnd('%');
            if (!double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out double raw))
            {
                return false;
            }

            if (raw > 1)
            {
                raw /= 100d;
            }

            percent = Math.Clamp(raw, 0, 1);
            return true;
        }

        private void UpdateHtmlNavButtons()
        {
            bool canGoBack = false;
            bool canGoForward = false;

            if (isHtmlMode && webView2.CoreWebView2 != null)
            {
                canGoBack = webView2.CoreWebView2.CanGoBack;
                canGoForward = webView2.CoreWebView2.CanGoForward;
            }

            buttonHtmlBack.Enabled = canGoBack;
            buttonHtmlForward.Enabled = canGoForward;
        }

        private void SetPanel2Height(int panel2Height)
        {
            int totalHeight = splitContainer1.Height;
            if (totalHeight <= 0)
            {
                return;
            }

            int minPanel2 = Math.Max(splitContainer1.Panel2MinSize, 0);
            panel2Height = Math.Max(panel2Height, minPanel2);

            int splitterWidth = splitContainer1.SplitterWidth;
            int desiredPanel1Height = totalHeight - panel2Height - splitterWidth;
            if (desiredPanel1Height < splitContainer1.Panel1MinSize)
            {
                desiredPanel1Height = splitContainer1.Panel1MinSize;
            }

            if (desiredPanel1Height < 0 || desiredPanel1Height > totalHeight - splitterWidth)
            {
                return;
            }

            splitContainer1.SplitterDistance = desiredPanel1Height;
        }

        private async void ButtonHtmlGo_Click(object? sender, EventArgs e)
        {
            await LoadHtmlFromInputAsync();
        }

        private void ButtonHtmlClear_Click(object? sender, EventArgs e)
        {
            ClearHtmlView();
        }

        private void ButtonHtmlBack_Click(object? sender, EventArgs e)
        {
            if (webView2.CoreWebView2?.CanGoBack == true)
            {
                webView2.CoreWebView2.GoBack();
            }
        }

        private void ButtonHtmlForward_Click(object? sender, EventArgs e)
        {
            if (webView2.CoreWebView2?.CanGoForward == true)
            {
                webView2.CoreWebView2.GoForward();
            }
        }

        private void ClearHtmlView()
        {
            textBoxHtmlUrl.Text = string.Empty;

            if (IsControlDetached(webView2, detachedHtmlHost))
            {
                DockWebView2();
            }

            ResetWebView2();
            ResetHtmlViewOptions();
            SetHtmlMode(false);
        }

        private void ResetWebView2()
        {
            var previous = webView2;
            if (previous != null)
            {
                try
                {
                    if (previous.CoreWebView2 != null)
                    {
                        previous.CoreWebView2.HistoryChanged -= WebView2_HistoryChanged;
                        previous.CoreWebView2.NavigationCompleted -= WebView2_NavigationCompleted;
                        previous.CoreWebView2.WebMessageReceived -= WebView2_WebMessageReceived;
                    }
                }
                catch
                {
                    // Best-effort cleanup only.
                }

                if (previous.Parent != null)
                {
                    previous.Parent.Controls.Remove(previous);
                }

                previous.Dispose();
            }

            webView2 = new Microsoft.Web.WebView2.WinForms.WebView2
            {
                AllowExternalDrop = true,
                CreationProperties = null,
                DefaultBackgroundColor = Color.White,
                Dock = DockStyle.Fill,
                Location = new Point(0, 0),
                Name = "webView2",
                TabIndex = 1,
                ZoomFactor = 1D
            };

            panelHtmlHost.Controls.Add(webView2);
            webView2Initialization = null;
            htmlRootFolder = null;
            UpdateHtmlNavButtons();
        }

        private async void ButtonHtmlBrowse_Click(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "HTML files (*.html;*.htm)|*.html;*.htm|All files (*.*)|*.*",
                Title = "Open HTML File"
            };

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            textBoxHtmlUrl.Text = dialog.FileName;
            await LoadHtmlFileAsync(dialog.FileName);
        }

        private async void TextBoxHtmlUrl_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Enter)
            {
                return;
            }

            e.SuppressKeyPress = true;
            await LoadHtmlFromInputAsync();
        }

        private async Task LoadHtmlFromInputAsync()
        {
            string input = textBoxHtmlUrl.Text.Trim();
            if (string.IsNullOrEmpty(input))
            {
                return;
            }

            string? localPath = TryGetLocalHtmlPath(input);
            if (!string.IsNullOrEmpty(localPath))
            {
                await LoadHtmlFileAsync(localPath);
                return;
            }

            if (!TryNormalizeUrl(input, out Uri? uri) || uri == null)
            {
                return;
            }

            textBoxHtmlUrl.Text = uri.ToString();
            await NavigateToUrlAsync(uri);
        }

        private static string? TryGetLocalHtmlPath(string input)
        {
            if (Uri.TryCreate(input, UriKind.Absolute, out Uri? uri) && uri.IsFile)
            {
                return File.Exists(uri.LocalPath) ? uri.LocalPath : null;
            }

            try
            {
                string fullPath = Path.GetFullPath(input);
                return File.Exists(fullPath) ? fullPath : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool TryNormalizeUrl(string input, out Uri? uri)
        {
            if (Uri.TryCreate(input, UriKind.Absolute, out uri))
            {
                return true;
            }

            return Uri.TryCreate("https://" + input, UriKind.Absolute, out uri);
        }

        private async Task NavigateToUrlAsync(Uri uri)
        {
            await EnsureWebView2InitializedAsync();

            if (!string.Equals(uri.Host, HtmlHostName, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(htmlRootFolder) &&
                webView2.CoreWebView2 != null)
            {
                webView2.CoreWebView2.ClearVirtualHostNameToFolderMapping(HtmlHostName);
                htmlRootFolder = null;
            }

            webView2.Source = uri;
            SetHtmlMode(true);
        }

        private async void RichTextBox1_TextChanged(object? sender, EventArgs e)
        {
            if (suppressRtfEvents)
            {
                return;
            }

            string newText = richTextBox1.Text;

            if (allowRtfUpdatesWhileHtmlFocus || !IsHtmlMirroringAvailable())
            {
                lastPlainText = newText;
                lastRtfSnapshot = richTextBox1.Rtf ?? string.Empty;
                return;
            }

            string inserted = ExtractInsertedText(lastPlainText, newText);

            if (string.IsNullOrEmpty(inserted))
            {
                return;
            }

            try
            {
                await SendTextToHtmlAsync(inserted);
            }
            catch
            {
                // Swallow to avoid disrupting the UI if the WebView is not ready.
            }

            suppressRtfEvents = true;
            try
            {
                if (!string.Equals(richTextBox1.Rtf, lastRtfSnapshot, StringComparison.Ordinal))
                {
                    richTextBox1.Rtf = lastRtfSnapshot;
                }
            }
            finally
            {
                suppressRtfEvents = false;
            }
        }

        private static string ExtractInsertedText(string oldText, string newText)
        {
            if (string.IsNullOrEmpty(oldText))
            {
                return newText;
            }

            if (string.IsNullOrEmpty(newText))
            {
                return string.Empty;
            }

            int prefix = 0;
            int maxPrefix = Math.Min(oldText.Length, newText.Length);
            while (prefix < maxPrefix && oldText[prefix] == newText[prefix])
            {
                prefix++;
            }

            int suffix = 0;
            int maxSuffix = Math.Min(oldText.Length - prefix, newText.Length - prefix);
            while (suffix < maxSuffix &&
                   oldText[oldText.Length - 1 - suffix] == newText[newText.Length - 1 - suffix])
            {
                suffix++;
            }

            int insertedLength = newText.Length - prefix - suffix;
            if (insertedLength <= 0)
            {
                return string.Empty;
            }

            return newText.Substring(prefix, insertedLength);
        }

        private async Task SendTextToHtmlAsync(string text)
        {
            if (string.IsNullOrEmpty(text) || !IsHtmlMirroringAvailable())
            {
                return;
            }

            await EnsureWebView2InitializedAsync();
            if (webView2.CoreWebView2 == null)
            {
                return;
            }

            await htmlInsertGate.WaitAsync();
            try
            {
                string textJson = JsonSerializer.Serialize(text);
                string script = $@"(() => {{
    const text = {textJson};
    const el = document.activeElement;
    if (!el) return;
    if (el.isContentEditable) {{
        const sel = window.getSelection();
        if (!sel || sel.rangeCount === 0) return;
        sel.deleteFromDocument();
        const range = sel.getRangeAt(0);
        range.insertNode(document.createTextNode(text));
        range.collapse(false);
        sel.removeAllRanges();
        sel.addRange(range);
        el.dispatchEvent(new Event('input', {{ bubbles: true }}));
        return;
    }}
    const tag = (el.tagName || '').toLowerCase();
    if (tag !== 'input' && tag !== 'textarea') return;
    if (tag === 'input') {{
        const type = (el.getAttribute('type') || 'text').toLowerCase();
        const allowed = new Set(['text','search','email','url','tel','password','number','date','datetime-local','month','time','week']);
        if (!allowed.has(type)) return;
    }}
    const start = typeof el.selectionStart === 'number' ? el.selectionStart : el.value.length;
    const end = typeof el.selectionEnd === 'number' ? el.selectionEnd : el.value.length;
    const value = el.value || '';
    el.value = value.slice(0, start) + text + value.slice(end);
    const pos = start + text.length;
    if (el.setSelectionRange) el.setSelectionRange(pos, pos);
    el.dispatchEvent(new Event('input', {{ bubbles: true }}));
}})();";

                await webView2.ExecuteScriptAsync(script);
            }
            finally
            {
                htmlInsertGate.Release();
            }
        }

        private bool IsHtmlMirroringAvailable()
        {
            return webView2.Source != null && webView2.ContainsFocus;
        }

        private void RunProgrammaticRtfUpdate(Action action)
        {
            bool previous = allowRtfUpdatesWhileHtmlFocus;
            allowRtfUpdatesWhileHtmlFocus = true;
            try
            {
                action();
            }
            finally
            {
                allowRtfUpdatesWhileHtmlFocus = previous;
            }
        }

        private bool TrySetTitle(string? title)
        {
            string sanitized = title?.Trim() ?? string.Empty;
            toolStripTitleLabel.Text = sanitized;
            return true;
        }

        private bool TrySetName(string? name)
        {
            string sanitized = name?.Trim() ?? string.Empty;
            toolStripNameLabel.Text = sanitized;
            return true;
        }

        private bool TrySendTitle(IntPtr recipient)
        {
            if (recipient == IntPtr.Zero)
            {
                return false;
            }

            string title = toolStripTitleLabel.Text ?? string.Empty;
            return NativeMethods.SendCopyData(recipient, CopyDataCommand.TitleResponse, title);
        }

        private bool TrySendName(IntPtr recipient)
        {
            if (recipient == IntPtr.Zero)
            {
                return false;
            }

            string name = toolStripNameLabel.Text ?? string.Empty;
            return NativeMethods.SendCopyData(recipient, CopyDataCommand.NameResponse, name);
        }

        private bool TrySetDataContext(string? payload)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                lock (dataContextLock)
                {
                    dataContext = new JsonObject();
                }

                return true;
            }

            JsonNode? node = JsonNode.Parse(payload);
            if (node is not JsonObject obj)
            {
                throw new ArgumentException("SetDataContext payload must be a JSON object.");
            }

            if (TryReplaceDataContext(obj))
            {
                return true;
            }

            lock (dataContextLock)
            {
                foreach (var kvp in obj)
                {
                    dataContext[kvp.Key] = kvp.Value?.DeepClone();
                }
            }

            return true;
        }

        private bool TryReplaceDataContext(JsonObject obj)
        {
            if (!obj.TryGetPropertyValue(DataContextModeKey, out JsonNode? modeNode))
            {
                return false;
            }

            if (modeNode is not JsonValue modeValue || !modeValue.TryGetValue(out string? mode))
            {
                throw new ArgumentException("SetDataContext __mode must be a string.");
            }

            if (!string.Equals(mode, DataContextReplaceMode, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"SetDataContext __mode '{mode}' is not supported.");
            }

            if (!obj.TryGetPropertyValue(DataContextDataKey, out JsonNode? dataNode) || dataNode is not JsonObject dataObj)
            {
                throw new ArgumentException("SetDataContext replace payload must include a data object.");
            }

            lock (dataContextLock)
            {
                dataContext = (JsonObject)dataObj.DeepClone();
            }

            return true;
        }

        private bool TrySendDataContext(IntPtr recipient, string? payload)
        {
            if (recipient == IntPtr.Zero)
            {
                return false;
            }

            string response;
            lock (dataContextLock)
            {
                if (string.IsNullOrWhiteSpace(payload))
                {
                    response = dataContext.ToJsonString();
                }
                else
                {
                    string keyPath = payload.Trim();
                    JsonNode? node = dataContext;
                    foreach (var part in keyPath.Split('.', StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (node is JsonObject obj && obj.TryGetPropertyValue(part, out JsonNode? next))
                        {
                            node = next;
                        }
                        else
                        {
                            node = null;
                            break;
                        }
                    }

                    response = node?.ToJsonString() ?? "null";
                }
            }

            return NativeMethods.SendCopyData(recipient, CopyDataCommand.DataContextResponse, response);
        }

        private bool TryGotoEnd()
        {
            SetHtmlMode(false);
            string text = richTextBox1.Text;
            int endPos = text.TrimEnd('\r', '\n', ' ', '\t').Length;
            richTextBox1.SelectionStart = endPos;
            richTextBox1.ScrollToCaret();
            richTextBox1.Focus();
            return true;
        }

        private void SetDefaultTypingFont(string face = "Arial", float sizePt = 10f)
        {
            var cf = new RichEditNative.CHARFORMAT2W
            {
                cbSize = (uint)Marshal.SizeOf<RichEditNative.CHARFORMAT2W>(),
                dwMask = RichEditNative.CFM_FACE | RichEditNative.CFM_SIZE | RichEditNative.CFM_CHARSET,
                szFaceName = string.IsNullOrWhiteSpace(face) ? "Arial" : face,
                yHeight = (int)Math.Round(sizePt * 20f),
                bCharSet = RichEditNative.DEFAULT_CHARSET
            };

            RichEditNative.SendMessage(
                richTextBox1.Handle,
                RichEditNative.EM_SETCHARFORMAT,
                (IntPtr)RichEditNative.SCF_DEFAULT,
                ref cf
            );

            richTextBox1.Font = new Font(face, sizePt, FontStyle.Regular, GraphicsUnit.Point);
        }

        private bool TryFixFontCommand(string? payload)
        {
            SetHtmlMode(false);

            string face = "Arial";
            float size = 10f;

            if (!string.IsNullOrWhiteSpace(payload))
            {
                var p = payload.Trim();
                var parts = p.Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 1)
                {
                    if (float.TryParse(
                            parts[0].Trim().Replace(',', '.'),
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out float s))
                    {
                        size = s;
                    }
                    else
                    {
                        face = parts[0].Trim();
                    }
                }
                else
                {
                    face = string.IsNullOrWhiteSpace(parts[0]) ? "Arial" : parts[0].Trim();
                    if (float.TryParse(
                            parts[1].Trim().Replace(',', '.'),
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out float s))
                    {
                        size = s;
                    }
                }
            }

            RunProgrammaticRtfUpdate(() =>
            {
                var cf = new RichEditNative.CHARFORMAT2W
                {
                    cbSize = (uint)Marshal.SizeOf<RichEditNative.CHARFORMAT2W>(),
                    dwMask = RichEditNative.CFM_FACE | RichEditNative.CFM_SIZE | RichEditNative.CFM_CHARSET,
                    szFaceName = string.IsNullOrWhiteSpace(face) ? "Arial" : face,
                    yHeight = (int)Math.Round(size * 20f),
                    bCharSet = RichEditNative.DEFAULT_CHARSET
                };

                int selStart = richTextBox1.SelectionStart;
                int selLen = richTextBox1.SelectionLength;

                RichEditNative.SendMessage(
                    richTextBox1.Handle,
                    RichEditNative.EM_SETCHARFORMAT,
                    (IntPtr)RichEditNative.SCF_ALL,
                    ref cf
                );

                try
                {
                    richTextBox1.Select(selStart, selLen);
                    richTextBox1.ScrollToCaret();
                }
                catch
                {
                    // Selection restore is best-effort.
                }
            });

            return true;
        }

        private bool TryCleanUpEnd()
        {
            SetHtmlMode(false);
            string txt = richTextBox1.Text;
            if (string.IsNullOrEmpty(txt))
            {
                return true;
            }

            int newLen = TrimmedLength(txt);
            if (newLen < txt.Length)
            {
                int toRemove = txt.Length - newLen;
                RunProgrammaticRtfUpdate(() =>
                {
                    richTextBox1.SelectionStart = newLen;
                    richTextBox1.SelectionLength = toRemove;
                    richTextBox1.SelectedText = string.Empty;
                    richTextBox1.SelectionStart = newLen;
                    richTextBox1.SelectionLength = 0;
                    richTextBox1.ScrollToCaret();
                });
            }

            return true;
        }

        private static int TrimmedLength(string s)
        {
            int i = s.Length;
            while (i > 0)
            {
                char c = s[i - 1];
                if (c == ' ' || c == '\t' || c == '\r' || c == '\n' || c == '\u00A0')
                {
                    i--;
                    continue;
                }
                break;
            }
            return i;
        }

        private bool TryUpdateRtfRegion(string regionName, string? replacementText)
        {
            if (string.IsNullOrWhiteSpace(regionName))
            {
                LogDebug("TryUpdateRtfRegion skipped: empty region.");
                return false;
            }

            if (TryUpdateRegionByInlineRtf(regionName, replacementText))
            {
                return true;
            }

            if (TryUpdateRegionByText(regionName, replacementText))
            {
                return true;
            }

            string rtf = richTextBox1.Rtf ?? string.Empty;
            string trimmedRegion = regionName.Trim();
            string beginMarker = "[[BEGIN:" + trimmedRegion + "]]";
            string endMarker = "[[END:" + trimmedRegion + "]]";
            string replacementRtf = EscapeRtfText(replacementText ?? string.Empty);

            int searchIndex = 0;
            bool replacedAny = false;
            var builder = new StringBuilder(rtf.Length + replacementRtf.Length);

            while (true)
            {
                int beginMarkerIndex = rtf.IndexOf(beginMarker, searchIndex, StringComparison.Ordinal);
                if (beginMarkerIndex < 0)
                {
                    break;
                }

                int endMarkerIndex = rtf.IndexOf(endMarker, beginMarkerIndex + beginMarker.Length, StringComparison.Ordinal);
                if (endMarkerIndex < 0)
                {
                    break;
                }

                if (!TryFindGroupBounds(rtf, beginMarkerIndex, out int beginGroupStart, out int beginGroupEnd) ||
                    !TryFindGroupBounds(rtf, endMarkerIndex, out int endGroupStart, out int endGroupEnd))
                {
                    LogDebug("Failed to resolve group bounds for region: " + trimmedRegion);
                    break;
                }

                int replaceStart = beginGroupEnd + 1;
                int replaceEnd = endGroupStart;
                if (replaceStart > replaceEnd)
                {
                    LogDebug("Invalid region span for: " + trimmedRegion);
                    break;
                }

                builder.Append(rtf, searchIndex, replaceStart - searchIndex);
                builder.Append(replacementRtf);
                searchIndex = replaceEnd;
                replacedAny = true;
            }

            if (!replacedAny)
            {
                bool beginExists = rtf.IndexOf(beginMarker, StringComparison.Ordinal) >= 0;
                bool endExists = rtf.IndexOf(endMarker, StringComparison.Ordinal) >= 0;
                LogDebug("Region markers not found for: " + trimmedRegion +
                         " Begin=" + beginExists + " End=" + endExists);
                return false;
            }

            builder.Append(rtf, searchIndex, rtf.Length - searchIndex);
            string updatedRtf = builder.ToString();

            RunProgrammaticRtfUpdate(() => richTextBox1.Rtf = updatedRtf);
            LogDebug("Region updated: " + trimmedRegion);
            return true;
        }

        private bool TryUpdateRegionByInlineRtf(string regionName, string? replacementText)
        {
            string trimmedRegion = regionName.Trim();
            string beginMarker = "[[BEGIN:" + trimmedRegion + "]]";
            string endMarker = "[[END:" + trimmedRegion + "]]";
            string rtf = richTextBox1.Rtf ?? string.Empty;

            int beginIndex = rtf.IndexOf(beginMarker, StringComparison.Ordinal);
            if (beginIndex < 0)
            {
                LogDebug("Begin marker not found in RTF for: " + trimmedRegion);
                return false;
            }

            int endIndex = rtf.IndexOf(endMarker, beginIndex + beginMarker.Length, StringComparison.Ordinal);
            if (endIndex < 0)
            {
                LogDebug("End marker not found in RTF for: " + trimmedRegion);
                return false;
            }

            string replacementRtf = EscapeRtfText(replacementText ?? string.Empty);
            int v0Index = rtf.IndexOf(@"\v0", beginIndex + beginMarker.Length, StringComparison.Ordinal);
            int hiddenOnIndex = FindHiddenOnControlWordStart(rtf, endIndex);

            if (v0Index >= 0 && v0Index < endIndex && hiddenOnIndex >= 0 && hiddenOnIndex < endIndex)
            {
                int visibleStart = v0Index + 3;
                if (visibleStart < rtf.Length && rtf[visibleStart] == ' ')
                {
                    visibleStart++;
                }

                if (visibleStart > hiddenOnIndex)
                {
                    LogDebug("Inline RTF span invalid for: " + trimmedRegion);
                    return false;
                }

                string updated = rtf.Substring(0, visibleStart) + replacementRtf + rtf.Substring(hiddenOnIndex);
                RunProgrammaticRtfUpdate(() => richTextBox1.Rtf = updated);
                LogDebug("Region updated via inline RTF: " + trimmedRegion);
                return true;
            }

            if (string.IsNullOrEmpty(replacementRtf))
            {
                LogDebug("Inline RTF clear skipped for: " + trimmedRegion);
                return true;
            }

            int insertStart = beginIndex + beginMarker.Length;
            string updatedRtf = rtf.Insert(insertStart, @"\v0 ");
            endIndex = updatedRtf.IndexOf(endMarker, insertStart + 4, StringComparison.Ordinal);
            if (endIndex < 0)
            {
                LogDebug("Inline RTF end marker missing after insert for: " + trimmedRegion);
                return false;
            }

            updatedRtf = updatedRtf.Insert(endIndex, @"\v ");
            int visibleInsertStart = insertStart + 4;
            if (visibleInsertStart > endIndex)
            {
                LogDebug("Inline RTF insert span invalid for: " + trimmedRegion);
                return false;
            }

            updatedRtf = updatedRtf.Substring(0, visibleInsertStart) + replacementRtf + updatedRtf.Substring(endIndex);
            RunProgrammaticRtfUpdate(() => richTextBox1.Rtf = updatedRtf);
            LogDebug("Region updated via inline RTF insert: " + trimmedRegion);
            return true;
        }

        private bool TryUpdateRegionByText(string regionName, string? replacementText)
        {
            string trimmedRegion = regionName.Trim();
            if (string.IsNullOrEmpty(replacementText))
            {
                LogDebug("Region update via Text skipped for empty replacement: " + trimmedRegion);
                return false;
            }

            string beginMarker = "[[BEGIN:" + trimmedRegion + "]]";
            string endMarker = "[[END:" + trimmedRegion + "]]";
            string text = richTextBox1.Text ?? string.Empty;

            int beginIndex = text.IndexOf(beginMarker, StringComparison.Ordinal);
            if (beginIndex < 0)
            {
                LogDebug("Begin marker not found in Text for: " + trimmedRegion);
                return false;
            }

            int start = beginIndex + beginMarker.Length;
            int endIndex = text.IndexOf(endMarker, start, StringComparison.Ordinal);
            if (endIndex < 0)
            {
                LogDebug("End marker not found in Text for: " + trimmedRegion);
                return false;
            }

            if (endIndex < start)
            {
                LogDebug("Invalid Text span for: " + trimmedRegion);
                return false;
            }

            string replacement = replacementText ?? string.Empty;
            RunProgrammaticRtfUpdate(() => ReplaceTextRange(start, endIndex - start, replacement));
            LogDebug("Region updated via Text: " + trimmedRegion);
            return true;
        }

        private void ReplaceTextRange(int start, int length, string replacement)
        {
            int selStart = richTextBox1.SelectionStart;
            int selLength = richTextBox1.SelectionLength;

            richTextBox1.SelectionStart = start;
            richTextBox1.SelectionLength = length;
            richTextBox1.SelectedText = replacement;

            try
            {
                richTextBox1.SelectionStart = Math.Min(selStart, richTextBox1.TextLength);
                richTextBox1.SelectionLength = selLength;
            }
            catch
            {
                // Selection restore is best-effort.
            }
        }

        private static string EscapeRtfText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            var builder = new StringBuilder(normalized.Length);

            foreach (char c in normalized)
            {
                switch (c)
                {
                    case '\\':
                        builder.Append(@"\\");
                        break;
                    case '{':
                        builder.Append(@"\{");
                        break;
                    case '}':
                        builder.Append(@"\}");
                        break;
                    case '\n':
                        builder.Append(@"\par ");
                        break;
                    case '\t':
                        builder.Append(@"\tab ");
                        break;
                    default:
                        if (c <= 0x7f)
                        {
                            builder.Append(c);
                        }
                        else
                        {
                            builder.Append(@"\u").Append((int)c).Append('?');
                        }
                        break;
                }
            }

            return builder.ToString();
        }

        private static bool TryFindGroupBounds(string rtf, int index, out int groupStart, out int groupEnd)
        {
            groupStart = -1;
            groupEnd = -1;

            if (index < 0 || index >= rtf.Length)
            {
                return false;
            }

            int depth = 0;
            for (int i = index; i >= 0; i--)
            {
                char c = rtf[i];
                if (IsRtfEscaped(rtf, i))
                {
                    continue;
                }

                if (c == '}')
                {
                    depth++;
                }
                else if (c == '{')
                {
                    if (depth == 0)
                    {
                        groupStart = i;
                        break;
                    }

                    depth--;
                }
            }

            if (groupStart < 0)
            {
                return false;
            }

            depth = 0;
            for (int i = groupStart; i < rtf.Length; i++)
            {
                char c = rtf[i];
                if (IsRtfEscaped(rtf, i))
                {
                    continue;
                }

                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        groupEnd = i;
                        return true;
                    }
                }
            }

            return false;
        }

        private static int FindHiddenOnControlWordStart(string rtf, int searchIndex)
        {
            int index = searchIndex;
            while (index >= 0)
            {
                index = rtf.LastIndexOf(@"\v", index, StringComparison.Ordinal);
                if (index < 0)
                {
                    return -1;
                }

                int nextIndex = index + 2;
                if (nextIndex < rtf.Length)
                {
                    char nextChar = rtf[nextIndex];
                    if (char.IsLetterOrDigit(nextChar))
                    {
                        index--;
                        continue;
                    }
                }

                return index;
            }

            return -1;
        }

        private static bool IsRtfEscaped(string rtf, int index)
        {
            int slashCount = 0;
            for (int i = index - 1; i >= 0 && rtf[i] == '\\'; i--)
            {
                slashCount++;
            }

            return (slashCount % 2) == 1;
        }

        private static string InitializeDebugLogPath()
        {
            string cwd = Environment.CurrentDirectory;
            string? dir = FindLogDirectory(cwd);
            if (string.IsNullOrEmpty(dir))
            {
                dir = cwd;
            }

            return Path.Combine(dir, "html-routing-debug.log");
        }

        private static string? FindLogDirectory(string start)
        {
            var current = new DirectoryInfo(start);
            for (int i = 0; i < 6 && current != null; i++)
            {
                string examples = Path.Combine(current.FullName, "examples");
                if (Directory.Exists(examples))
                {
                    return examples;
                }

                current = current.Parent;
            }

            return null;
        }

        private static void LogDebug(string message)
        {
            try
            {
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message + Environment.NewLine;
                lock (DebugLogLock)
                {
                    File.AppendAllText(DebugLogPath, line);
                }
            }
            catch
            {
                // Best-effort logging only.
            }
        }
       
        private void ToolStripBoldButton_Click(object? sender, EventArgs e)
        {
            ApplySelectionStyle(FontStyle.Bold, toolStripBoldButton.CheckState != CheckState.Checked);
        }

        private void ToolStripItalicButton_Click(object? sender, EventArgs e)
        {
            ApplySelectionStyle(FontStyle.Italic, toolStripItalicButton.CheckState != CheckState.Checked);
        }

        private void ToolStripUnderlineButton_Click(object? sender, EventArgs e)
        {
            ApplySelectionStyle(FontStyle.Underline, toolStripUnderlineButton.CheckState != CheckState.Checked);
        }

        private void ToolStripPopHtmlButton_Click(object? sender, EventArgs e)
        {
            ToggleWebViewDocking();
        }

        private void ToolStripPopRtfButton_Click(object? sender, EventArgs e)
        {
            ToggleRichTextDocking();
        }

        private void RichTextBox1_SelectionChanged(object? sender, EventArgs e)
        {
            UpdateFormattingButtons();
        }

        private void ApplySelectionStyle(FontStyle style, bool enable)
        {
            var selectionFont = richTextBox1.SelectionFont ?? richTextBox1.Font;
            FontStyle newStyle = enable ? selectionFont.Style | style : selectionFont.Style & ~style;
            richTextBox1.SelectionFont = new Font(selectionFont, newStyle);
            UpdateFormattingButtons();
        }

        private void UpdateFormattingButtons()
        {
            var font = richTextBox1.SelectionFont;

            UpdateButtonState(toolStripBoldButton, font, FontStyle.Bold);
            UpdateButtonState(toolStripItalicButton, font, FontStyle.Italic);
            UpdateButtonState(toolStripUnderlineButton, font, FontStyle.Underline);
        }

        private static void UpdateButtonState(ToolStripButton button, Font? font, FontStyle style)
        {
            if (font == null)
            {
                button.Checked = false;
                button.CheckState = CheckState.Indeterminate;
            }
            else
            {
                bool isSet = font.Style.HasFlag(style);
                button.Checked = isSet;
                button.CheckState = isSet ? CheckState.Checked : CheckState.Unchecked;
            }
        }

        private void ToggleWebViewDocking()
        {
            if (IsControlDetached(webView2, detachedHtmlHost))
            {
                DockWebView2();
            }
            else
            {
                DetachWebView2();
            }
        }

        private void ToggleRichTextDocking()
        {
            if (IsControlDetached(richTextBox1, detachedRtfHost))
            {
                DockRichTextBox();
            }
            else
            {
                DetachRichTextBox();
            }
        }

        private void DetachWebView2()
        {
            if (detachedHtmlHost == null)
            {
                detachedHtmlHost = CreateDetachedHost("HTML View", DetachedHtmlHost_FormClosing);
            }

            MoveControl(webView2, detachedHtmlHost);
            detachedHtmlHost.Show(this);
            detachedHtmlHost.BringToFront();
            toolStripPopHtmlButton.Text = "Dock HTML";
            UpdateHtmlPanelLayout();
        }

        private void DockWebView2()
        {
            if (detachedHtmlHost == null)
            {
                return;
            }

            MoveControl(webView2, panelHtmlHost);
            detachedHtmlHost.Hide();
            toolStripPopHtmlButton.Text = "Pop HTML";
            UpdateHtmlPanelLayout();
        }

        private void DetachRichTextBox()
        {
            if (detachedRtfHost == null)
            {
                detachedRtfHost = CreateDetachedHost("Rich Text View", DetachedRtfHost_FormClosing);
            }

            MoveControl(richTextBox1, detachedRtfHost);
            detachedRtfHost.Show(this);
            detachedRtfHost.BringToFront();
            toolStripPopRtfButton.Text = "Dock RTF";
        }

        private void DockRichTextBox()
        {
            if (detachedRtfHost == null)
            {
                return;
            }

            MoveControl(richTextBox1, splitContainer1.Panel1);
            detachedRtfHost.Hide();
            toolStripPopRtfButton.Text = "Pop RTF";
        }

        private Form CreateDetachedHost(string title, FormClosingEventHandler closingHandler)
        {
            var host = new Form
            {
                Text = title,
                StartPosition = FormStartPosition.Manual,
                Size = new Size(700, 500),
                ShowInTaskbar = true,
                Owner = this
            };

            host.FormClosing += closingHandler;
            return host;
        }

        private void DetachedHtmlHost_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (isClosing)
            {
                return;
            }

            e.Cancel = true;
            DockWebView2();
        }

        private void DetachedRtfHost_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (isClosing)
            {
                return;
            }

            e.Cancel = true;
            DockRichTextBox();
        }

        private static void MoveControl(Control control, Control destination)
        {
            if (control.Parent != null)
            {
                control.Parent.Controls.Remove(control);
            }

            destination.Controls.Add(control);
            control.Dock = DockStyle.Fill;
        }

        private static bool IsControlDetached(Control control, Form? host)
        {
            return host != null && control.Parent == host;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            isClosing = true;
            detachedHtmlHost?.Close();
            detachedRtfHost?.Close();
            base.OnFormClosing(e);
        }

        private static string SanitizeFileName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "RadEdit";
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                builder.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            }

            string result = builder.ToString().Trim();
            return string.IsNullOrEmpty(result) ? "RadEdit" : result;
        }

        private static string GetAppVersion()
        {
            string? infoVersion = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(infoVersion))
            {
                string trimmed = infoVersion.Trim();
                int plusIndex = trimmed.IndexOf('+');
                return plusIndex > 0 ? trimmed.Substring(0, plusIndex) : trimmed;
            }

            string fallback = Application.ProductVersion ?? string.Empty;
            if (Version.TryParse(fallback, out Version? parsed))
            {
                return $"{parsed.Major}.{parsed.Minor}";
            }

            return "0.2";
        }

        private static class NativeMethods
        {
            internal const int WM_COPYDATA = 0x004A;

            [StructLayout(LayoutKind.Sequential)]
            internal struct CopyDataStruct
            {
                public IntPtr dwData;
                public int cbData;
                public IntPtr lpData;
            }

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref CopyDataStruct lParam);

            internal static CopyDataStruct GetCopyData(IntPtr pointer)
            {
                if (pointer == IntPtr.Zero)
                {
                    throw new ArgumentNullException(nameof(pointer));
                }

                return Marshal.PtrToStructure<CopyDataStruct>(pointer);
            }

            internal static string CopyDataToString(CopyDataStruct data)
            {
                if (data.cbData <= 0 || data.lpData == IntPtr.Zero)
                {
                    return string.Empty;
                }

                int charCount = data.cbData / sizeof(char);
                string? value = Marshal.PtrToStringUni(data.lpData, charCount);
                return value?.TrimEnd('\0') ?? string.Empty;
            }

            internal static bool SendCopyData(IntPtr targetHandle, CopyDataCommand command, string? message)
            {
                if (targetHandle == IntPtr.Zero)
                {
                    return false;
                }

                message ??= string.Empty;

                IntPtr buffer = Marshal.StringToHGlobalUni(message);

                try
                {
                    var data = new CopyDataStruct
                    {
                        dwData = new IntPtr((long)command),
                        lpData = buffer,
                        cbData = (message.Length + 1) * sizeof(char)
                    };

                    IntPtr result = SendMessage(targetHandle, WM_COPYDATA, IntPtr.Zero, ref data);
                    return result != IntPtr.Zero;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }
    }
}
