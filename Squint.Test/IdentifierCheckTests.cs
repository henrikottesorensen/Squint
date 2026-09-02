// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;

using AwesomeAssertions;

using Xunit;

namespace Squint.Test;

/// <summary>
/// The one-call check, on the cases a username validator meets.
/// </summary>
public class IdentifierCheckTests
{
    /// <summary>
    /// Names that pass at the Highly Restrictive level, with the level each lands on.
    /// </summary>
    [Theory]
    [InlineData("henrik", RestrictionLevel.AsciiOnly)]
    [InlineData("søren", RestrictionLevel.SingleScript)]
    [InlineData("Ægir", RestrictionLevel.SingleScript)]
    [InlineData("Yıldız", RestrictionLevel.SingleScript)]
    [InlineData("東京太郎", RestrictionLevel.SingleScript)]
    [InlineData("aアー", RestrictionLevel.HighlyRestrictive)]
    [InlineData("", RestrictionLevel.AsciiOnly)]
    public void OrdinaryNamesAreAccepted(string name, RestrictionLevel expectedLevel)
    {
        IdentifierCheck check = Identifiers.Check(name, RestrictionLevel.HighlyRestrictive);

        check.IsAccepted.Should().BeTrue();
        check.Problems.Should().Be(IdentifierProblems.None);
        check.Level.Should().Be(expectedLevel);
        check.Normalized.Should().Be(name);
    }

    /// <summary>
    /// A Cyrillic letter among Latin ones exceeds the Highly Restrictive level, and is accepted
    /// once the permitted level is loosened to what it is.
    /// </summary>
    [Fact]
    public void ACyrillicLetterAmongLatinExceedsTheLevel()
    {
        IdentifierCheck strict = Identifiers.Check("hеnrik", RestrictionLevel.HighlyRestrictive);
        IdentifierCheck loose = Identifiers.Check("hеnrik", RestrictionLevel.MinimallyRestrictive);

        strict.Problems.Should().Be(IdentifierProblems.ExceedsRestrictionLevel);
        strict.Level.Should().Be(RestrictionLevel.MinimallyRestrictive);
        loose.IsAccepted.Should().BeTrue();
        strict.Skeleton.Should().Be("henrik");
    }

    /// <summary>
    /// Two names with the same skeleton collide however each fares on its own.
    /// </summary>
    [Fact]
    public void ConfusableNamesShareASkeleton()
    {
        Identifiers.Check("paypal", RestrictionLevel.HighlyRestrictive).Skeleton
                   .Should()
                   .Be(Identifiers.Check("pаypаl", RestrictionLevel.Unrestricted).Skeleton);
    }

    /// <summary>
    /// The profile is checked on the raw input, so a mathematical alphabet is refused even
    /// though NFKC would fold it to plain letters that pass. That is what keeps the verdict the
    /// same on a machine whose normaliser is older.
    /// </summary>
    [Fact]
    public void TheProfileIsCheckedBeforeNormalising()
    {
        IdentifierCheck check = Identifiers.Check("𝗉𝖺𝗒𝗉𝖺𝗅", RestrictionLevel.HighlyRestrictive);

        check.Problems.Should().Be(IdentifierProblems.OutsideProfile);
        check.Normalized.Should().Be("paypal");
        check.Level.Should().Be(RestrictionLevel.AsciiOnly);
    }

    /// <summary>
    /// A zero-width joiner and an emoji are outside the profile.
    /// </summary>
    [Theory]
    [InlineData("hen‍rik")]
    [InlineData("henrik☃")]
    public void CharactersOutsideTheProfileAreRefused(string name)
    {
        Identifiers.Check(name, RestrictionLevel.Unrestricted).Problems.Should().Be(IdentifierProblems.OutsideProfile);
    }

    /// <summary>
    /// Problems accumulate: digits from two number systems, in a string whose level is also too
    /// loose for the ceiling.
    /// </summary>
    [Fact]
    public void ProblemsAccumulate()
    {
        IdentifierCheck check = Identifiers.Check("a1١", RestrictionLevel.SingleScript);

        check.Problems.Should().Be(IdentifierProblems.MixedNumbers | IdentifierProblems.ExceedsRestrictionLevel);
        check.NumberSystems.Should().Equal('0', 0x0660);
        check.Level.Should().Be(RestrictionLevel.ModeratelyRestrictive);
    }

    /// <summary>
    /// Normalization is applied, and it is the normalized form that is judged and returned.
    /// </summary>
    [Fact]
    public void TheNormalizedFormIsJudged()
    {
        IdentifierCheck check = Identifiers.Check("érik", RestrictionLevel.HighlyRestrictive);

        check.IsAccepted.Should().BeTrue();
        check.Normalized.Should().Be("érik");
        check.Input.Should().Be("érik");
    }

    /// <summary>
    /// A caller's own profile replaces the General Security Profile.
    /// </summary>
    [Fact]
    public void ACallersProfileIsUsed()
    {
        Identifiers.Check("henrik☃", RestrictionLevel.Unrestricted, cp => true).IsAccepted.Should().BeTrue();
        Identifiers.Check("henrik", RestrictionLevel.Unrestricted, cp => cp != 'h').Problems.Should().Be(IdentifierProblems.OutsideProfile);
    }

    /// <summary>
    /// The sentinel level is refused, like a null.
    /// </summary>
    [Fact]
    public void UndefinedLevelAndNullAreRefused()
    {
        Action undefined = () => Identifiers.Check("henrik", RestrictionLevel.Undefined);
        Action nullText = () => Identifiers.Check(null!, RestrictionLevel.AsciiOnly);

        undefined.Should().Throw<ArgumentOutOfRangeException>();
        nullText.Should().Throw<ArgumentNullException>();
    }
}
