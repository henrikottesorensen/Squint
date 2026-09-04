// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace Squint.Uts39;

/// <summary>
/// The restriction levels of UTS #39 section 5.2, numbered as the specification numbers them.
/// A greater value is a looser level, so <c>level &lt;= permitted</c> is the acceptance test.
/// </summary>
public enum RestrictionLevel
{
    /// <summary>
    /// Not a level: the default of an uninitialised value, never returned.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// Every character is ASCII.
    /// </summary>
    AsciiOnly = 1,

    /// <summary>
    /// Single-script: the resolved script set is not empty. Common and Inherited characters
    /// count as every script, so digits and punctuation do not break it.
    /// </summary>
    SingleScript = 2,

    /// <summary>
    /// Single-script, or Latin with one East Asian writing system: Japanese, Korean, or Han with
    /// Bopomofo.
    /// </summary>
    HighlyRestrictive = 3,

    /// <summary>
    /// Highly Restrictive, or Latin with any one other Recommended script except Cyrillic or Greek.
    /// </summary>
    ModeratelyRestrictive = 4,

    /// <summary>
    /// Any mixture of scripts, every character being in the identifier profile.
    /// </summary>
    MinimallyRestrictive = 5,

    /// <summary>
    /// A character is outside the identifier profile.
    /// </summary>
    Unrestricted = 6,
}
