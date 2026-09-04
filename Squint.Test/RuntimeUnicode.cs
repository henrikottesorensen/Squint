// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

using Squint.Uts39;

namespace Squint.Test;

/// <summary>
/// What the runtime's normaliser knows, so that a comparison against it can leave out what it
/// does not.
/// </summary>
/// <remarks>
/// <para>
/// <c>string.Normalize</c> is the machine's ICU on Linux and macOS and the operating system's on
/// Windows, each at whatever Unicode version that build shipped with, and it need not match
/// <c>CharUnicodeInfo</c>, which is managed code at the runtime's own version. A character the
/// category tables know but the normaliser predates would be reported as a disagreement, and
/// there is one such character for every release the normaliser is behind.
/// </para>
/// <para>
/// So the normaliser's version is measured rather than assumed: for each Unicode version, one
/// character of that age with a canonical decomposition is normalised, and the newest version
/// whose sentinel decomposes is the version the normaliser has. Characters younger than that
/// are then left out of the comparison. The ages come from <c>DerivedAge.txt</c> in the
/// repository, read here rather than shipped in the library, since only this test needs them.
/// </para>
/// </remarks>
internal static class RuntimeUnicode
{
    private static readonly Lazy<(SortedDictionary<Version, List<(int start, int end)>> ranges, Version normalizer)> State = new Lazy<(SortedDictionary<Version, List<(int start, int end)>> ranges, Version normalizer)>(Measure);

    /// <summary>
    /// The Unicode version the runtime's normaliser behaves as, measured.
    /// </summary>
    internal static Version NormalizerVersion => State.Value.normalizer;

    /// <summary>
    /// Whether the runtime's normaliser is expected to know the code point: assigned by the
    /// category tables, and no younger than the normaliser.
    /// </summary>
    internal static bool NormalizerKnows(int codePoint)
    {
        string text = char.ConvertFromUtf32(codePoint);

        if (CharUnicodeInfo.GetUnicodeCategory(text, 0) == UnicodeCategory.OtherNotAssigned)
        {
            return false;
        }

        Version? age = AgeOf(codePoint);
        return age is not null && age <= State.Value.normalizer;
    }

    private static Version? AgeOf(int codePoint)
    {
        foreach (KeyValuePair<Version, List<(int start, int end)>> version in State.Value.ranges)
        {
            foreach ((int start, int end) in version.Value)
            {
                if (codePoint >= start && codePoint <= end)
                {
                    return version.Key;
                }
            }
        }

        return null;
    }

    private static (SortedDictionary<Version, List<(int start, int end)>> ranges, Version normalizer) Measure()
    {
        SortedDictionary<Version, List<(int start, int end)>> ranges = new SortedDictionary<Version, List<(int start, int end)>>();
        string root = RepositoryRoot();

        foreach (string raw in File.ReadLines(Path.Combine(root, "ucd", "DerivedAge.txt")))
        {
            string line = raw;
            int hash = line.IndexOf('#', StringComparison.Ordinal);

            if (hash >= 0)
            {
                line = line.Substring(0, hash);
            }

            if (line.Trim().Length == 0)
            {
                continue;
            }

            string[] fields = line.Split(';');
            string range = fields[0].Trim();
            Version age = Version.Parse(fields[1].Trim());
            int dots = range.IndexOf("..", StringComparison.Ordinal);
            int start = int.Parse(dots >= 0 ? range.Substring(0, dots) : range, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            int end = dots >= 0 ? int.Parse(range.Substring(dots + 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) : start;

            if (!ranges.TryGetValue(age, out List<(int start, int end)>? list))
            {
                list = new List<(int start, int end)>();
                ranges[age] = list;
            }

            list.Add((start, end));
        }

        // The newest version whose sentinel the normaliser decomposes. A version with no
        // decomposable character of its own (17.0 added none) is credited if the category tables
        // know a character of that age, which is the runtime's own version and the best available
        // evidence.
        Version normalizer = new Version(1, 1);

        foreach (KeyValuePair<Version, List<(int start, int end)>> version in ranges)
        {
            bool? decomposes = SentinelDecomposes(version.Value);

            if (decomposes == true || (decomposes is null && CategoryKnows(version.Value)))
            {
                normalizer = version.Key;
            }
            else
            {
                break;
            }
        }

        return (ranges, normalizer);
    }

    private static bool? SentinelDecomposes(List<(int start, int end)> ranges)
    {
        foreach ((int start, int end) in ranges)
        {
            for (int codePoint = start; codePoint <= end; codePoint++)
            {
                if (codePoint >= 0xD800 && codePoint <= 0xDFFF)
                {
                    continue;
                }

                string text = char.ConvertFromUtf32(codePoint);

                if (string.Equals(Normalization.Nfd(text), text, StringComparison.Ordinal))
                {
                    continue;
                }

                return !string.Equals(text.Normalize(NormalizationForm.FormD), text, StringComparison.Ordinal);
            }
        }

        return null;
    }

    private static bool CategoryKnows(List<(int start, int end)> ranges)
    {
        foreach ((int start, int end) in ranges)
        {
            for (int codePoint = start; codePoint <= end; codePoint++)
            {
                if (codePoint >= 0xD800 && codePoint <= 0xDFFF)
                {
                    continue;
                }

                return CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(codePoint), 0) != UnicodeCategory.OtherNotAssigned;
            }
        }

        return false;
    }

    private static string RepositoryRoot()
    {
        string? directory = AppContext.BaseDirectory;

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "Squint.slnx")))
            {
                return directory;
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new InvalidOperationException("The test binary is not inside the repository.");
    }
}
