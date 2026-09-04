// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace Squint;

/// <summary>
/// One thing wrong with a name: what, where, and a sentence saying so.
/// </summary>
public sealed class Finding
{
    internal Finding(FindingKind kind, int position, int length, string text, string message)
    {
        Kind = kind;
        Position = position;
        Length = length;
        Text = text;
        Message = message;
    }

    /// <summary>
    /// What the finding is about.
    /// </summary>
    public FindingKind Kind { get; }

    /// <summary>
    /// Where in the input it starts, as a UTF-16 index into the string as given, so it can be
    /// highlighted with <c>Substring</c>.
    /// </summary>
    public int Position { get; }

    /// <summary>
    /// How many UTF-16 code units it covers: one or two for a single character, more when a
    /// finding covers the whole name.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// The offending text itself.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// One English sentence, without a full stop, for an error message. Word your own from
    /// <see cref="Kind"/> and <see cref="Position"/> when you need another language.
    /// </summary>
    public string Message { get; }

    /// <inheritdoc/>
    public override string ToString()
    {
        return Message;
    }
}
