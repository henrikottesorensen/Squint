// SPDX-License-Identifier: LGPL-2.1-or-later
// Copyright (c) 2026 Henrik O. Sørensen

using System;
using System.Text;

namespace Squint;

/// <summary>
/// Reads and writes code points on UTF-16 strings by hand, which is what netstandard2.0 offers.
/// </summary>
internal static class CodePoints
{
    /// <summary>
    /// The largest code point.
    /// </summary>
    internal const int Maximum = 0x10FFFF;

    /// <summary>
    /// Reads the code point at <paramref name="index"/> and advances past it. A lone surrogate is
    /// returned as its own value rather than failing, so that every string has a defined answer.
    /// </summary>
    internal static int Read(string text, ref int index)
    {
        char first = text[index++];

        if (char.IsHighSurrogate(first) && index < text.Length && char.IsLowSurrogate(text[index]))
        {
            return char.ConvertToUtf32(first, text[index++]);
        }

        return first;
    }

    /// <summary>
    /// Appends a code point as one or two UTF-16 code units.
    /// </summary>
    internal static void Append(StringBuilder output, int codePoint)
    {
        if (codePoint <= 0xFFFF)
        {
            output.Append((char)codePoint);
        }
        else
        {
            output.Append(char.ConvertFromUtf32(codePoint));
        }
    }

    /// <summary>
    /// Rejects a value that is not a code point. Surrogate code points are accepted: they are
    /// code points, with properties of their own, even though no character has them.
    /// </summary>
    internal static void Validate(int codePoint, string parameterName)
    {
        if (codePoint < 0 || codePoint > Maximum)
        {
            throw new ArgumentOutOfRangeException(parameterName, codePoint, "Not a Unicode code point.");
        }
    }
}
