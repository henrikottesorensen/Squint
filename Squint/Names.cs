// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Globalization;

using Squint.Uts39;

namespace Squint;

/// <summary>
/// Usernames, handles and labels, in plain terms: is this name acceptable, and if not, what is
/// wrong with it and where.
/// </summary>
/// <remarks>
/// This is the layer for a caller who has not read UTS #39. It runs
/// <see cref="Identifiers.Check(string, RestrictionLevel)"/> underneath and turns its verdict
/// into findings with positions and sentences. Nothing here is syntax: length, first character
/// and reserved words stay the caller's rules, applied to <see cref="Inspection.CleanForm"/>.
/// </remarks>
public static class Names
{
    /// <summary>
    /// Whether the name is acceptable under the policy.
    /// </summary>
    /// <exception cref="ArgumentNullException">The name is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The policy is <see cref="NamePolicy.Undefined"/>.</exception>
    public static bool IsAcceptable(string name, NamePolicy policy = NamePolicy.OneScript)
    {
        return Inspect(name, policy).IsAcceptable;
    }

    /// <summary>
    /// Everything about the name: whether it is acceptable, every finding with its position and
    /// a sentence, the form to store, and the key that catches lookalikes.
    /// </summary>
    /// <exception cref="ArgumentNullException">The name is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The policy is <see cref="NamePolicy.Undefined"/>.</exception>
    public static Inspection Inspect(string name, NamePolicy policy = NamePolicy.OneScript)
    {
        if (name is null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        RestrictionLevel permitted = policy switch
        {
            NamePolicy.Ascii => RestrictionLevel.AsciiOnly,
            NamePolicy.OneScript => RestrictionLevel.HighlyRestrictive,
            NamePolicy.Relaxed => RestrictionLevel.ModeratelyRestrictive,
            NamePolicy.Anything => RestrictionLevel.MinimallyRestrictive,
            _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Not a policy."),
        };

        IdentifierCheck check = Identifiers.Check(name, permitted);
        List<Finding> findings = new List<Finding>();

        if (check.Problems.HasFlag(IdentifierProblems.OutsideProfile))
        {
            FindCharactersOutsideTheProfile(name, findings);
        }

        // The level exceeds the policy for one of two reasons: the profile, already reported
        // character by character above, or the scripts, reported here.
        if (check.Level != RestrictionLevel.Unrestricted && check.Level > permitted)
        {
            if (policy == NamePolicy.Ascii)
            {
                FindNonAscii(name, findings);
            }
            else
            {
                FindMixedScripts(name, policy, findings);
            }
        }

        if (check.Problems.HasFlag(IdentifierProblems.MixedNumbers))
        {
            FindMixedDigits(name, findings);
        }

        findings.Sort((left, right) => left.Position.CompareTo(right.Position));
        return new Inspection(name, policy, check, findings);
    }

    /// <summary>
    /// The name in its canonical form, the one to store and to compare for equality: NFKC.
    /// </summary>
    /// <exception cref="ArgumentNullException">The name is null.</exception>
    public static string Clean(string name)
    {
        return Normalization.Nfkc(name);
    }

    private static void FindCharactersOutsideTheProfile(string name, List<Finding> findings)
    {
        int index = 0;

        while (index < name.Length)
        {
            int position = index;
            int codePoint = CodePoints.Read(name, ref index);

            if (Identifiers.StatusOf(codePoint) == IdentifierStatus.Allowed)
            {
                continue;
            }

            string text = name.Substring(position, index - position);

            if (Confusables.IsDefaultIgnorable(codePoint))
            {
                findings.Add(new Finding(FindingKind.Invisible, position, text.Length, text, $"An invisible character ({Describe(codePoint)}) at position {position}"));
                continue;
            }

            string folded = Normalization.Nfkc(text);

            if (!string.Equals(folded, text, StringComparison.Ordinal))
            {
                findings.Add(new Finding(FindingKind.CompatibilityForm, position, text.Length, text, $"'{text}' ({Describe(codePoint)}) at position {position} is a compatibility form of '{folded}'"));
                continue;
            }

            findings.Add(new Finding(FindingKind.NotAllowed, position, text.Length, text, $"'{text}' ({Describe(codePoint)}) at position {position} is not allowed in a name"));
        }
    }

    private static void FindNonAscii(string name, List<Finding> findings)
    {
        int index = 0;

        while (index < name.Length)
        {
            int position = index;
            int codePoint = CodePoints.Read(name, ref index);

            if (codePoint <= 0x7F || Identifiers.StatusOf(codePoint) != IdentifierStatus.Allowed)
            {
                continue;
            }

            string text = name.Substring(position, index - position);
            findings.Add(new Finding(FindingKind.NotAscii, position, text.Length, text, $"'{text}' ({Describe(codePoint)}) at position {position} is not ASCII"));
        }
    }

    /// <summary>
    /// The script most characters belong to is the name's own; every character that does not
    /// share it is the foreigner. Characters that count as every script, digits and
    /// punctuation, belong to any of them and are never blamed.
    /// </summary>
    private static void FindMixedScripts(string name, NamePolicy policy, List<Finding> findings)
    {
        Dictionary<UnicodeScript, int> votes = new Dictionary<UnicodeScript, int>();
        int index = 0;

        while (index < name.Length)
        {
            int codePoint = CodePoints.Read(name, ref index);
            ScriptSet augmented = Scripts.AugmentedSetOf(codePoint);

            if (augmented.IsAll)
            {
                continue;
            }

            UnicodeScript script = Scripts.Of(codePoint);
            votes.TryGetValue(script, out int count);
            votes[script] = count + 1;
        }

        UnicodeScript main = UnicodeScript.Undefined;
        int best = 0;

        foreach (KeyValuePair<UnicodeScript, int> vote in votes)
        {
            if (vote.Value > best)
            {
                main = vote.Key;
                best = vote.Value;
            }
        }

        int before = findings.Count;
        index = 0;

        while (main != UnicodeScript.Undefined && index < name.Length)
        {
            int position = index;
            int codePoint = CodePoints.Read(name, ref index);
            ScriptSet augmented = Scripts.AugmentedSetOf(codePoint);

            if (augmented.IsAll || augmented.Contains(main))
            {
                continue;
            }

            if (Identifiers.StatusOf(codePoint) != IdentifierStatus.Allowed)
            {
                // Already reported as not allowed; a second finding would say less.
                continue;
            }

            string text = name.Substring(position, index - position);
            string message = $"'{text}' ({Describe(codePoint)}) at position {position} is {ScriptName(Scripts.Of(codePoint))} among {ScriptName(main)} letters";
            string lookalike = Confusables.Skeleton(text);

            if (!string.Equals(lookalike, text, StringComparison.Ordinal) && IsAscii(lookalike) && lookalike.Length > 0)
            {
                message += $" and looks like '{lookalike}'";
            }

            findings.Add(new Finding(FindingKind.MixedScripts, position, text.Length, text, message));
        }

        if (findings.Count == before)
        {
            // Should not happen: a level above the policy means the resolved script set is
            // empty, so some allowed character lacks the main script and was reported above.
            // Kept so that an acceptable name is always one with no findings, whatever the data.
            string scripts = string.Join(", ", ScriptNames(votes.Keys));
            findings.Add(new Finding(FindingKind.MixedScripts, 0, name.Length, name, $"The name mixes {scripts}, which the {PolicyName(policy)} policy does not allow"));
        }
    }

    private static void FindMixedDigits(string name, List<Finding> findings)
    {
        int firstZero = -1;
        int index = 0;

        while (index < name.Length)
        {
            int position = index;
            int codePoint = CodePoints.Read(name, ref index);
            int? value = Identifiers.DecimalDigitValue(codePoint);

            if (value is null)
            {
                continue;
            }

            int zero = codePoint - value.Value;

            if (firstZero < 0)
            {
                firstZero = zero;
                continue;
            }

            if (zero != firstZero)
            {
                string text = name.Substring(position, index - position);
                findings.Add(new Finding(FindingKind.MixedDigits, position, text.Length, text, $"'{text}' ({Describe(codePoint)}) at position {position} is a digit {value} from a different number system than the '{char.ConvertFromUtf32(firstZero)}' digits before it"));
            }
        }
    }

    private static string Describe(int codePoint)
    {
        return "U+" + codePoint.ToString("X4", CultureInfo.InvariantCulture) + ", " + ScriptName(Scripts.Of(codePoint));
    }

    private static string ScriptName(UnicodeScript script)
    {
        return Scripts.Name(script).Replace("_", " ");
    }

    private static IEnumerable<string> ScriptNames(IEnumerable<UnicodeScript> scripts)
    {
        List<string> names = new List<string>();

        foreach (UnicodeScript script in scripts)
        {
            names.Add(ScriptName(script));
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    private static string PolicyName(NamePolicy policy)
    {
        return policy switch
        {
            NamePolicy.Ascii => "Ascii",
            NamePolicy.OneScript => "OneScript",
            NamePolicy.Relaxed => "Relaxed",
            _ => "Anything",
        };
    }

    private static bool IsAscii(string text)
    {
        foreach (char c in text)
        {
            if (c > 0x7F)
            {
                return false;
            }
        }

        return true;
    }
}
