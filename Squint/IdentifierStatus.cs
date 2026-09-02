// SPDX-License-Identifier: LGPL-2.1-or-later
// Copyright (c) 2026 Henrik O. Sørensen

namespace Squint;

/// <summary>
/// The Identifier_Status property, UTS #39 section 3.1: whether a code point is in the General
/// Security Profile for identifiers.
/// </summary>
public enum IdentifierStatus
{
    /// <summary>
    /// Not a status: the default of an uninitialised value, never returned.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// Not in the profile. Every code point whose <see cref="IdentifierType"/> is anything but
    /// <see cref="IdentifierType.Recommended"/> or <see cref="IdentifierType.Inclusion"/>.
    /// </summary>
    Restricted = 1,

    /// <summary>
    /// In the profile.
    /// </summary>
    Allowed,
}
