// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Squint.Generator;

/// <summary>
/// The scripts in enum order: the <c>Undefined</c> sentinel, then <c>Unknown</c>, the rest of
/// the property values by name, then the three writing systems UTS #39 adds.
/// </summary>
public sealed class ScriptRegistry
{
    private readonly List<ScriptInfo> _scripts;
    private readonly Dictionary<string, int> _byCode;
    private readonly Dictionary<string, int> _byName;

    private ScriptRegistry(List<ScriptInfo> scripts)
    {
        _scripts = scripts;
        _byCode = new Dictionary<string, int>(StringComparer.Ordinal);
        _byName = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < scripts.Count; i++)
        {
            _byCode[scripts[i].Code] = i;
            _byName[scripts[i].LongName] = i;
        }
    }

    /// <summary>
    /// Number of scripts, including the three writing systems.
    /// </summary>
    public int Count => _scripts.Count;

    /// <summary>
    /// The script at an enum position.
    /// </summary>
    public ScriptInfo this[int index] => _scripts[index];

    /// <summary>
    /// Reads the <c>sc</c> lines of PropertyValueAliases.txt.
    /// </summary>
    public static ScriptRegistry Load(string propertyValueAliasesPath)
    {
        List<ScriptInfo> values = new List<ScriptInfo>();

        foreach (string raw in File.ReadLines(propertyValueAliasesPath))
        {
            if (!raw.StartsWith("sc ", StringComparison.Ordinal))
            {
                continue;
            }

            string[] fields = raw.Split(';');
            string code = fields[1].Trim();
            string longName = fields[2].Trim();
            values.Add(new ScriptInfo(code, longName, $"<c>{code}</c>, the Script property value <c>{longName}</c>."));
        }

        // Position 0 is the sentinel every enum in the library carries: not a script, the value
        // an uninitialised field has, rejected by every lookup. Unknown (Zzzz), the property
        // value of an unassigned code point, is a real script and takes position 1.
        ScriptInfo unknown = values.Single(v => string.Equals(v.Code, "Zzzz", StringComparison.Ordinal));
        List<ScriptInfo> ordered = new List<ScriptInfo>
        {
            new ScriptInfo(string.Empty, "Undefined", "Not a script: the default of an uninitialised value, never returned and not accepted by any lookup or set."),
            unknown,
        };
        ordered.AddRange(values.Where(v => !ReferenceEquals(v, unknown)).OrderBy(v => v.EnumName, StringComparer.Ordinal));

        if (ordered.Count != values.Count + 1)
        {
            throw new InvalidDataException("PropertyValueAliases.txt lists a script code twice.");
        }

        ordered.Add(new ScriptInfo("Hanb", "Han_With_Bopomofo", "<c>Hanb</c>, Han with Bopomofo: a writing system, not a Script property value. UTS #39 section 5.1 adds it to the script set of every Han and Bopomofo character."));
        ordered.Add(new ScriptInfo("Jpan", "Japanese", "<c>Jpan</c>, Japanese: a writing system, not a Script property value. UTS #39 section 5.1 adds it to the script set of every Han, Hiragana and Katakana character."));
        ordered.Add(new ScriptInfo("Kore", "Korean", "<c>Kore</c>, Korean: a writing system, not a Script property value. UTS #39 section 5.1 adds it to the script set of every Han and Hangul character."));

        if (ordered.Count > 256)
        {
            throw new InvalidDataException("More scripts than a ScriptSet can hold.");
        }

        return new ScriptRegistry(ordered);
    }

    /// <summary>
    /// The enum position of a four-letter code.
    /// </summary>
    public int IndexOf(string code)
    {
        if (_byCode.TryGetValue(code, out int index))
        {
            return index;
        }

        throw new InvalidDataException($"Unknown script code {code}.");
    }

    /// <summary>
    /// The enum position of a long property value name.
    /// </summary>
    public int IndexOfName(string longName)
    {
        if (_byName.TryGetValue(longName, out int index))
        {
            return index;
        }

        throw new InvalidDataException($"Unknown script name {longName}.");
    }
}
