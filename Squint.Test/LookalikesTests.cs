// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using AwesomeAssertions;

using Xunit;

namespace Squint.Test;

/// <summary>
/// The two-string question and the key, on the examples a reader would try first.
/// </summary>
public class LookalikesTests
{
    /// <summary>
    /// Pairs that look alike: across scripts, within ASCII, through a digit, and through a
    /// compatibility form that the key folds first.
    /// </summary>
    [Theory]
    [InlineData("paypal", "pаypаl")]
    [InlineData("modern", "rnodern")]
    [InlineData("love", "1ove")]
    [InlineData("scope", "ѕсоре")]
    [InlineData("file", "ﬁle")]
    [InlineData("paypal", "𝗉𝖺𝗒𝗉𝖺𝗅")]
    [InlineData("henrik", "hen‍rik")]
    public void LookalikesMatch(string first, string second)
    {
        Lookalikes.Match(first, second).Should().BeTrue();
        Lookalikes.Key(second).Should().Be(Lookalikes.Key(first));
    }

    /// <summary>
    /// Pairs that do not.
    /// </summary>
    [Theory]
    [InlineData("paypal", "paypa1s")]
    [InlineData("henrik", "hendrik")]
    [InlineData("søren", "soren")]
    public void DifferentNamesDoNot(string first, string second)
    {
        Lookalikes.Match(first, second).Should().BeFalse();
    }

    /// <summary>
    /// The key is the skeleton of the clean form, so it agrees with what
    /// <see cref="Names.Inspect(string, NamePolicy)"/> reports.
    /// </summary>
    [Fact]
    public void KeyAgreesWithInspection()
    {
        Lookalikes.Key("pаypаl").Should().Be("paypal");
        Lookalikes.Key("pаypаl").Should().Be(Names.Inspect("pаypаl").LookalikeKey);
    }
}
