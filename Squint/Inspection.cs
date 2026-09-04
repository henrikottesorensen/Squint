// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Collections.Generic;

using Squint.Uts39;

namespace Squint;

/// <summary>
/// What <see cref="Names.Inspect(string, NamePolicy)"/> found: the verdict, every finding, and
/// the two strings to keep.
/// </summary>
public sealed class Inspection
{
    internal Inspection(string input, NamePolicy policy, IdentifierCheck check, IReadOnlyList<Finding> findings)
    {
        Input = input;
        Policy = policy;
        CleanForm = check.Normalized;
        LookalikeKey = check.Skeleton;
        Level = check.Level;
        Findings = findings;
    }

    /// <summary>
    /// The name as given.
    /// </summary>
    public string Input { get; }

    /// <summary>
    /// The policy it was judged against.
    /// </summary>
    public NamePolicy Policy { get; }

    /// <summary>
    /// Whether nothing was found wrong. True exactly when <see cref="Findings"/> is empty.
    /// </summary>
    public bool IsAcceptable => Findings.Count == 0;

    /// <summary>
    /// Everything found wrong, in order of position. Empty for an acceptable name.
    /// </summary>
    public IReadOnlyList<Finding> Findings { get; }

    /// <summary>
    /// The name in its canonical form, the one to store and to compare for equality. Usually
    /// the input itself; differs when the input carried a decomposed accent or a compatibility
    /// form.
    /// </summary>
    public string CleanForm { get; }

    /// <summary>
    /// A key two names share exactly when they look alike. Index it, and treat a new name whose
    /// key matches an existing one as taken. Not for display, and not stable across Unicode
    /// versions: store <see cref="UnicodeData.Version"/> beside it and recompute when that
    /// changes.
    /// </summary>
    public string LookalikeKey { get; }

    /// <summary>
    /// The UTS #39 restriction level the name has, for anyone who wants the expert answer.
    /// </summary>
    public RestrictionLevel Level { get; }
}
