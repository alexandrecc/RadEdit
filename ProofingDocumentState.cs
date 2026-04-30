using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace RadEdit
{
    internal enum ProofingChangeSource
    {
        UserEdit,
        TrustedAutomation
    }

    internal enum SlotProofPolicy
    {
        Strict,
        FieldValue,
        SentenceScoped
    }

    internal enum ProofingUnitKind
    {
        Sentence,
        SlotContext
    }

    internal readonly struct ProofingUnit
    {
        public ProofingUnit(ProofingUnitKind kind, int start, int length, string text)
        {
            Kind = kind;
            Start = start;
            Length = length;
            Text = text ?? string.Empty;
        }

        public ProofingUnitKind Kind { get; }
        public int Start { get; }
        public int Length { get; }
        public string Text { get; }
    }

    internal readonly record struct ProofingSlot(
        Guid Id,
        TextRange Range,
        SlotProofPolicy Policy,
        bool IsProtected);

    internal sealed class ProofingDocumentState
    {
        private readonly List<TextRange> trustedRanges = new();
        private readonly List<ProofingSlot> slots = new();
        private readonly List<TextRange> protectedRanges = new();
        private string currentText = string.Empty;

        public void Reset(string text)
        {
            currentText = text ?? string.Empty;
            trustedRanges.Clear();
            slots.Clear();
            protectedRanges.Clear();
        }

        public void ApplyChange(string newText, ProofingChangeSource source)
        {
            newText ??= string.Empty;
            if (string.Equals(currentText, newText, StringComparison.Ordinal))
            {
                return;
            }

            string oldText = currentText;
            var oldTrusted = new List<TextRange>(trustedRanges);
            var oldSlots = new List<ProofingSlot>(slots);
            TextChange change = ComputeTextChange(oldText, newText);
            RadEditDebugLog.Write(
                "Proofing state change. "
                + "Source=" + source
                + " Start=" + change.Start.ToString(CultureInfo.InvariantCulture)
                + " OldLength=" + change.OldLength.ToString(CultureInfo.InvariantCulture)
                + " NewLength=" + change.NewLength.ToString(CultureInfo.InvariantCulture)
                + " OldFragment=" + DescribeChangedFragment(oldText, change.Start, change.OldLength)
                + " NewFragment=" + DescribeChangedFragment(newText, change.Start, change.NewLength)
                + " Before=" + DescribeCurrentStateForLog());
            int oldChangeEnd = change.Start + change.OldLength;
            int delta = change.NewLength - change.OldLength;

            MapRanges(trustedRanges, change.Start, oldChangeEnd, delta, newText.Length);
            MapSlots(slots, change.Start, oldChangeEnd, delta, newText.Length);
            currentText = newText;

            if (source == ProofingChangeSource.TrustedAutomation)
            {
                var changedRange = new TextRange(change.Start, change.NewLength);
                SubtractRange(trustedRanges, changedRange);
                RemoveIntersectingSlots(changedRange);
                AddTrustedRegionState(changedRange);
                NormalizeRanges(trustedRanges);
                NormalizeSlots(slots);
                RebuildProtectedRanges();
                RadEditDebugLog.Write(
                    "Proofing state updated from trusted automation. "
                    + "ChangedRange=" + DescribeRange(changedRange)
                    + " After=" + DescribeCurrentStateForLog());
                return;
            }

            ProofingSlot? touchedProtectedSlot = FindTouchedProtectedSlot(oldSlots, change);
            bool touchedTrustedScaffold = TouchesTrustedContent(oldTrusted, change);
            RadEditDebugLog.Write(
                "Proofing user edit analysis. "
                + "TouchedProtectedSlot=" + DescribeNullableSlot(touchedProtectedSlot)
                + " TouchedTrustedScaffold=" + touchedTrustedScaffold);

            if (touchedProtectedSlot is ProofingSlot protectedSlot)
            {
                SetSlotProtection(protectedSlot.Id, isProtected: false);

                if (protectedSlot.Policy == SlotProofPolicy.SentenceScoped || touchedTrustedScaffold)
                {
                    TextRange sentenceRange = ExpandChangeToSentenceRange(newText, change);
                    if (sentenceRange.Length > 0)
                    {
                        SubtractRange(trustedRanges, sentenceRange);
                    }
                }
            }
            else if (touchedTrustedScaffold)
            {
                TextRange sentenceRange = ExpandChangeToSentenceRange(newText, change);
                if (sentenceRange.Length > 0)
                {
                    SubtractRange(trustedRanges, sentenceRange);
                }
            }

            UpdateSlotProtectionFromCurrentText();
            NormalizeRanges(trustedRanges);
            NormalizeSlots(slots);
            RebuildProtectedRanges();
            RadEditDebugLog.Write("Proofing state updated from user edit. After=" + DescribeCurrentStateForLog());
        }

        public bool HasProofableContent(string text)
        {
            if (!string.Equals(currentText, text ?? string.Empty, StringComparison.Ordinal))
            {
                return !string.IsNullOrWhiteSpace(text);
            }

            return BuildLlmProofingUnits().Count > 0;
        }

        public bool IsRangeProtected(int start, int length)
        {
            return IsRangeCoveredByRanges(protectedRanges, start, length);
        }

        public bool IntersectsProtectedRange(int start, int length)
        {
            if (length <= 0)
            {
                return false;
            }

            int end = start + length;
            foreach (TextRange range in protectedRanges)
            {
                int rangeStart = range.Start;
                int rangeEnd = range.Start + range.Length;
                if (rangeEnd <= start)
                {
                    continue;
                }

                if (rangeStart >= end)
                {
                    break;
                }

                return true;
            }

            return false;
        }

        public List<ProofingUnit> BuildLlmProofingUnits()
        {
            return BuildLlmProofingUnitsCore(debugLines: null);
        }

        public string DescribeCurrentStateForLog()
        {
            return "TextLength=" + currentText.Length.ToString(CultureInfo.InvariantCulture)
                + " Trusted=" + DescribeRanges(trustedRanges)
                + " Slots=" + DescribeSlots(slots)
                + " Protected=" + DescribeRanges(protectedRanges);
        }

        public string DescribeLlmProofingPlanForLog()
        {
            var debugLines = new List<string>();
            List<ProofingUnit> units = BuildLlmProofingUnitsCore(debugLines);
            return "State={" + DescribeCurrentStateForLog()
                + "} Units=" + DescribeUnits(units)
                + " Decisions=" + DescribeDebugLines(debugLines);
        }

        private List<ProofingUnit> BuildLlmProofingUnitsCore(List<string>? debugLines)
        {
            var units = new List<ProofingUnit>();
            if (string.IsNullOrWhiteSpace(currentText))
            {
                debugLines?.Add("No units because current text is empty.");
                return units;
            }

            List<SentenceSegment> segments = SentenceSegmentation.Split(currentText);
            if (segments.Count == 0)
            {
                debugLines?.Add("No units because sentence segmentation returned 0 segments.");
                return units;
            }

            var sentenceUnitRanges = new List<TextRange>();
            var fieldValueSentenceCoverageRanges = new List<TextRange>();
            foreach (ProofingSlot slot in slots)
            {
                if (slot.IsProtected || slot.Policy != SlotProofPolicy.FieldValue)
                {
                    continue;
                }

                fieldValueSentenceCoverageRanges.Add(slot.Range);

                TextRange fieldLabelRange = GetFieldValueSentenceShieldRange(slot);
                if (fieldLabelRange.Length > 0)
                {
                    fieldValueSentenceCoverageRanges.Add(fieldLabelRange);
                }
            }

            foreach (SentenceSegment segment in segments)
            {
                var segmentRange = new TextRange(segment.Start, segment.Length);
                bool hasSentenceScopedSlot = slots.Any(slot =>
                    !slot.IsProtected &&
                    slot.Policy == SlotProofPolicy.SentenceScoped &&
                    RangesIntersect(slot.Range, segmentRange));
                if (hasSentenceScopedSlot)
                {
                    units.Add(new ProofingUnit(ProofingUnitKind.Sentence, segment.Start, segment.Length, segment.Text));
                    sentenceUnitRanges.Add(segmentRange);
                    debugLines?.Add(
                        "Sentence " + DescribeSegment(segment)
                        + " -> emitted sentence unit because it intersects a sentence-scoped slot.");
                    continue;
                }

                if (IsRangeProtected(segment.Start, segment.Length))
                {
                    debugLines?.Add(
                        "Sentence " + DescribeSegment(segment)
                        + " -> skipped because the full sentence is protected.");
                    continue;
                }

                var coverageRanges = new List<TextRange>(protectedRanges.Count + fieldValueSentenceCoverageRanges.Count);
                coverageRanges.AddRange(protectedRanges);
                foreach (TextRange range in fieldValueSentenceCoverageRanges)
                {
                    if (RangesIntersect(range, segmentRange))
                    {
                        coverageRanges.Add(range);
                    }
                }

                NormalizeRanges(coverageRanges);
                if (!IsRangeCoveredByRanges(coverageRanges, segment.Start, segment.Length))
                {
                    units.Add(new ProofingUnit(ProofingUnitKind.Sentence, segment.Start, segment.Length, segment.Text));
                    sentenceUnitRanges.Add(segmentRange);
                    debugLines?.Add(
                        "Sentence " + DescribeSegment(segment)
                        + " -> emitted sentence unit because coverage is incomplete. Coverage="
                        + DescribeRanges(coverageRanges));
                }
                else
                {
                    debugLines?.Add(
                        "Sentence " + DescribeSegment(segment)
                        + " -> skipped because protected ranges plus field-value slots cover the full sentence. Coverage="
                        + DescribeRanges(coverageRanges));
                }
            }

            var emittedSlotContexts = new HashSet<string>(StringComparer.Ordinal);
            foreach (ProofingSlot slot in slots)
            {
                if (slot.IsProtected)
                {
                    debugLines?.Add(
                        "Slot " + DescribeSlot(slot)
                        + " -> no slot-context unit because it is still protected.");
                    continue;
                }

                if (slot.Policy == SlotProofPolicy.SentenceScoped)
                {
                    debugLines?.Add(
                        "Slot " + DescribeSlot(slot)
                        + " -> no slot-context unit because it is sentence-scoped.");
                    continue;
                }

                if (slot.Policy == SlotProofPolicy.Strict)
                {
                    debugLines?.Add(
                        "Slot " + DescribeSlot(slot)
                        + " -> no slot-context unit because strict slots use sentence-segment units.");
                    continue;
                }

                bool coveredBySentenceUnit = sentenceUnitRanges.Any(range => RangeContains(range, slot.Range));
                if (coveredBySentenceUnit)
                {
                    debugLines?.Add(
                        "Slot " + DescribeSlot(slot)
                        + " -> no slot-context unit because an emitted sentence unit already covers it.");
                    continue;
                }

                List<TextRange> contextRanges = BuildFieldValueSlotContextRanges(slot.Range);
                if (contextRanges.Count > 1)
                {
                    debugLines?.Add(
                        "Slot " + DescribeSlot(slot)
                        + " -> split field-value slot into "
                        + contextRanges.Count.ToString(CultureInfo.InvariantCulture)
                        + " sentence-sized slot-context units.");
                }

                foreach (TextRange contextRange in contextRanges)
                {
                    string contextKey = contextRange.Start.ToString() + ":" + contextRange.Length.ToString();
                    if (!emittedSlotContexts.Add(contextKey))
                    {
                        debugLines?.Add(
                            "Slot " + DescribeSlot(slot)
                            + " -> no slot-context unit because context "
                            + DescribeRange(contextRange)
                            + " was already emitted.");
                        continue;
                    }

                    if (contextRange.Start < 0 ||
                        contextRange.Length <= 0 ||
                        contextRange.Start + contextRange.Length > currentText.Length)
                    {
                        debugLines?.Add(
                            "Slot " + DescribeSlot(slot)
                            + " -> skipped because computed context range is invalid: "
                            + DescribeRange(contextRange));
                        continue;
                    }

                    string contextText = currentText.Substring(contextRange.Start, contextRange.Length);
                    units.Add(new ProofingUnit(
                        ProofingUnitKind.SlotContext,
                        contextRange.Start,
                        contextRange.Length,
                        contextText));
                    debugLines?.Add(
                        "Slot " + DescribeSlot(slot)
                        + " -> emitted slot-context unit " + DescribeRange(contextRange)
                        + " Text=" + FormatTextForLog(contextText));
                }
            }

            units.Sort((left, right) => left.Start.CompareTo(right.Start));
            debugLines?.Add("Final proofing units=" + DescribeUnits(units));
            return units;
        }

        private List<TextRange> BuildFieldValueSlotContextRanges(TextRange slotRange)
        {
            var ranges = new List<TextRange>();
            if (slotRange.Start < 0 ||
                slotRange.Length <= 0 ||
                slotRange.Start + slotRange.Length > currentText.Length)
            {
                ranges.Add(slotRange);
                return ranges;
            }

            string slotText = currentText.Substring(slotRange.Start, slotRange.Length);
            List<SentenceSegment> segments = SentenceSegmentation.Split(slotText);
            if (segments.Count == 0)
            {
                ranges.Add(slotRange);
                return ranges;
            }

            foreach (SentenceSegment segment in segments)
            {
                ranges.Add(new TextRange(slotRange.Start + segment.Start, segment.Length));
            }

            return ranges;
        }

        private TextRange GetFieldValueSentenceShieldRange(ProofingSlot slot)
        {
            TextRange lineRange = ExpandRangeToLineRange(currentText, slot.Range);
            if (lineRange.Length <= 0 || slot.Range.Start <= lineRange.Start)
            {
                return default;
            }

            int prefixLength = slot.Range.Start - lineRange.Start;
            return prefixLength > 0
                ? new TextRange(lineRange.Start, prefixLength)
                : default;
        }

        private void AddTrustedRegionState(TextRange range)
        {
            if (range.Length <= 0 || string.IsNullOrEmpty(currentText))
            {
                return;
            }

            int start = Math.Max(0, Math.Min(range.Start, currentText.Length));
            int end = Math.Max(start, Math.Min(range.Start + range.Length, currentText.Length));
            if (end <= start)
            {
                return;
            }

            int cursor = start;
            int i = start;
            while (i < end)
            {
                if (i + 1 < end && currentText[i] == '[' && currentText[i + 1] == ']')
                {
                    if (i > cursor)
                    {
                        trustedRanges.Add(new TextRange(cursor, i - cursor));
                    }

                    var slotRange = new TextRange(i, 2);
                    SlotProofPolicy policy = ClassifySlotPolicy(slotRange);
                    slots.Add(new ProofingSlot(Guid.NewGuid(), slotRange, policy, true));
                    i += 2;
                    cursor = i;
                    continue;
                }

                i++;
            }

            if (cursor < end)
            {
                trustedRanges.Add(new TextRange(cursor, end - cursor));
            }
        }

        private SlotProofPolicy ClassifySlotPolicy(TextRange slotRange)
        {
            TextRange lineRange = ExpandRangeToLineRange(currentText, slotRange);
            if (lineRange.Length <= 0)
            {
                return SlotProofPolicy.Strict;
            }

            string lineText = currentText.Substring(lineRange.Start, lineRange.Length);
            int localStart = Math.Max(0, slotRange.Start - lineRange.Start);
            int localEnd = Math.Min(lineText.Length, localStart + slotRange.Length);
            string before = lineText.Substring(0, Math.Max(0, localStart));
            string after = localEnd < lineText.Length
                ? lineText.Substring(localEnd)
                : string.Empty;

            string beforeTrimmed = before.TrimEnd();
            string afterTrimmed = after.TrimStart();
            string trimmedLine = lineText.Trim();

            if (Regex.IsMatch(beforeTrimmed, @"[:;]\s*$", RegexOptions.CultureInvariant))
            {
                return SlotProofPolicy.FieldValue;
            }

            if (LooksLikeHeading(trimmedLine))
            {
                return SlotProofPolicy.Strict;
            }

            if (LooksLikeSentenceScoped(trimmedLine, beforeTrimmed, afterTrimmed))
            {
                return SlotProofPolicy.SentenceScoped;
            }

            return SlotProofPolicy.Strict;
        }

        private static bool LooksLikeHeading(string text)
        {
            string trimmed = (text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return false;
            }

            int letterCount = 0;
            int lowerCount = 0;
            int upperCount = 0;
            foreach (char c in trimmed)
            {
                if (!char.IsLetter(c))
                {
                    continue;
                }

                letterCount++;
                if (char.IsLower(c))
                {
                    lowerCount++;
                }
                else if (char.IsUpper(c))
                {
                    upperCount++;
                }
            }

            if (letterCount == 0 || lowerCount > 0 || upperCount != letterCount)
            {
                return false;
            }

            int wordCount = Regex.Matches(trimmed, @"[\p{L}\p{N}]+", RegexOptions.CultureInvariant).Count;
            return wordCount > 0 && wordCount <= 12 && trimmed.Length <= 120;
        }

        private static bool LooksLikeSentenceScoped(string line, string before, string after)
        {
            string trimmedLine = (line ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmedLine))
            {
                return false;
            }

            bool hasLowercase = trimmedLine.Any(char.IsLower);
            if (!hasLowercase)
            {
                return false;
            }

            int wordCount = Regex.Matches(trimmedLine, @"[\p{L}\p{N}]+", RegexOptions.CultureInvariant).Count;
            if (wordCount < 3)
            {
                return false;
            }

            bool hasWordBefore = Regex.IsMatch(before ?? string.Empty, @"[\p{L}\p{N}]", RegexOptions.CultureInvariant);
            bool hasWordAfter = Regex.IsMatch(after ?? string.Empty, @"[\p{L}\p{N}]", RegexOptions.CultureInvariant);
            bool endsLikeSentence = Regex.IsMatch(trimmedLine, @"[.!?]\s*$", RegexOptions.CultureInvariant);
            return hasWordBefore && (hasWordAfter || endsLikeSentence);
        }

        private void UpdateSlotProtectionFromCurrentText()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                ProofingSlot slot = slots[i];
                if (slot.Range.Length <= 0 ||
                    slot.Range.Start < 0 ||
                    slot.Range.Start + slot.Range.Length > currentText.Length)
                {
                    continue;
                }

                bool shouldProtect = string.Equals(
                    currentText.Substring(slot.Range.Start, slot.Range.Length),
                    "[]",
                    StringComparison.Ordinal);
                if (slot.IsProtected != shouldProtect)
                {
                    slots[i] = slot with { IsProtected = shouldProtect };
                }
            }
        }

        private void RebuildProtectedRanges()
        {
            protectedRanges.Clear();
            protectedRanges.AddRange(trustedRanges);
            foreach (ProofingSlot slot in slots)
            {
                if (slot.IsProtected)
                {
                    protectedRanges.Add(slot.Range);
                }
            }

            NormalizeRanges(protectedRanges);
        }

        private void RemoveIntersectingSlots(TextRange range)
        {
            if (range.Length <= 0 || slots.Count == 0)
            {
                return;
            }

            int end = range.Start + range.Length;
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                ProofingSlot slot = slots[i];
                int slotEnd = slot.Range.Start + slot.Range.Length;
                if (slotEnd <= range.Start || slot.Range.Start >= end)
                {
                    continue;
                }

                slots.RemoveAt(i);
            }
        }

        private void SetSlotProtection(Guid slotId, bool isProtected)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                ProofingSlot slot = slots[i];
                if (slot.Id == slotId)
                {
                    slots[i] = slot with { IsProtected = isProtected };
                    return;
                }
            }
        }

        private static ProofingSlot? FindTouchedProtectedSlot(List<ProofingSlot> previousSlots, TextChange change)
        {
            foreach (ProofingSlot slot in previousSlots)
            {
                if (!slot.IsProtected)
                {
                    continue;
                }

                if (change.OldLength == 0)
                {
                    int slotEnd = slot.Range.Start + slot.Range.Length;
                    if (change.Start >= slot.Range.Start && change.Start < slotEnd)
                    {
                        return slot;
                    }

                    continue;
                }

                if (RangesIntersect(slot.Range, new TextRange(change.Start, change.OldLength)))
                {
                    return slot;
                }
            }

            return null;
        }

        private static bool TouchesTrustedContent(List<TextRange> ranges, TextChange change)
        {
            if (change.OldLength == 0)
            {
                return ContainsInsertionPoint(ranges, change.Start);
            }

            return IntersectsAny(ranges, change.Start, change.OldLength);
        }

        private static TextRange ExpandChangeToSentenceRange(string text, TextChange change)
        {
            List<SentenceSegment> segments = SentenceSegmentation.Split(text);
            if (segments.Count == 0)
            {
                return default;
            }

            int probeStart = Math.Max(0, change.Start);
            int probeEnd = Math.Max(probeStart, change.Start + Math.Max(change.NewLength, 1));
            int mergedStart = int.MaxValue;
            int mergedEnd = -1;

            foreach (SentenceSegment segment in segments)
            {
                int segmentEnd = segment.Start + segment.Length;
                bool intersects = change.NewLength == 0
                    ? probeStart >= segment.Start && probeStart <= segmentEnd
                    : probeStart < segmentEnd && probeEnd > segment.Start;
                if (!intersects)
                {
                    continue;
                }

                mergedStart = Math.Min(mergedStart, segment.Start);
                mergedEnd = Math.Max(mergedEnd, segmentEnd);
            }

            if (mergedEnd > mergedStart)
            {
                return new TextRange(mergedStart, mergedEnd - mergedStart);
            }

            SentenceSegment fallback = segments[Math.Min(segments.Count - 1, FindClosestSegmentIndex(segments, probeStart))];
            return new TextRange(fallback.Start, fallback.Length);
        }

        private static TextRange ExpandRangeToLineRange(string text, TextRange range)
        {
            if (string.IsNullOrEmpty(text) || range.Start < 0 || range.Start > text.Length)
            {
                return default;
            }

            int start = Math.Min(range.Start, text.Length);
            while (start > 0 && text[start - 1] != '\r' && text[start - 1] != '\n')
            {
                start--;
            }

            int end = Math.Min(text.Length, range.Start + Math.Max(range.Length, 1));
            while (end < text.Length && text[end] != '\r' && text[end] != '\n')
            {
                end++;
            }

            while (start < end && char.IsWhiteSpace(text[start]) && text[start] != '\r' && text[start] != '\n')
            {
                start++;
            }

            while (end > start && char.IsWhiteSpace(text[end - 1]) && text[end - 1] != '\r' && text[end - 1] != '\n')
            {
                end--;
            }

            return end > start ? new TextRange(start, end - start) : default;
        }

        private static int FindClosestSegmentIndex(List<SentenceSegment> segments, int position)
        {
            for (int i = 0; i < segments.Count; i++)
            {
                SentenceSegment segment = segments[i];
                if (position <= segment.Start + segment.Length)
                {
                    return i;
                }
            }

            return segments.Count - 1;
        }

        private static bool ContainsInsertionPoint(List<TextRange> ranges, int position)
        {
            foreach (TextRange range in ranges)
            {
                int rangeEnd = range.Start + range.Length;
                if (position >= range.Start && position < rangeEnd)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IntersectsAny(List<TextRange> ranges, int start, int length)
        {
            int end = start + length;
            foreach (TextRange range in ranges)
            {
                int rangeEnd = range.Start + range.Length;
                if (rangeEnd <= start)
                {
                    continue;
                }

                if (range.Start >= end)
                {
                    return false;
                }

                return true;
            }

            return false;
        }

        private static bool RangesIntersect(TextRange left, TextRange right)
        {
            int leftEnd = left.Start + left.Length;
            int rightEnd = right.Start + right.Length;
            return leftEnd > right.Start && rightEnd > left.Start;
        }

        private static bool RangeContains(TextRange outer, TextRange inner)
        {
            return inner.Start >= outer.Start &&
                   inner.Start + inner.Length <= outer.Start + outer.Length;
        }

        private static bool IsRangeCoveredByRanges(List<TextRange> ranges, int start, int length)
        {
            if (length <= 0)
            {
                return false;
            }

            int end = start + length;
            int coveredUntil = start;
            foreach (TextRange range in ranges)
            {
                int rangeStart = range.Start;
                int rangeEnd = range.Start + range.Length;
                if (rangeEnd <= coveredUntil)
                {
                    continue;
                }

                if (rangeStart > coveredUntil)
                {
                    return false;
                }

                coveredUntil = Math.Max(coveredUntil, rangeEnd);
                if (coveredUntil >= end)
                {
                    return true;
                }
            }

            return false;
        }

        private static void SubtractRange(List<TextRange> ranges, TextRange removed)
        {
            if (removed.Length <= 0 || ranges.Count == 0)
            {
                return;
            }

            int removeStart = removed.Start;
            int removeEnd = removed.Start + removed.Length;
            var updated = new List<TextRange>(ranges.Count + 2);

            foreach (TextRange range in ranges)
            {
                int rangeStart = range.Start;
                int rangeEnd = range.Start + range.Length;
                if (rangeEnd <= removeStart || rangeStart >= removeEnd)
                {
                    updated.Add(range);
                    continue;
                }

                if (rangeStart < removeStart)
                {
                    updated.Add(new TextRange(rangeStart, removeStart - rangeStart));
                }

                if (rangeEnd > removeEnd)
                {
                    updated.Add(new TextRange(removeEnd, rangeEnd - removeEnd));
                }
            }

            ranges.Clear();
            ranges.AddRange(updated);
        }

        private static void NormalizeRanges(List<TextRange> ranges)
        {
            if (ranges.Count == 0)
            {
                return;
            }

            ranges.RemoveAll(range => range.Length <= 0);
            if (ranges.Count <= 1)
            {
                return;
            }

            ranges.Sort((left, right) => left.Start.CompareTo(right.Start));
            var merged = new List<TextRange>(ranges.Count);
            TextRange current = ranges[0];
            for (int i = 1; i < ranges.Count; i++)
            {
                TextRange next = ranges[i];
                int currentEnd = current.Start + current.Length;
                int nextEnd = next.Start + next.Length;
                if (next.Start <= currentEnd)
                {
                    current = new TextRange(current.Start, Math.Max(currentEnd, nextEnd) - current.Start);
                    continue;
                }

                merged.Add(current);
                current = next;
            }

            merged.Add(current);
            ranges.Clear();
            ranges.AddRange(merged.Where(range => range.Length > 0));
        }

        private static void NormalizeSlots(List<ProofingSlot> currentSlots)
        {
            currentSlots.RemoveAll(slot => slot.Range.Length <= 0);
            currentSlots.Sort((left, right) => left.Range.Start.CompareTo(right.Range.Start));
        }

        private static void MapRanges(
            List<TextRange> ranges,
            int changeStart,
            int changeEnd,
            int delta,
            int newLength)
        {
            for (int i = ranges.Count - 1; i >= 0; i--)
            {
                TextRange range = ranges[i];
                int start = range.Start;
                int end = start + range.Length;

                int newStart = MapPosition(start, changeStart, changeEnd, delta);
                int newEnd = MapPosition(end, changeStart, changeEnd, delta);
                int updatedLength = newEnd - newStart;

                if (updatedLength <= 0 || newStart >= newLength)
                {
                    ranges.RemoveAt(i);
                    continue;
                }

                if (newStart < 0)
                {
                    newStart = 0;
                }

                if (newStart + updatedLength > newLength)
                {
                    updatedLength = newLength - newStart;
                }

                if (updatedLength <= 0)
                {
                    ranges.RemoveAt(i);
                    continue;
                }

                ranges[i] = new TextRange(newStart, updatedLength);
            }
        }

        private static void MapSlots(
            List<ProofingSlot> currentSlots,
            int changeStart,
            int changeEnd,
            int delta,
            int newLength)
        {
            for (int i = currentSlots.Count - 1; i >= 0; i--)
            {
                ProofingSlot slot = currentSlots[i];
                int start = slot.Range.Start;
                int end = start + slot.Range.Length;

                int newStart = MapPosition(start, changeStart, changeEnd, delta);
                int newEnd = MapPosition(end, changeStart, changeEnd, delta);
                int updatedLength = newEnd - newStart;

                if (updatedLength <= 0 || newStart >= newLength)
                {
                    currentSlots.RemoveAt(i);
                    continue;
                }

                if (newStart < 0)
                {
                    newStart = 0;
                }

                if (newStart + updatedLength > newLength)
                {
                    updatedLength = newLength - newStart;
                }

                if (updatedLength <= 0)
                {
                    currentSlots.RemoveAt(i);
                    continue;
                }

                currentSlots[i] = slot with
                {
                    Range = new TextRange(newStart, updatedLength)
                };
            }
        }

        private static int MapPosition(int position, int changeStart, int changeEnd, int delta)
        {
            if (position < changeStart)
            {
                return position;
            }

            if (position >= changeEnd)
            {
                return position + delta;
            }

            return changeStart;
        }

        private static string DescribeChangedFragment(string text, int start, int length)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "\"\"";
            }

            int safeStart = Math.Max(0, Math.Min(start, text.Length));
            int safeLength = Math.Max(0, Math.Min(length, text.Length - safeStart));
            int windowStart = Math.Max(0, safeStart - 24);
            int windowEnd = Math.Min(text.Length, safeStart + Math.Max(safeLength, 1) + 24);
            return "["
                + windowStart.ToString(CultureInfo.InvariantCulture)
                + ":"
                + (windowEnd - windowStart).ToString(CultureInfo.InvariantCulture)
                + "]="
                + FormatTextForLog(text.Substring(windowStart, windowEnd - windowStart), 180);
        }

        private static string DescribeRange(TextRange range)
        {
            return "("
                + range.Start.ToString(CultureInfo.InvariantCulture)
                + ","
                + range.Length.ToString(CultureInfo.InvariantCulture)
                + ")";
        }

        private static string DescribeRanges(IEnumerable<TextRange> ranges)
        {
            var list = ranges.ToList();
            if (list.Count == 0)
            {
                return "[]";
            }

            return "["
                + string.Join(", ", list.Select(DescribeRange))
                + "]";
        }

        private string DescribeSlots(IEnumerable<ProofingSlot> currentSlots)
        {
            var list = currentSlots.ToList();
            if (list.Count == 0)
            {
                return "[]";
            }

            return "["
                + string.Join(", ", list.Select(DescribeSlot))
                + "]";
        }

        private string DescribeSlot(ProofingSlot slot)
        {
            string slotText = string.Empty;
            if (slot.Range.Start >= 0 &&
                slot.Range.Length > 0 &&
                slot.Range.Start + slot.Range.Length <= currentText.Length)
            {
                slotText = currentText.Substring(slot.Range.Start, slot.Range.Length);
            }

            TextRange lineRange = ExpandRangeToLineRange(currentText, slot.Range);
            string lineText = string.Empty;
            if (lineRange.Length > 0 &&
                lineRange.Start >= 0 &&
                lineRange.Start + lineRange.Length <= currentText.Length)
            {
                lineText = currentText.Substring(lineRange.Start, lineRange.Length);
            }

            return "Id=" + slot.Id.ToString("N")
                + " Range=" + DescribeRange(slot.Range)
                + " Policy=" + slot.Policy
                + " Protected=" + slot.IsProtected
                + " Text=" + FormatTextForLog(slotText)
                + " Line=" + FormatTextForLog(lineText);
        }

        private string DescribeNullableSlot(ProofingSlot? slot)
        {
            return slot is ProofingSlot value ? DescribeSlot(value) : "none";
        }

        private static string DescribeSegment(SentenceSegment segment)
        {
            return "Start=" + segment.Start.ToString(CultureInfo.InvariantCulture)
                + " Length=" + segment.Length.ToString(CultureInfo.InvariantCulture)
                + " Text=" + FormatTextForLog(segment.Text);
        }

        private static string DescribeUnits(IEnumerable<ProofingUnit> units)
        {
            var list = units.ToList();
            if (list.Count == 0)
            {
                return "[]";
            }

            return "["
                + string.Join(", ", list.Select(unit =>
                    unit.Kind
                    + "@"
                    + unit.Start.ToString(CultureInfo.InvariantCulture)
                    + "+"
                    + unit.Length.ToString(CultureInfo.InvariantCulture)
                    + "="
                    + FormatTextForLog(unit.Text)))
                + "]";
        }

        private static string DescribeDebugLines(IEnumerable<string> debugLines)
        {
            var list = debugLines.ToList();
            if (list.Count == 0)
            {
                return "[]";
            }

            return "[" + string.Join(" || ", list) + "]";
        }

        private static string FormatTextForLog(string? text, int maxLength = 120)
        {
            string normalized = (text ?? string.Empty)
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
            if (normalized.Length <= maxLength)
            {
                return normalized;
            }

            return normalized.Substring(0, maxLength) + "...";
        }

        private static TextChange ComputeTextChange(string oldText, string newText)
        {
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

            return new TextChange(
                prefix,
                oldText.Length - prefix - suffix,
                newText.Length - prefix - suffix);
        }

        private readonly record struct TextChange(int Start, int OldLength, int NewLength);
    }
}
