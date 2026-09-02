// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using AwesomeAssertions;

using Xunit;

namespace Squint.Test;

/// <summary>
/// Restriction levels, UTS #39 section 5.2, on ICU4J's <c>TestRestrictionLevel</c> vectors and
/// the specification's examples.
/// </summary>
public class RestrictionLevelTests
{
    /// <summary>
    /// ICU4J's vectors, under ICU's test profile: the General Security Profile plus U+303C, whose
    /// Script_Extensions of Han, Hiragana and Katakana make it the interesting case.
    /// </summary>
    [Theory]
    [InlineData("aγ♥", RestrictionLevel.Unrestricted)]
    [InlineData("a", RestrictionLevel.AsciiOnly)]
    [InlineData("γ", RestrictionLevel.SingleScript)]
    [InlineData("aアー", RestrictionLevel.HighlyRestrictive)]
    [InlineData("aअ", RestrictionLevel.ModeratelyRestrictive)]
    [InlineData("aγ", RestrictionLevel.MinimallyRestrictive)]
    [InlineData("a♥", RestrictionLevel.Unrestricted)]
    [InlineData("a〼", RestrictionLevel.HighlyRestrictive)]
    [InlineData("aー〼", RestrictionLevel.HighlyRestrictive)]
    [InlineData("aー〼ア", RestrictionLevel.HighlyRestrictive)]
    [InlineData("アaー〼", RestrictionLevel.HighlyRestrictive)]
    [InlineData("a1١", RestrictionLevel.ModeratelyRestrictive)]
    [InlineData("a1١۱", RestrictionLevel.ModeratelyRestrictive)]
    [InlineData("١ー〼aア1१۱", RestrictionLevel.MinimallyRestrictive)]
    [InlineData("aアー〼1१١۱", RestrictionLevel.MinimallyRestrictive)]
    public void MatchesIcu4jUnderItsProfile(string text, RestrictionLevel expected)
    {
        Identifiers.RestrictionLevelOf(text, cp => cp == 0x303C || Identifiers.StatusOf(cp) == IdentifierStatus.Allowed)
                   .Should()
                   .Be(expected);
    }

    /// <summary>
    /// The specification's Minimally Restrictive examples, all of which mix Latin with Greek or
    /// Cyrillic: the two Recommended scripts the Moderately level names as exceptions.
    /// </summary>
    [Theory]
    [InlineData("Ωmega")]
    [InlineData("Teχ")]
    [InlineData("HλLF-LIFE")]
    [InlineData("Toys-Я-Us")]
    public void LatinWithGreekOrCyrillicIsMinimallyRestrictive(string text)
    {
        Identifiers.RestrictionLevelOf(text).Should().Be(RestrictionLevel.MinimallyRestrictive);
    }

    /// <summary>
    /// The everyday cases the library exists for: a Danish name is single-script, a Cyrillic
    /// letter in it is not, and an emoji is outside the profile altogether.
    /// </summary>
    [Theory]
    [InlineData("søren", RestrictionLevel.SingleScript)]
    [InlineData("henrik", RestrictionLevel.AsciiOnly)]
    [InlineData("hеnrik", RestrictionLevel.MinimallyRestrictive)]
    [InlineData("henrik☃", RestrictionLevel.Unrestricted)]
    [InlineData("", RestrictionLevel.AsciiOnly)]
    public void EverydayNamesLandWhereExpected(string text, RestrictionLevel expected)
    {
        Identifiers.RestrictionLevelOf(text).Should().Be(expected);
    }

    /// <summary>
    /// The levels are numbered so that the specification's "level or less" reads as a comparison.
    /// </summary>
    [Fact]
    public void LevelsAreOrderedLooserIsGreater()
    {
        (RestrictionLevel.AsciiOnly < RestrictionLevel.SingleScript).Should().BeTrue();
        (RestrictionLevel.HighlyRestrictive < RestrictionLevel.ModeratelyRestrictive).Should().BeTrue();
        (RestrictionLevel.MinimallyRestrictive < RestrictionLevel.Unrestricted).Should().BeTrue();
    }

    /// <summary>
    /// Where the text and ICU part company. ICU admits Latin with any script but Cyrillic, Greek
    /// and Cherokee at the Moderately level; the text requires a Recommended script. Only a
    /// caller's own profile can reach the difference, since the General Security Profile has no
    /// Tifinagh in it.
    /// </summary>
    [Fact]
    public void LatinWithALimitedUseScriptIsMinimallyRestrictiveUnderAPermissiveProfile()
    {
        Identifiers.RestrictionLevelOf("aⵣ", cp => true).Should().Be(RestrictionLevel.MinimallyRestrictive);
        Identifiers.RestrictionLevelOf("aअ", cp => true).Should().Be(RestrictionLevel.ModeratelyRestrictive);
    }
}
