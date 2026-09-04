// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System.Linq;

using AwesomeAssertions;

using Xunit;

using Squint.Uts39;

namespace Squint.Test;

/// <summary>
/// Mixed-number detection, UTS #39 section 5.3, on ICU4J's <c>TestMixedNumbers</c> vectors.
/// </summary>
public class MixedNumberTests
{
    /// <summary>
    /// The number systems found, each as its zero.
    /// </summary>
    [Theory]
    [InlineData("1", "0")]
    [InlineData("१", "०")]
    [InlineData("1१", "0०")]
    [InlineData("١۱", "٠۰")]
    [InlineData("a♥", "")]
    [InlineData("a〼", "")]
    [InlineData("a1١", "0٠")]
    [InlineData("a1١۱", "0٠۰")]
    [InlineData("١ー〼aア1१۱", "0٠۰०")]
    public void NumberSystemsAreTheirZeros(string text, string expectedZeros)
    {
        Identifiers.NumberSystemsOf(text).Should().Equal(expectedZeros.Select(c => (int)c));
        Identifiers.HasMixedNumbers(text).Should().Be(expectedZeros.Length > 1);
    }

    /// <summary>
    /// Only decimal digits count: a superscript two, a Roman numeral and a circled digit are
    /// numbers of other kinds and are not number systems.
    /// </summary>
    [Theory]
    [InlineData("²")]
    [InlineData("Ⅳ")]
    [InlineData("①")]
    public void OtherNumbersAreNotNumberSystems(string text)
    {
        Identifiers.NumberSystemsOf(text).Should().BeEmpty();
    }

    /// <summary>
    /// The digit value of a decimal digit is its distance from its zero, in any system.
    /// </summary>
    [Theory]
    [InlineData('7', 7)]
    [InlineData(0x0663, 3)]
    [InlineData(0xFF19, 9)]
    [InlineData(0x1D7D8, 0)]
    public void DecimalDigitValueIsTheDistanceFromZero(int codePoint, int expected)
    {
        Identifiers.DecimalDigitValue(codePoint).Should().Be(expected);
    }

    /// <summary>
    /// A letter has no digit value.
    /// </summary>
    [Fact]
    public void LettersHaveNoDigitValue()
    {
        Identifiers.DecimalDigitValue('a').Should().BeNull();
    }
}
