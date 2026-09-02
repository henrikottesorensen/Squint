// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Collections.Generic;

namespace Squint.Generator;

/// <summary>
/// One data line of a Unicode Character Database file: a code point or range, then fields.
/// </summary>
public sealed class UcdLine
{
    internal UcdLine(int start, int end, string[] fields)
    {
        Start = start;
        End = end;
        Fields = fields;
    }

    /// <summary>
    /// First code point of the range.
    /// </summary>
    public int Start { get; }

    /// <summary>
    /// Last code point of the range, inclusive. Equal to <see cref="Start"/> for a single code point.
    /// </summary>
    public int End { get; }

    /// <summary>
    /// The fields after the code point, trimmed, without the comment.
    /// </summary>
    public IReadOnlyList<string> Fields { get; }
}
