// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;

using Squint.Uts39;

namespace Squint;

/// <summary>
/// Do two strings look alike, and how to catch a lookalike of something already taken.
/// </summary>
/// <remarks>
/// The key is UTS #39's skeleton of the NFKC form, so a ligature or a fullwidth letter is
/// folded before the visual comparison. It is not for display, and not stable across Unicode
/// versions: store <see cref="UnicodeData.Version"/> beside it and recompute when that changes.
/// </remarks>
public static class Lookalikes
{
    /// <summary>
    /// Whether the two strings look alike: <c>paypal</c> and <c>pаypаl</c> with Cyrillic
    /// letters, <c>modern</c> and <c>rnodern</c>, <c>1ove</c> and <c>love</c>.
    /// </summary>
    /// <exception cref="ArgumentNullException">Either string is null.</exception>
    public static bool Match(string first, string second)
    {
        return string.Equals(Key(first), Key(second), StringComparison.Ordinal);
    }

    /// <summary>
    /// A key two strings share exactly when they look alike. Index it beside every stored name,
    /// and treat a new name whose key is already there as taken.
    /// </summary>
    /// <exception cref="ArgumentNullException">The text is null.</exception>
    public static string Key(string text)
    {
        return Confusables.Skeleton(Normalization.Nfkc(text));
    }
}
