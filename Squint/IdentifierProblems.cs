// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;

namespace Squint;

/// <summary>
/// What <see cref="Identifiers.Check(string, RestrictionLevel)"/> found wrong with an identifier.
/// Several can be true at once, so a caller can report all of them.
/// </summary>
[Flags]
public enum IdentifierProblems
{
    /// <summary>
    /// Nothing: the identifier is accepted. A flags enum's zero is "none of these", so this is
    /// <c>None</c> and not the <c>Undefined</c> sentinel the other enums carry.
    /// </summary>
    None = 0,

    /// <summary>
    /// A character is outside the identifier profile, in the input or in its normalized form.
    /// </summary>
    OutsideProfile = 1 << 0,

    /// <summary>
    /// The restriction level of the normalized form is looser than the level permitted.
    /// </summary>
    ExceedsRestrictionLevel = 1 << 1,

    /// <summary>
    /// The normalized form has decimal digits from more than one number system.
    /// </summary>
    MixedNumbers = 1 << 2,
}
