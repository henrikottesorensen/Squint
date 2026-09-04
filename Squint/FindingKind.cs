// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace Squint;

/// <summary>
/// What a <see cref="Finding"/> is about. Switch on this to word a message yourself.
/// </summary>
public enum FindingKind
{
    /// <summary>
    /// Not a kind: the default of an uninitialised value, never returned.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// An invisible character: a zero-width joiner or space, a soft hyphen, a variation
    /// selector, a byte order mark. It renders as nothing and makes a second name out of the
    /// same letters.
    /// </summary>
    Invisible = 1,

    /// <summary>
    /// A character from a different script than the rest of the name, beyond what the policy
    /// allows: a Cyrillic letter among Latin ones. The message says which script, and which
    /// ASCII character it looks like when it looks like one. When no single character can be
    /// blamed, one finding covers the whole name.
    /// </summary>
    MixedScripts = 2,

    /// <summary>
    /// A digit from a different number system than the first digit in the name: Arabic-Indic
    /// ١ after an ASCII 1.
    /// </summary>
    MixedDigits = 3,

    /// <summary>
    /// A compatibility form of ordinary letters: a ligature, a fullwidth letter, a superscript
    /// digit, a letter from a mathematical alphabet. The message says what it folds to. Typed
    /// by nobody, so a name with one was pasted or constructed.
    /// </summary>
    CompatibilityForm = 4,

    /// <summary>
    /// A character not allowed in names at all: an emoji, a symbol, a control character, a
    /// letter from a historic or specialised script, an unassigned code point.
    /// </summary>
    NotAllowed = 5,

    /// <summary>
    /// A character outside ASCII, under the <see cref="NamePolicy.Ascii"/> policy only: an
    /// ordinary letter that this system does not take.
    /// </summary>
    NotAscii = 6,
}
