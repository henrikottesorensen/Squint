// SPDX-License-Identifier: LGPL-2.1-or-later
// Copyright (c) 2026 Henrik O. Sørensen

namespace Squint;

/// <summary>
/// How two strings are confusable, per UTS #39 section 4.
/// </summary>
public enum ConfusableClass
{
    /// <summary>
    /// Not a result: the default of an uninitialised value, never returned.
    /// </summary>
    Undefined = 0,

    /// <summary>
    /// The skeletons differ: the strings are not confusable.
    /// </summary>
    NotConfusable = 1,

    /// <summary>
    /// Confusable, and the resolved script sets share a script. Example: <c>ǉeto</c> and
    /// <c>ljeto</c>, both Latin.
    /// </summary>
    SingleScript,

    /// <summary>
    /// Confusable, and the resolved script sets share no script, with at least one string being
    /// mixed-script itself. Example: <c>paypal</c> and <c>pаypаl</c> with two Cyrillic letters.
    /// </summary>
    MixedScript,

    /// <summary>
    /// Confusable, the resolved script sets share no script, and each string is single-script:
    /// the whole word has been rewritten in another script. Example: <c>scope</c> in Latin and
    /// <c>ѕсоре</c> in Cyrillic. Every whole-script confusable is also a mixed-script confusable
    /// in the specification's terms; this value is the more specific one.
    /// </summary>
    WholeScript,
}
