// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Collections.Generic;

namespace Squint;

/// <summary>
/// The outcome of <see cref="Identifiers.Check(string, RestrictionLevel)"/>: the verdict, and
/// the intermediate forms a caller stores or reports.
/// </summary>
public sealed class IdentifierCheck
{
    internal IdentifierCheck(
        string input,
        string normalized,
        string skeleton,
        RestrictionLevel level,
        IReadOnlyList<int> numberSystems,
        IdentifierProblems problems)
    {
        Input = input;
        Normalized = normalized;
        Skeleton = skeleton;
        Level = level;
        NumberSystems = numberSystems;
        Problems = problems;
    }

    /// <summary>
    /// The text as given.
    /// </summary>
    public string Input { get; }

    /// <summary>
    /// The NFKC form of the input: the identifier's identity, the form to store and compare.
    /// </summary>
    public string Normalized { get; }

    /// <summary>
    /// The skeleton of the normalized form. Two identifiers whose skeletons are equal are
    /// confusable; store it beside <see cref="Normalized"/>, together with
    /// <see cref="UnicodeData.Version"/>, and recompute it when that version changes.
    /// </summary>
    public string Skeleton { get; }

    /// <summary>
    /// The restriction level of the normalized form.
    /// </summary>
    public RestrictionLevel Level { get; }

    /// <summary>
    /// The number systems of the normalized form's decimal digits, each as its zero.
    /// </summary>
    public IReadOnlyList<int> NumberSystems { get; }

    /// <summary>
    /// Everything found wrong, or <see cref="IdentifierProblems.None"/>.
    /// </summary>
    public IdentifierProblems Problems { get; }

    /// <summary>
    /// Whether nothing was found wrong.
    /// </summary>
    public bool IsAccepted => Problems == IdentifierProblems.None;
}
