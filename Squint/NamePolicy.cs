// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace Squint;

/// <summary>
/// How much script mixing a name may have. Four words in place of UTS #39's six restriction
/// levels; each is one of those levels underneath.
/// </summary>
public enum NamePolicy
{
    /// <summary>
    /// Not a policy: the default of an uninitialised value, never accepted.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// ASCII letters, digits and punctuation only. For systems that were never going to accept
    /// anything else and want to say so. UTS #39 ASCII-Only.
    /// </summary>
    Ascii = 1,

    /// <summary>
    /// One script per name, digits and punctuation aside, plus Latin together with Japanese,
    /// Korean or Chinese, which those languages need. The default, and what a household, a team
    /// or a company wants: Søren, Müller, Yıldız and 東京太郎 pass, a Cyrillic letter hidden among
    /// Latin ones does not. UTS #39 Highly Restrictive.
    /// </summary>
    OneScript = 2,

    /// <summary>
    /// <see cref="OneScript"/>, or Latin with one other widely used script except Cyrillic or
    /// Greek, the two whose letters most resemble Latin ones. For a public service with users
    /// who write names like "Amit अमित". UTS #39 Moderately Restrictive.
    /// </summary>
    Relaxed = 3,

    /// <summary>
    /// Any mixture of scripts, as long as every character is one allowed in names at all.
    /// Ωmega and Toys-Я-Us pass. Lookalikes are then caught only by
    /// <see cref="Inspection.LookalikeKey"/> collisions. UTS #39 Minimally Restrictive.
    /// </summary>
    Anything = 4,
}
