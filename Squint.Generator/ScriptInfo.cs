// SPDX-License-Identifier: MPL-2.0
// Copyright (c) 2026 Henrik O. Sørensen
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0. If a copy of
// the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.

namespace Squint.Generator;

/// <summary>
/// One script: its ISO 15924 code, its long property value name, and the enum member it becomes.
/// </summary>
public sealed class ScriptInfo
{
    internal ScriptInfo(string code, string longName, string summary)
    {
        Code = code;
        LongName = longName;
        EnumName = longName.Replace("_", string.Empty);
        Summary = summary;
    }

    /// <summary>
    /// The four-letter code, such as <c>Latn</c>.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// The long property value name, such as <c>Old_Italic</c>.
    /// </summary>
    public string LongName { get; }

    /// <summary>
    /// The enum member name, the long name without underscores.
    /// </summary>
    public string EnumName { get; }

    /// <summary>
    /// The doc comment for the enum member.
    /// </summary>
    public string Summary { get; }
}
