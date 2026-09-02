// SPDX-License-Identifier: LGPL-2.1-or-later
// Copyright (c) 2026 Henrik O. Sørensen

using System;
using System.Collections.Generic;
using System.IO;

namespace Squint.Generator;

/// <summary>
/// Regenerates <c>Squint/Generated/*.g.cs</c> from the Unicode data files under <c>ucd/</c>.
/// </summary>
/// <remarks>
/// Run with no arguments from anywhere inside the repository; pass the repository root to run it
/// from elsewhere. The output is committed, so the library builds without this project and a
/// reviewer can diff what a data update changed.
/// </remarks>
public static class Program
{
    /// <summary>
    /// Entry point.
    /// </summary>
    public static int Main(string[] args)
    {
        string root = args.Length > 0 ? args[0] : FindRepositoryRoot();
        string ucd = Path.Combine(root, "ucd");
        string output = Path.Combine(root, "Squint", "Generated");

        IReadOnlyDictionary<string, string> files = TableGenerator.Generate(ucd);

        Directory.CreateDirectory(output);

        foreach (KeyValuePair<string, string> file in files)
        {
            string path = Path.Combine(output, file.Key);
            File.WriteAllText(path, file.Value);
            Console.WriteLine($"wrote {path} ({file.Value.Length} chars)");
        }

        return 0;
    }

    private static string FindRepositoryRoot()
    {
        string? directory = Directory.GetCurrentDirectory();

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory, "Squint.slnx")))
            {
                return directory;
            }

            directory = Path.GetDirectoryName(directory);
        }

        throw new InvalidOperationException("Not inside the Squint repository; pass its root as the first argument.");
    }
}
