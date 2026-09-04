// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;

namespace Squint.Uts39;

/// <summary>
/// The identifier profile of UTS #39 section 3.1, restriction levels of section 5.2 and mixed
/// number detection of section 5.3, and <see cref="Check(string, RestrictionLevel)"/>, which
/// runs them together in the right order.
/// </summary>
public static class Identifiers
{
    /// <summary>
    /// The whole check on one identifier, in the order the pieces have to run: the profile on
    /// the input as given, NFKC, the profile again on the result, the restriction level against
    /// <paramref name="permitted"/>, mixed numbers, and the skeleton.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The profile is checked before normalising as well as after, on purpose. A character
    /// whose normalization differs between Unicode versions is, by that very fact, outside the
    /// General Security Profile (its Identifier_Type is Not_NFKC), so checking the raw input
    /// refuses it before any normaliser, of any version, has had a say. That is what makes the
    /// verdict the same on every machine. It also means a ligature, a fullwidth letter or a
    /// mathematical alphabet is refused although NFKC would fold it to letters that pass; a
    /// caller whose flow folds first by design should pass <see cref="Normalization.Nfkc"/> of
    /// the text instead.
    /// </para>
    /// <para>
    /// Nothing here is syntax. An empty string is accepted, and so is one of only punctuation;
    /// the length, the first character and the characters a system reserves are the caller's
    /// rules, applied on <see cref="IdentifierCheck.Normalized"/>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">The text is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The level is <see cref="RestrictionLevel.Undefined"/>.</exception>
    public static IdentifierCheck Check(string text, RestrictionLevel permitted)
    {
        return Check(text, permitted, IsInGeneralSecurityProfile);
    }

    /// <summary>
    /// <see cref="Check(string, RestrictionLevel)"/> against a caller's identifier profile.
    /// </summary>
    /// <exception cref="ArgumentNullException">The text or the profile is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The level is <see cref="RestrictionLevel.Undefined"/>.</exception>
    public static IdentifierCheck Check(string text, RestrictionLevel permitted, Func<int, bool> identifierProfile)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        if (identifierProfile is null)
        {
            throw new ArgumentNullException(nameof(identifierProfile));
        }

        if (permitted < RestrictionLevel.AsciiOnly || permitted > RestrictionLevel.Unrestricted)
        {
            throw new ArgumentOutOfRangeException(nameof(permitted), permitted, "Not a restriction level.");
        }

        IdentifierProblems problems = IdentifierProblems.None;

        if (!Satisfies(text, identifierProfile))
        {
            problems |= IdentifierProblems.OutsideProfile;
        }

        string normalized = Normalization.Nfkc(text);

        if (!Satisfies(normalized, identifierProfile))
        {
            problems |= IdentifierProblems.OutsideProfile;
        }

        RestrictionLevel level = RestrictionLevelOf(normalized, identifierProfile);

        if (level > permitted)
        {
            problems |= IdentifierProblems.ExceedsRestrictionLevel;
        }

        IReadOnlyList<int> numberSystems = NumberSystemsOf(normalized);

        if (numberSystems.Count > 1)
        {
            problems |= IdentifierProblems.MixedNumbers;
        }

        return new IdentifierCheck(text, normalized, Confusables.Skeleton(normalized), level, numberSystems, problems);
    }

    /// <summary>
    /// The Identifier_Type of a code point.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Not a code point.</exception>
    public static IdentifierType TypeOf(int codePoint)
    {
        CodePoints.Validate(codePoint, nameof(codePoint));
        return (IdentifierType)IdentifierData.TypeRunValues[Tables.FindRun(IdentifierData.TypeRunStarts, codePoint)];
    }

    /// <summary>
    /// The Identifier_Status of a code point.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Not a code point.</exception>
    public static IdentifierStatus StatusOf(int codePoint)
    {
        CodePoints.Validate(codePoint, nameof(codePoint));

        return Tables.FindRange(IdentifierData.AllowedStarts, IdentifierData.AllowedEnds, codePoint) >= 0
            ? IdentifierStatus.Allowed
            : IdentifierStatus.Restricted;
    }

    /// <summary>
    /// Whether every code point of the string has Identifier_Status Allowed: the General
    /// Security Profile, with no further syntax. An empty string passes.
    /// </summary>
    /// <exception cref="ArgumentNullException">The text is null.</exception>
    public static bool IsAllowed(string text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        int index = 0;

        while (index < text.Length)
        {
            if (StatusOf(CodePoints.Read(text, ref index)) != IdentifierStatus.Allowed)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The restriction level of the string, with the General Security Profile as the identifier
    /// profile: a character with Identifier_Status Restricted makes the string
    /// <see cref="RestrictionLevel.Unrestricted"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">The text is null.</exception>
    public static RestrictionLevel RestrictionLevelOf(string text)
    {
        return RestrictionLevelOf(text, IsInGeneralSecurityProfile);
    }

    /// <summary>
    /// The restriction level of the string against a caller's identifier profile: a code point
    /// the profile rejects makes the string <see cref="RestrictionLevel.Unrestricted"/>.
    /// </summary>
    /// <remarks>
    /// Section 5.2 by the numbers. Unlike ICU, which admits Latin with any script other than
    /// Cyrillic, Greek and Cherokee at the Moderately Restrictive level, this follows the text:
    /// the other script must be Recommended. The two agree whenever the profile is the General
    /// Security Profile, because every Allowed character is in a Recommended script.
    /// </remarks>
    /// <exception cref="ArgumentNullException">The text or the profile is null.</exception>
    public static RestrictionLevel RestrictionLevelOf(string text, Func<int, bool> identifierProfile)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        if (identifierProfile is null)
        {
            throw new ArgumentNullException(nameof(identifierProfile));
        }

        bool ascii = true;
        int index = 0;

        while (index < text.Length)
        {
            int codePoint = CodePoints.Read(text, ref index);

            if (!identifierProfile(codePoint))
            {
                return RestrictionLevel.Unrestricted;
            }

            if (codePoint > 0x7F)
            {
                ascii = false;
            }
        }

        if (ascii)
        {
            return RestrictionLevel.AsciiOnly;
        }

        if (!Scripts.ResolvedSetOf(text).IsEmpty)
        {
            return RestrictionLevel.SingleScript;
        }

        ScriptSet withoutLatin = Scripts.ResolvedSetWithout(text, UnicodeScript.Latin);

        if (withoutLatin.Contains(UnicodeScript.Japanese)
            || withoutLatin.Contains(UnicodeScript.Korean)
            || withoutLatin.Contains(UnicodeScript.HanWithBopomofo))
        {
            return RestrictionLevel.HighlyRestrictive;
        }

        if (withoutLatin.Intersects(ModeratelyRestrictivePartners))
        {
            return RestrictionLevel.ModeratelyRestrictive;
        }

        return RestrictionLevel.MinimallyRestrictive;
    }

    /// <summary>
    /// Whether the string has decimal digits from more than one number system.
    /// </summary>
    /// <exception cref="ArgumentNullException">The text is null.</exception>
    public static bool HasMixedNumbers(string text)
    {
        return NumberSystemsOf(text).Count > 1;
    }

    /// <summary>
    /// The number systems the string's decimal digits come from, each as the code point of its
    /// zero, in ascending order. Only characters of General_Category Nd count.
    /// </summary>
    /// <exception cref="ArgumentNullException">The text is null.</exception>
    public static IReadOnlyList<int> NumberSystemsOf(string text)
    {
        if (text is null)
        {
            throw new ArgumentNullException(nameof(text));
        }

        SortedSet<int> zeros = new SortedSet<int>();
        int index = 0;

        while (index < text.Length)
        {
            int zero = DigitZero(CodePoints.Read(text, ref index));

            if (zero >= 0)
            {
                zeros.Add(zero);
            }
        }

        return new List<int>(zeros);
    }

    /// <summary>
    /// The value of a decimal digit, or null for any other code point.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Not a code point.</exception>
    public static int? DecimalDigitValue(int codePoint)
    {
        CodePoints.Validate(codePoint, nameof(codePoint));

        int zero = DigitZero(codePoint);
        return zero < 0 ? null : codePoint - zero;
    }

    private static ScriptSet ModeratelyRestrictivePartners { get; } =
        Scripts.Recommended.Remove(UnicodeScript.Cyrillic).Remove(UnicodeScript.Greek);

    private static bool IsInGeneralSecurityProfile(int codePoint)
    {
        return StatusOf(codePoint) == IdentifierStatus.Allowed;
    }

    private static bool Satisfies(string text, Func<int, bool> identifierProfile)
    {
        int index = 0;

        while (index < text.Length)
        {
            if (!identifierProfile(CodePoints.Read(text, ref index)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The zero of the code point's number system, or -1 when it is not a decimal digit. Every
    /// Nd digit sits in a contiguous run of ten from its zero, which the generator verified.
    /// </summary>
    private static int DigitZero(int codePoint)
    {
        int run = Tables.FindRun(CharacterData.DigitZeros, codePoint);

        if (run >= 0 && codePoint <= CharacterData.DigitZeros[run] + 9)
        {
            return CharacterData.DigitZeros[run];
        }

        return -1;
    }
}
