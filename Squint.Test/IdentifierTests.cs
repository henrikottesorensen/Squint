// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using AwesomeAssertions;

using Xunit;

using Squint.Uts39;

namespace Squint.Test;

/// <summary>
/// The identifier profile, UTS #39 section 3.1, by example.
/// </summary>
public class IdentifierTests
{
    /// <summary>
    /// The types of a few characters whose reason for being in or out of the profile is well known.
    /// Hyphen and full stop are Inclusion rather than Recommended: they are in the profile as
    /// punctuation the profile admits, not as identifier characters in their own right.
    /// </summary>
    [Theory]
    [InlineData('a', IdentifierType.Recommended)]
    [InlineData('-', IdentifierType.Inclusion)]
    [InlineData('.', IdentifierType.Inclusion)]
    [InlineData(0x00B7, IdentifierType.Inclusion)]
    [InlineData(0x00AD, IdentifierType.DefaultIgnorable)]
    [InlineData(0x13A0, IdentifierType.LimitedUse)]
    [InlineData(0x0378, IdentifierType.NotCharacter)]
    [InlineData(0xE000, IdentifierType.NotCharacter)]
    [InlineData(0xFFFF, IdentifierType.NotCharacter)]
    [InlineData(0x2126, IdentifierType.NotNfkc)]
    public void TypesAreAsExpected(int codePoint, IdentifierType expected)
    {
        Identifiers.TypeOf(codePoint).Should().Be(expected);
    }

    /// <summary>
    /// A character can carry more than one type.
    /// </summary>
    [Fact]
    public void TypesCanCombine()
    {
        IdentifierType type = Identifiers.TypeOf(0x0740);

        type.Should().Be(IdentifierType.LimitedUse | IdentifierType.Technical);
    }

    /// <summary>
    /// Allowed is exactly Recommended or Inclusion.
    /// </summary>
    [Theory]
    [InlineData('a', IdentifierStatus.Allowed)]
    [InlineData(0x00F8, IdentifierStatus.Allowed)]
    [InlineData(0x00B7, IdentifierStatus.Allowed)]
    [InlineData(0x00AD, IdentifierStatus.Restricted)]
    [InlineData(0x13A0, IdentifierStatus.Restricted)]
    [InlineData(0x2603, IdentifierStatus.Restricted)]
    [InlineData(0x0378, IdentifierStatus.Restricted)]
    public void StatusIsAsExpected(int codePoint, IdentifierStatus expected)
    {
        Identifiers.StatusOf(codePoint).Should().Be(expected);
    }

    /// <summary>
    /// A string is in the profile when every character is.
    /// </summary>
    [Theory]
    [InlineData("søren", true)]
    [InlineData("", true)]
    [InlineData("hen‍rik", false)]
    [InlineData("henrik☃", false)]
    public void StringIsAllowedWhenEveryCharacterIs(string text, bool expected)
    {
        Identifiers.IsAllowed(text).Should().Be(expected);
    }

    /// <summary>
    /// Out-of-range integers are refused rather than looked up.
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(0x110000)]
    public void NonCodePointsAreRefused(int value)
    {
        System.Action typeOf = () => Identifiers.TypeOf(value);
        typeOf.Should().Throw<System.ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// The version the tables came from is stated.
    /// </summary>
    [Fact]
    public void UnicodeVersionIsStated()
    {
        UnicodeData.Version.Should().Be("17.0.0");
    }
}
