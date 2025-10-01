using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

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
            SetName = 11
        }

        private const int WM_COPYDATA = 0x004A;

        public Form1()
        {
            InitializeComponent();
            richTextBox1.SelectionChanged += RichTextBox1_SelectionChanged;
            UpdateFormattingButtons();
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
                case CopyDataCommand.SetTitle:
                    return TrySetTitle(payload);
                case CopyDataCommand.GetTitle:
                    return TrySendTitle(senderHandle);
                case CopyDataCommand.SetName:
                    return TrySetName(payload);
                case CopyDataCommand.TempFileResponse:
                case CopyDataCommand.TitleResponse:
                case CopyDataCommand.ErrorResponse:
                    // Responses are handled upstream by callers.
                    return true;
                default:
                    NativeMethods.SendCopyData(senderHandle, CopyDataCommand.ErrorResponse, "Unknown command.");
                    return false;
            }
        }

        private bool TrySetRtf(string? rtf)
        {
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
            if (string.IsNullOrEmpty(rtf))
            {
                return false;
            }

            richTextBox1.SelectedRtf = rtf;
            return true;
        }

        private bool TryApplyRtfFile(string? path, bool replaceContent)
        {
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