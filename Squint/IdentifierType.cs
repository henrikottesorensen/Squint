// SPDX-License-Identifier: LGPL-2.1-or-later
// Copyright (c) 2026 Henrik O. Sørensen

using System;

namespace Squint;

/// <summary>
/// The Identifier_Type property, UTS #39 section 3.1: why a code point is, or is not, in the
/// General Security Profile for identifiers. A code point can carry more than one.
/// </summary>
[Flags]
public enum IdentifierType
{
    /// <summary>
    /// No types. Never returned for a code point, since every one has at least
    /// <see cref="NotCharacter"/>, but a legitimate result of masking. This is the one enum in
    /// the library without an <c>Undefined</c> sentinel: in a flags enum zero means "none of
    /// these", a value code can compute, so it cannot also mean that nobody chose a value.
    /// </summary>
    None = 0,

    /// <summary>
    /// Unassigned, a surrogate, a private-use or a noncharacter code point.
    /// </summary>
    NotCharacter = 1 << 0,

    /// <summary>
    /// Deprecated in the Unicode Standard.
    /// </summary>
    Deprecated = 1 << 1,

    /// <summary>
    /// Has the Default_Ignorable_Code_Point property: invisible in rendering.
    /// </summary>
    DefaultIgnorable = 1 << 2,

    /// <summary>
    /// Not stable under NFKC.
    /// </summary>
    NotNfkc = 1 << 3,

    /// <summary>
    /// Not an XID_Continue character.
    /// </summary>
    NotXid = 1 << 4,

    /// <summary>
    /// In a script excluded from identifiers by UAX #31.
    /// </summary>
    Exclusion = 1 << 5,

    /// <summary>
    /// Obsolete: no longer in use in a living language, or a historic form.
    /// </summary>
    Obsolete = 1 << 6,

    /// <summary>
    /// Specialised usage: technical, liturgical, phonetic and the like.
    /// </summary>
    Technical = 1 << 7,

    /// <summary>
    /// Uncommon in modern text.
    /// </summary>
    UncommonUse = 1 << 8,

    /// <summary>
    /// In a script of limited modern use, UAX #31 Table 7.
    /// </summary>
    LimitedUse = 1 << 9,

    /// <summary>
    /// Included in the profile specifically, though it is not a letter or digit. Allowed.
    /// </summary>
    Inclusion = 1 << 10,

    /// <summary>
    /// Recommended for use in identifiers. Allowed.
    /// </summary>
    Recommended = 1 << 11,
}
