// SPDX-License-Identifier: LGPL-2.1-or-later
// Copyright (c) 2026 Henrik O. Sørensen

using System;
using System.Collections.Generic;
using System.IO;

namespace Squint.Generator;

/// <summary>
/// The three columns of UnicodeData.txt the library needs: canonical combining class, canonical
/// decomposition mapping, and decimal digit value.
/// </summary>
public sealed class UnicodeDataFile
{
    private UnicodeDataFile(
        Dictionary<int, byte> combiningClasses,
        Dictionary<int, int[]> canonicalDecompositions,
        Dictionary<int, int[]> compatibilityDecompositions,
        Dictionary<int, int> decimalDigits)
    {
        CombiningClasses = combiningClasses;
        CanonicalDecompositions = canonicalDecompositions;
        CompatibilityDecompositions = compatibilityDecompositions;
        DecimalDigits = decimalDigits;
    }

    /// <summary>
    /// Every code point with a non-zero canonical combining class.
    /// </summary>
    public IReadOnlyDictionary<int, byte> CombiningClasses { get; }

    /// <summary>
    /// Every code point with a canonical decomposition, one level deep as the file states it.
    /// </summary>
    public IReadOnlyDictionary<int, int[]> CanonicalDecompositions { get; }

    /// <summary>
    /// Every code point with a compatibility decomposition (a tagged mapping), one level deep.
    /// </summary>
    public IReadOnlyDictionary<int, int[]> CompatibilityDecompositions { get; }

    /// <summary>
    /// Every <c>Nd</c> code point and its digit value.
    /// </summary>
    public IReadOnlyDictionary<int, int> DecimalDigits { get; }

    /// <summary>
    /// Reads the file.
    /// </summary>
    public static UnicodeDataFile Load(string path)
    {
        Dictionary<int, byte> combiningClasses = new Dictionary<int, byte>();
        Dictionary<int, int[]> decompositions = new Dictionary<int, int[]>();
        Dictionary<int, int[]> compatibility = new Dictionary<int, int[]>();
        Dictionary<int, int> digits = new Dictionary<int, int>();

        foreach (string line in File.ReadLines(path))
        {
            string[] fields = line.Split(';');

            if (fields.Length < 15)
            {
                throw new InvalidDataException($"UnicodeData.txt line has {fields.Length} fields: {line}");
            }

            // Range entries (<CJK Ideograph, First> .. <..., Last>) carry none of the three
            // properties, so reading only the first code point of each is enough.
            int cp = UcdFile.ParseHex(fields[0]);
            string category = fields[2];
            string combiningClass = fields[3];
            string decomposition = fields[5];
            string decimalValue = fields[6];

            byte ccc = byte.Parse(combiningClass, System.Globalization.CultureInfo.InvariantCulture);

            if (ccc != 0)
            {
                combiningClasses[cp] = ccc;
            }

            if (decomposition.Length > 0)
            {
                if (decomposition.StartsWith("<", StringComparison.Ordinal))
                {
                    compatibility[cp] = UcdFile.ParseHexList(decomposition.Substring(decomposition.IndexOf('>') + 1));
                }
                else
                {
                    decompositions[cp] = UcdFile.ParseHexList(decomposition);
                }
            }

            if (string.Equals(category, "Nd", StringComparison.Ordinal))
            {
                if (decimalValue.Length == 0)
                {
                    throw new InvalidDataException($"Nd code point {cp:X4} has no decimal digit value.");
                }

                digits[cp] = int.Parse(decimalValue, System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return new UnicodeDataFile(combiningClasses, decompositions, compatibility, digits);
    }
}
