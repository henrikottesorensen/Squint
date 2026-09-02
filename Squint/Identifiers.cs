// SPDX-License-Identifier: LGPL-2.1-or-later
// Copyright (c) 2026 Henrik O. Sørensen

using System;
using System.Collections.Generic;

namespace Squint;

/// <summary>
/// The identifier profile of UTS #39 section 3.1, restriction levels of section 5.2 and mixed
/// number detection of section 5.3.
/// </summary>
public static class Identifiers
{
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
