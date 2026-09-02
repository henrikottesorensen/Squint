// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Text;

namespace Squint;

/// <summary>
/// The four Unicode normalization forms, from the library's own tables.
/// </summary>
/// <remarks>
/// <para>
/// The runtime's <c>string.Normalize</c> is not used anywhere in this library, on purpose. On
/// Linux and macOS it is the machine's ICU, on Windows the operating system's, and each carries
/// whatever Unicode version that build shipped with; in globalization-invariant mode it returns
/// non-ASCII text unchanged. A form computed here is the same on every one of them, and it is
/// computed from the same Unicode version as the confusable and identifier tables it sits
/// beside, so an identifier canonicalised with <see cref="Nfkc"/> and checked with the rest of
/// the library has been through one version of the data, not two.
/// </para>
/// <para>
/// Decomposition is a table lookup: the generator has already expanded every mapping in full,
/// so nothing recurses here. Hangul syllables decompose and compose arithmetically (Unicode
/// chapter 3, section 3.12). Composition is the canonical composition algorithm of UAX #15,
/// over a pair table of the primary composites.
/// </para>
/// </remarks>
public static class Normalization
{
    private const int HangulBase = 0xAC00;
    private const int HangulCount = 11172;
    private const int LeadBase = 0x1100;
    private const int LeadCount = 19;
    private const int VowelBase = 0x1161;
    private const int VowelCount = 21;
    private const int TrailBase = 0x11A7;
    private const int TrailCount = 28;

    /// <summary>
    /// The first code point with a canonical decomposition or a non-zero combining class. Text
    /// below it is in NFD and NFC already.
    /// </summary>
    private const int FirstCanonical = 0xC0;

    /// <summary>
    /// The first code point with a compatibility decomposition: U+00A0 NO-BREAK SPACE, which
    /// NFKD turns into a space. Text below it is in every form already.
    /// </summary>
    private const int FirstCompatibility = 0xA0;

    /// <summary>
    /// Normalization Form D: canonical decomposition.
    /// </summary>
    /// <exception cref="ArgumentNullException">The text is null.</exception>
    public static string Nfd(string text)
    {
        return Normalize(text, false, false);
    }

    /// <summary>
    /// Normalization Form C: canonical decomposition followed by canonical composition. The
    /// form browsers send and most text is stored in.
    /// </summary>
    /// <exception cref="ArgumentNullException">The text is null.</exception>
    public static string Nfc(string text)
    {
        return Normalize(text, false, true);
    }

    /// <summary>
    /// Normalization Form KD: compatibility decomposition.
    /// </summary>
    /// <exception cref="ArgumentNullException">The text is null.</exception>
    public static string Nfkd(string text)
    {
        return Normalize(text, true, false);
    }

    /// <summary>
    /// Normalization Form KC: compatibility decomposition followed by canonical composition.
    /// The form UAX #31 and UTS #39 compare identifiers in: ligatures, fullwidth forms,
    /// superscripts and the mathematical alphabets fold to the plain letters, so it is for
    /// identity, not display.
    /// </summary>
    /// <exception cref="ArgumentNullException">The text is null.</exception>
    public static string Nfkc(string text)
    {
        return Normalize(text, true, true);
    }

    /// <summary>
    /// The Canonical_Combining_Class of a code point.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Not a code point.</exception>
    public static int CombiningClass(int codePoint)
    {
        CodePoints.Validate(codePoint, nameof(codePoint));
        return CombiningClassUnchecked(codePoint);
    }

    private static string Normalize(string text, bool compatibility, bool compose)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        int first = compatibility ? FirstCompatibility : FirstCanonical;
        bool plain = true;

        foreach (char c in text)
        {
            if (c >= first)
            {
                plain = false;
                break;
            }
        }

        if (plain)
        {
            return text;
        }

        List<int> codePoints = new List<int>(text.Length + 8);
        int index = 0;

        while (index < text.Length)
        {
            Decompose(CodePoints.Read(text, ref index), compatibility, first, codePoints);
        }

        Reorder(codePoints);

        if (compose)
        {
            Compose(codePoints);
        }

        StringBuilder output = new StringBuilder(codePoints.Count + 4);

        foreach (int codePoint in codePoints)
        {
            CodePoints.Append(output, codePoint);
        }

        return output.ToString();
    }

    private static int CombiningClassUnchecked(int codePoint)
    {
        int run = Tables.FindRun(NormalizationData.CombiningClassStarts, codePoint);
        return run < 0 ? 0 : NormalizationData.CombiningClassValues[run];
    }

    private static void Decompose(int codePoint, bool compatibility, int first, List<int> output)
    {
        if (codePoint < first)
        {
            output.Add(codePoint);
            return;
        }

        int syllable = codePoint - HangulBase;

        if (syllable >= 0 && syllable < HangulCount)
        {
            output.Add(LeadBase + (syllable / (VowelCount * TrailCount)));
            output.Add(VowelBase + ((syllable % (VowelCount * TrailCount)) / TrailCount));
            int trail = syllable % TrailCount;

            if (trail != 0)
            {
                output.Add(TrailBase + trail);
            }

            return;
        }

        if (compatibility)
        {
            int compatibilityKey = Tables.FindKey(NormalizationData.CompatibilityKeys, codePoint);

            if (compatibilityKey >= 0)
            {
                int packed = NormalizationData.CompatibilityValues[compatibilityKey];
                AppendRange(NormalizationData.CompatibilityDecompositions, packed >> 6, packed & 0x3F, output);
                return;
            }
        }

        int key = Tables.FindKey(NormalizationData.DecompositionKeys, codePoint);

        if (key < 0)
        {
            output.Add(codePoint);
            return;
        }

        int value = NormalizationData.DecompositionValues[key];
        AppendRange(NormalizationData.Decompositions, value >> 4, value & 0xF, output);
    }

    private static void AppendRange(string blob, int offset, int length, List<int> output)
    {
        int end = offset + length;

        while (offset < end)
        {
            output.Add(CodePoints.Read(blob, ref offset));
        }
    }

    /// <summary>
    /// The Canonical Ordering Algorithm: a stable sort of each run of non-starters by combining
    /// class, done as an insertion sort because runs are short.
    /// </summary>
    private static void Reorder(List<int> codePoints)
    {
        for (int i = 1; i < codePoints.Count; i++)
        {
            int codePoint = codePoints[i];
            int combiningClass = CombiningClassUnchecked(codePoint);

            if (combiningClass == 0)
            {
                continue;
            }

            int j = i;

            while (j > 0)
            {
                int previousClass = CombiningClassUnchecked(codePoints[j - 1]);

                if (previousClass == 0 || previousClass <= combiningClass)
                {
                    break;
                }

                codePoints[j] = codePoints[j - 1];
                j--;
            }

            codePoints[j] = codePoint;
        }
    }

    /// <summary>
    /// The Canonical Composition Algorithm, in place on a decomposed and reordered sequence.
    /// Each character is composed with the last starter unless something blocks it: a character
    /// in between with combining class zero, or with a class not lower than its own.
    /// </summary>
    private static void Compose(List<int> codePoints)
    {
        int starter = -1;
        int write = 0;
        int lastClass = 0;

        for (int read = 0; read < codePoints.Count; read++)
        {
            int codePoint = codePoints[read];
            int combiningClass = CombiningClassUnchecked(codePoint);

            if (starter >= 0)
            {
                bool adjacent = write == starter + 1;
                bool blocked = !adjacent && (lastClass == 0 || lastClass >= combiningClass);

                if (!blocked)
                {
                    int composite = ComposePair(codePoints[starter], codePoint);

                    if (composite >= 0)
                    {
                        codePoints[starter] = composite;
                        continue;
                    }
                }
            }

            if (combiningClass == 0)
            {
                starter = write;
            }

            lastClass = combiningClass;
            codePoints[write++] = codePoint;
        }

        codePoints.RemoveRange(write, codePoints.Count - write);
    }

    /// <summary>
    /// The primary composite of two code points, or -1 when they do not compose.
    /// </summary>
    private static int ComposePair(int first, int second)
    {
        int lead = first - LeadBase;

        if (lead >= 0 && lead < LeadCount)
        {
            int vowel = second - VowelBase;

            if (vowel >= 0 && vowel < VowelCount)
            {
                return HangulBase + (((lead * VowelCount) + vowel) * TrailCount);
            }

            return -1;
        }

        int syllable = first - HangulBase;

        if (syllable >= 0 && syllable < HangulCount && syllable % TrailCount == 0)
        {
            int trail = second - TrailBase;

            if (trail > 0 && trail < TrailCount)
            {
                return first + trail;
            }

            return -1;
        }

        long pair = ((long)first << 21) | (uint)second;
        long[] keys = NormalizationData.PairKeys;
        int low = 0;
        int high = keys.Length - 1;

        while (low <= high)
        {
            int middle = low + ((high - low) / 2);

            if (keys[middle] == pair)
            {
                return NormalizationData.PairComposites[middle];
            }

            if (keys[middle] < pair)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return -1;
    }
}
