using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace RadEdit
{
    internal sealed class LanguageToolRichTextBox : RichTextBox
    {
        private readonly List<TextRange> underlineRanges = new();
        private readonly Pen underlinePen = new(Color.FromArgb(255, 18, 18), 3f);
        private readonly Brush activeHighlightBrush = new SolidBrush(Color.FromArgb(90, 255, 120, 120));
        private TextRange? activeRange;

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
            base.WndProc(ref m);

            const int WM_PAINT = 0x000F;
            const int WM_PRINTCLIENT = 0x0318;
            if (m.Msg == WM_PAINT || m.Msg == WM_PRINTCLIENT)
            {
                DrawUnderlines();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                underlinePen.Dispose();
                activeHighlightBrush.Dispose();
            }

            base.Dispose(disposing);
        }

        private void DrawUnderlines()
        {
            if (TextLength == 0)
            {
                return;
            }

            using Graphics graphics = CreateGraphics();
            int textLength = TextLength;
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
    }
}
