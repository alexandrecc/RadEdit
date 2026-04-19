using System;
using System.Collections.Generic;

namespace RadEdit
{
    internal readonly struct SentenceSegment
    {
        public SentenceSegment(int start, int length, string text)
        {
            Start = start;
            Length = length;
            Text = text ?? string.Empty;
        }

        public int Start { get; }
        public int Length { get; }
        public string Text { get; }
    }

    internal static class SentenceSegmentation
    {
        public static List<SentenceSegment> Split(string text)
        {
            var segments = new List<SentenceSegment>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return segments;
            }

            int segmentStart = -1;
            for (int i = 0; i < text.Length; i++)
            {
                char current = text[i];
                if (segmentStart < 0)
                {
                    if (char.IsWhiteSpace(current))
                    {
                        continue;
                    }

                    segmentStart = i;
                }

                bool atBoundary = IsSentenceBoundary(text, i);
                bool atEnd = i == text.Length - 1;
                if (!atBoundary && !atEnd)
                {
                    continue;
                }

                int endExclusive = atBoundary ? ExtendSentenceBoundaryEnd(text, i + 1) : i + 1;
                int trimmedEnd = endExclusive;
                while (trimmedEnd > segmentStart && char.IsWhiteSpace(text[trimmedEnd - 1]))
                {
                    trimmedEnd--;
                }

                if (trimmedEnd > segmentStart)
                {
                    segments.Add(new SentenceSegment(
                        segmentStart,
                        trimmedEnd - segmentStart,
                        text.Substring(segmentStart, trimmedEnd - segmentStart)));
                }

                segmentStart = -1;
                i = Math.Max(i, endExclusive - 1);
            }

            if (segmentStart >= 0 && segmentStart < text.Length)
            {
                string tail = text.Substring(segmentStart).TrimEnd();
                if (!string.IsNullOrWhiteSpace(tail))
                {
                    segments.Add(new SentenceSegment(segmentStart, tail.Length, tail));
                }
            }

            return segments;
        }

        private static bool IsSentenceBoundary(string text, int index)
        {
            char current = text[index];
            if (current == '\r' || current == '\n')
            {
                return true;
            }

            if (current != '.' && current != '!' && current != '?')
            {
                return false;
            }

            int next = index + 1;
            if (next >= text.Length)
            {
                return true;
            }

            char nextChar = text[next];
            return char.IsWhiteSpace(nextChar) || nextChar == '"' || nextChar == '\'' || nextChar == ')' || nextChar == ']';
        }

        private static int ExtendSentenceBoundaryEnd(string text, int index)
        {
            int end = index;
            while (end < text.Length)
            {
                char current = text[end];
                if (current == '"' || current == '\'' || current == ')' || current == ']' || current == '}')
                {
                    end++;
                    continue;
                }

                break;
            }

            return end;
        }
    }
}
