using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace RadEdit
{
    internal sealed class LanguageToolRichTextBox : RichTextBox
    {
        private readonly List<TextRange> underlineRanges = new();
        private readonly Pen underlinePen = new(Color.FromArgb(255, 18, 18), 3f);
        private readonly Brush activeHighlightBrush = new SolidBrush(Color.FromArgb(90, 255, 120, 120));
        private readonly Pen inactiveCaretPen = new(Color.FromArgb(220, 0, 120, 215), 2f);
        private TextRange? activeRange;

        public LanguageToolRichTextBox()
        {
            SelectionChanged += (_, _) => Invalidate();
            TextChanged += (_, _) => Invalidate();
            GotFocus += (_, _) => Invalidate();
            LostFocus += (_, _) => Invalidate();
            Resize += (_, _) => Invalidate();
        }

        public void SetUnderlineRanges(IEnumerable<TextRange> ranges)
        {
            underlineRanges.Clear();
            underlineRanges.AddRange(ranges);
            Invalidate();
        }

        public void SetActiveRange(TextRange? range)
        {
            activeRange = range;
            Invalidate();
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_MOUSEACTIVATE = 0x0021;
            if (m.Msg == WM_MOUSEACTIVATE && CanFocus && !Focused)
            {
                Focus();
            }

            base.WndProc(ref m);

            const int WM_PAINT = 0x000F;
            const int WM_PRINTCLIENT = 0x0318;
            const int WM_VSCROLL = 0x0115;
            const int WM_HSCROLL = 0x0114;
            const int WM_MOUSEWHEEL = 0x020A;

            if (m.Msg == WM_PAINT || m.Msg == WM_PRINTCLIENT)
            {
                DrawUnderlines();
            }

            if ((m.Msg == WM_VSCROLL || m.Msg == WM_HSCROLL || m.Msg == WM_MOUSEWHEEL) && ShouldDrawInactiveCaret())
            {
                Invalidate();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                underlinePen.Dispose();
                activeHighlightBrush.Dispose();
                inactiveCaretPen.Dispose();
            }

            base.Dispose(disposing);
        }

        private void DrawUnderlines()
        {
            Stopwatch? totalStopwatch = RadEditDebugLog.StartTiming();
            using Graphics graphics = CreateGraphics();
            int textLength = TextLength;
            if (textLength == 0)
            {
                DrawInactiveCaret(graphics, textLength);
                if (totalStopwatch != null)
                {
                    totalStopwatch.Stop();
                    RadEditDebugLog.WriteSlowOperation(
                        "LanguageToolRichTextBox.DrawUnderlines",
                        totalStopwatch.ElapsedMilliseconds,
                        16,
                        "textLength=0");
                }
                return;
            }

            int lineCount = Lines.Length;

            if (activeRange.HasValue)
            {
                DrawActiveHighlight(graphics, activeRange.Value, textLength, lineCount);
            }

            if (underlineRanges.Count > 0)
            {
                foreach (var range in underlineRanges)
                {
                    int start = Math.Max(0, Math.Min(range.Start, textLength));
                    int end = Math.Max(0, Math.Min(range.Start + range.Length, textLength));
                    if (end <= start)
                    {
                        continue;
                    }

                    int startLine = GetLineFromCharIndex(start);
                    int endLine = GetLineFromCharIndex(Math.Max(start, end - 1));

                    for (int line = startLine; line <= endLine; line++)
                    {
                        int lineStart = GetFirstCharIndexFromLine(line);
                        int lineEnd = (line + 1 < lineCount) ? GetFirstCharIndexFromLine(line + 1) : textLength;
                        if (lineStart < 0 || lineEnd < 0)
                        {
                            continue;
                        }

                        int segmentStart = Math.Max(start, lineStart);
                        int segmentEnd = Math.Min(end, lineEnd);
                        if (segmentEnd <= segmentStart)
                        {
                            continue;
                        }

                        Point startPoint = GetPositionFromCharIndex(segmentStart);
                        Point endPoint = GetUnderlineEndPoint(segmentEnd, textLength);
                        int underlineY = startPoint.Y + Font.Height - 2;

                        if (endPoint.X <= startPoint.X)
                        {
                            continue;
                        }

                        graphics.DrawLine(underlinePen,
                            startPoint.X,
                            underlineY,
                            endPoint.X,
                            underlineY);
                    }
                }
            }

            DrawInactiveCaret(graphics, textLength);
            if (totalStopwatch != null)
            {
                totalStopwatch.Stop();
                RadEditDebugLog.WriteSlowOperation(
                    "LanguageToolRichTextBox.DrawUnderlines",
                    totalStopwatch.ElapsedMilliseconds,
                    16,
                    $"textLength={textLength} underlines={underlineRanges.Count} active={(activeRange.HasValue ? 1 : 0)}");
            }
        }

        private Point GetUnderlineEndPoint(int charIndex, int textLength)
        {
            if (charIndex <= 0)
            {
                return GetPositionFromCharIndex(0);
            }

            int lastIndex = Math.Min(charIndex - 1, textLength - 1);
            Point pos = GetPositionFromCharIndex(lastIndex);
            string ch = Text[lastIndex].ToString();
            int width = TextRenderer.MeasureText(ch, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width;
            return new Point(pos.X + width, pos.Y);
        }

        private void DrawActiveHighlight(Graphics graphics, TextRange range, int textLength, int lineCount)
        {
            int start = Math.Max(0, Math.Min(range.Start, textLength));
            int end = Math.Max(0, Math.Min(range.Start + range.Length, textLength));
            if (end <= start)
            {
                return;
            }

            int startLine = GetLineFromCharIndex(start);
            int endLine = GetLineFromCharIndex(Math.Max(start, end - 1));

            for (int line = startLine; line <= endLine; line++)
            {
                int lineStart = GetFirstCharIndexFromLine(line);
                int lineEnd = (line + 1 < lineCount) ? GetFirstCharIndexFromLine(line + 1) : textLength;
                if (lineStart < 0 || lineEnd < 0)
                {
                    continue;
                }

                int segmentStart = Math.Max(start, lineStart);
                int segmentEnd = Math.Min(end, lineEnd);
                if (segmentEnd <= segmentStart)
                {
                    continue;
                }

                Point startPoint = GetPositionFromCharIndex(segmentStart);
                Point endPoint = GetUnderlineEndPoint(segmentEnd, textLength);
                int top = startPoint.Y - 1;
                int height = Font.Height + 2;
                int width = Math.Max(1, endPoint.X - startPoint.X);

                graphics.FillRectangle(activeHighlightBrush,
                    startPoint.X - 1,
                    top,
                    width + 2,
                    height);
            }
        }

        private void DrawInactiveCaret(Graphics graphics, int textLength)
        {
            if (!ShouldDrawInactiveCaret())
            {
                return;
            }

            Point caretPoint = GetInactiveCaretPoint(textLength);
            int caretHeight = Math.Max(2, Font.Height);
            int caretBottom = Math.Min(ClientRectangle.Height - 1, caretPoint.Y + caretHeight);

            graphics.DrawLine(
                inactiveCaretPen,
                caretPoint.X,
                caretPoint.Y,
                caretPoint.X,
                caretBottom);
        }

        private bool ShouldDrawInactiveCaret()
        {
            return !Focused &&
                   Visible &&
                   Enabled &&
                   SelectionLength == 0;
        }

        private Point GetInactiveCaretPoint(int textLength)
        {
            int caretIndex = Math.Max(0, Math.Min(SelectionStart, textLength));
            Point caretPoint = GetPositionFromCharIndex(caretIndex);

            if (caretIndex == textLength && textLength > 0)
            {
                Point lastCharPoint = GetPositionFromCharIndex(textLength - 1);
                if (caretPoint == lastCharPoint)
                {
                    char lastChar = Text[textLength - 1];
                    if (lastChar != '\r' && lastChar != '\n')
                    {
                        caretPoint = GetUnderlineEndPoint(textLength, textLength);
                    }
                }
            }

            return new Point(
                Math.Max(1, Math.Min(caretPoint.X, ClientRectangle.Width - 1)),
                Math.Max(1, Math.Min(caretPoint.Y, Math.Max(1, ClientRectangle.Height - Font.Height))));
        }
    }
}
