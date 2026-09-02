// SPDX-License-Identifier: LGPL-2.1-or-later
// Copyright (c) 2026 Henrik O. Sørensen

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Squint.Generator;

/// <summary>
/// Reads the semicolon-separated, hash-commented format every file under <c>ucd/</c> shares.
/// </summary>
public static class UcdFile
{
    private static readonly char[] Blanks = { ' ', '\t' };

    /// <summary>
    /// Reads every data line of the file, skipping blank lines and comments.
    /// </summary>
    public static IEnumerable<UcdLine> Read(string path)
    {
        foreach (string raw in File.ReadLines(path))
        {
            string line = raw;
            int hash = line.IndexOf('#');

            if (hash >= 0)
            {
                line = line.Substring(0, hash);
            }

            if (line.Trim().Length == 0)
            {
                continue;
            }

            string[] parts = line.Split(';');
            string range = parts[0].Trim();
            int start;
            int end;
            int dots = range.IndexOf("..", StringComparison.Ordinal);

            if (dots >= 0)
            {
                start = ParseHex(range.Substring(0, dots));
                end = ParseHex(range.Substring(dots + 2));
            }
            else
            {
                start = ParseHex(range);
                end = start;
            }

            string[] fields = new string[parts.Length - 1];

            for (int i = 1; i < parts.Length; i++)
            {
                fields[i - 1] = parts[i].Trim();
            }

            yield return new UcdLine(start, end, fields);
        }
    }

    /// <summary>
    /// Reads the version stamped in the file's header comment, from a line such as
    /// <c># Scripts-17.0.0.txt</c> or <c># Version: 17.0.0</c>.
    /// </summary>
    public static string ReadVersion(string path)
    {
        foreach (string line in File.ReadLines(path))
        {
            if (!line.StartsWith("#", StringComparison.Ordinal))
            {
                break;
            }

            const string versionPrefix = "# Version: ";

            if (line.StartsWith(versionPrefix, StringComparison.Ordinal))
            {
                return line.Substring(versionPrefix.Length).Trim();
            }

            string name = Path.GetFileNameWithoutExtension(path);
            string filePrefix = "# " + name + "-";

            if (line.StartsWith(filePrefix, StringComparison.Ordinal) && line.EndsWith(".txt", StringComparison.Ordinal))
            {
                return line.Substring(filePrefix.Length, line.Length - filePrefix.Length - ".txt".Length);
            }
        }

        throw new InvalidDataException($"No version header in {path}.");
    }

    /// <summary>
    /// Parses a hexadecimal code point.
    /// </summary>
    public static int ParseHex(string text)
    {
        return int.Parse(text.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses a space-separated list of hexadecimal code points.
    /// </summary>
    public static int[] ParseHexList(string text)
    {
        string[] parts = text.Split(Blanks, StringSplitOptions.RemoveEmptyEntries);
        int[] result = new int[parts.Length];

        for (int i = 0; i < parts.Length; i++)
        {
            result[i] = ParseHex(parts[i]);
        }

        return result;
    }
}
