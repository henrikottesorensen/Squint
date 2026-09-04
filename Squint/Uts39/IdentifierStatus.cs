// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace Squint.Uts39;

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
