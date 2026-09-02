// SPDX-License-Identifier: LGPL-2.1-or-later
// Copyright (c) 2026 Henrik O. Sørensen

using System;
using System.Collections.Generic;
using System.IO;

using AwesomeAssertions;

using Xunit;

using Squint.Generator;

namespace Squint.Test;

/// <summary>
/// The committed tables are what the generator writes from the committed data, and the two
/// projects agree on the things they each declare by hand.
/// </summary>
public class GeneratedTablesTests
{
    /// <summary>
    /// Regenerating in memory reproduces every file byte for byte. A data update that forgot to
    /// regenerate, or a generator change that did, fails here.
    /// </summary>
    [Fact]
    public void CommittedTablesAreCurrent()
    {
        string root = FindRepositoryRoot();
        IReadOnlyDictionary<string, string> generated = TableGenerator.Generate(Path.Combine(root, "ucd"));

        foreach (KeyValuePair<string, string> file in generated)
        {
            string path = Path.Combine(root, "Squint", "Generated", file.Key);
            File.Exists(path).Should().BeTrue($"{file.Key} should be committed");
            File.ReadAllText(path).Should().Be(file.Value, $"{file.Key} should be regenerated");
        }

        Directory.GetFiles(Path.Combine(root, "Squint", "Generated")).Length.Should().Be(generated.Count);
    }

    /// <summary>
    /// The bit each Identifier_Type gets in the generator is the bit the enum declares for it.
    /// </summary>
    [Fact]
    public void IdentifierTypeBitsAgree()
    {
        for (int bit = 0; bit < TableGenerator.IdentifierTypeNames.Count; bit++)
        {
            string enumName = TableGenerator.IdentifierTypeNames[bit].Replace("_", string.Empty, StringComparison.Ordinal);

            if (string.Equals(enumName, "NotNFKC", StringComparison.Ordinal))
            {
                enumName = "NotNfkc";
            }

            if (string.Equals(enumName, "NotXID", StringComparison.Ordinal))
            {
                enumName = "NotXid";
            }

            Enum.TryParse(enumName, out IdentifierType type).Should().BeTrue($"{enumName} should be an IdentifierType");
            ((int)type).Should().Be(1 << bit);
        }
    }

    private static string FindRepositoryRoot()
    {
        string? directory = AppContext.BaseDirectory;

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "Squint.slnx")))
            {
                return directory;
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new InvalidOperationException("The test binary is not inside the repository.");
    }
}
