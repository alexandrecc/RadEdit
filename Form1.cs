using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

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
            SetHtmlFile = 15,
            RequestHtmlFile = 16,
            HtmlFileResponse = 17
        }

        private const int WM_COPYDATA = 0x004A;
        private const string HtmlHostName = "radedit.local";
        private const string HtmlLanguage = "fr-CA";
        private Task? webView2Initialization;
        private string? htmlRootFolder;
        private bool isHtmlMode;
        private string lastPlainText = string.Empty;
        private readonly SemaphoreSlim htmlInsertGate = new(1, 1);

        public Form1()
        {
            InitializeComponent();
            richTextBox1.SelectionChanged += RichTextBox1_SelectionChanged;
            richTextBox1.TextChanged += RichTextBox1_TextChanged;
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
                case CopyDataCommand.ErrorResponse:
                    // Responses are handled upstream by callers.
                    return true;
                case CopyDataCommand.GetName:
                    return TrySendName(senderHandle);
                case CopyDataCommand.GotoEnd:
                    return TryGotoEnd();
                default:
                    NativeMethods.SendCopyData(senderHandle, CopyDataCommand.ErrorResponse, "Unknown command.");
                    return false;
            }
        }

        private bool TrySetRtf(string? rtf)
        {
            SetHtmlMode(false);
            if (string.IsNullOrEmpty(rtf))
            {
                richTextBox1.Clear();
                return true;
            }

            richTextBox1.Rtf = rtf;
            return true;
        }

        private bool TryInsertRtf(string? rtf)
        {
            SetHtmlMode(false);
            if (string.IsNullOrEmpty(rtf))
            {
                return false;
            }

            richTextBox1.SelectedRtf = rtf;
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
                richTextBox1.LoadFile(fullPath, RichTextBoxStreamType.RichText);
            }
            else
            {
                using var buffer = new RichTextBox();
                buffer.LoadFile(fullPath, RichTextBoxStreamType.RichText);
                richTextBox1.SelectedRtf = buffer.Rtf;
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
                        NativeMethods.SendCopyData(recipient, CopyDataCommand.ErrorResponse, "HTML mode is not active.");
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
            await EnsureWebView2InitializedAsync();

            string? folder = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(folder))
            {
                throw new InvalidOperationException("HTML file folder could not be resolved.");
            }

            ConfigureHtmlMapping(folder);

            string fileName = Path.GetFileName(fullPath);
            string url = $"https://{HtmlHostName}/{Uri.EscapeDataString(fileName)}";
            webView2.Source = new Uri(url);
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

        private void SetHtmlMode(bool enable)
        {
            if (isHtmlMode == enable)
            {
                return;
            }

            isHtmlMode = enable;
            lastPlainText = richTextBox1.Text;
            richTextBox1.Visible = !enable;
            webView2.Visible = enable;

            toolStripBoldButton.Enabled = !enable;
            toolStripItalicButton.Enabled = !enable;
            toolStripUnderlineButton.Enabled = !enable;

            if (enable)
            {
                webView2.BringToFront();
            }
            else
            {
                richTextBox1.BringToFront();
            }
        }

        private async void RichTextBox1_TextChanged(object? sender, EventArgs e)
        {
            string newText = richTextBox1.Text;

            if (!isHtmlMode)
            {
                lastPlainText = newText;
                return;
            }

            string inserted = ExtractInsertedText(lastPlainText, newText);
            lastPlainText = newText;

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
            if (!isHtmlMode || string.IsNullOrEmpty(text))
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
