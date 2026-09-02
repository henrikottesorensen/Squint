# Squint

[![CI](https://github.com/henrikottesorensen/Squint/actions/workflows/ci.yml/badge.svg)](https://github.com/henrikottesorensen/Squint/actions/workflows/ci.yml)

**Tired of squinting at strings for Cyrillic homoglyphs? Squint does it for you.**

[UTS #39, Unicode Security Mechanisms](https://www.unicode.org/reports/tr39/) for .NET, from the
Unicode 17.0 data: confusable skeletons and the three classes of confusable, Script and
Script_Extensions with the resolved script set, the six restriction levels, Identifier_Status and
Identifier_Type, mixed-number detection, and the four normalization forms.

Pure managed code with its own normaliser, so it needs no ICU, works in globalization-invariant
mode, and gives the same answer on every platform from the same Unicode version as its
confusable table. Targets `netstandard2.0`. No dependencies, and the tables travel inside the
assembly, which is 280 KB for that reason.

## Usage

```csharp
using Squint;

// The whole check on an identifier, in the right order: the profile on the raw input, NFKC,
// the restriction level against your ceiling, mixed numbers, the skeleton.
IdentifierCheck check = Identifiers.Check("hеnrik", RestrictionLevel.HighlyRestrictive);
check.IsAccepted;                                // false
check.Problems;                                  // ExceedsRestrictionLevel - one Cyrillic е
check.Level;                                     // MinimallyRestrictive
check.Normalized;                                // the NFKC form: store and compare this
check.Skeleton;                                  // "henrik": collides with the real one

// The pieces, for anyone composing their own.

// Section 4: the skeleton. Two strings are confusable when their skeletons are equal.
Confusables.Skeleton("pаypаl");                 // "paypal" - two Cyrillic letters, one skeleton
Confusables.AreConfusable("modern", "rnodern");  // true - rn and m, in plain ASCII
Confusables.Classify("scope", "ѕсоре");          // ConfusableClass.WholeScript
Confusables.Classify("paypal", "pаypаl");        // ConfusableClass.MixedScript
Confusables.Classify("ǉeto", "ljeto");           // ConfusableClass.SingleScript

// Section 5.1: scripts. Common and Inherited count as every script; Han also counts as
// Japanese, Korean and Han-with-Bopomofo, so 〆切 is single-script.
Scripts.ResolvedSetOf("Circ1e");                 // {Latn}
Scripts.ResolvedSetOf("Сirсlе");                 // {} - Cyrillic С and с among Latin letters
Scripts.IsSingleScript("〆切");                   // true
Scripts.AugmentedSetOf(0x306D);                  // {Hira, Jpan}

// Section 5.2: restriction levels, against the General Security Profile by default.
Identifiers.RestrictionLevelOf("henrik");        // AsciiOnly
Identifiers.RestrictionLevelOf("søren");         // SingleScript
Identifiers.RestrictionLevelOf("aアー");         // HighlyRestrictive - Latin plus Japanese
Identifiers.RestrictionLevelOf("Toys-Я-Us");     // MinimallyRestrictive - Latin plus Cyrillic
Identifiers.RestrictionLevelOf("henrik☃");       // Unrestricted - outside the profile

// Section 3.1: the identifier profile.
Identifiers.StatusOf('ø');                       // IdentifierStatus.Allowed
Identifiers.TypeOf(0x13A0);                      // IdentifierType.LimitedUse - Cherokee
Identifiers.IsAllowed("hen‍rik");           // false - a zero-width joiner

// Section 5.3: mixed numbers.
Identifiers.HasMixedNumbers("a1١");              // true - ASCII and Arabic-Indic digits
Identifiers.NumberSystemsOf("a1١");              // [0x30, 0x660] - each system by its zero

// UAX #15: the normalization forms, at the same Unicode version as everything above.
Normalization.Nfkc("𝗉𝖺𝗒𝗉𝖺𝗅");                    // "paypal" - the identity form UAX #31 asks for
Normalization.Nfkc("ﬁle");                       // "file"
Normalization.Nfc("e\u0301");                    // "é"
```

`Identifiers.Check` is that composition for a username or a label: refuse when it is not
accepted, and treat it as a collision when its `Skeleton` equals a stored one. It is not syntax:
length, first character and reserved names stay yours, applied to `Normalized`. Because the
profile is checked on the input *before* normalising, a ligature, a fullwidth letter or a
mathematical alphabet is refused even though NFKC would fold it to letters that pass. For a
username that is the right answer: a person who typed one is pasting or up to something. For a
flow that folds first by design, a domain label after UTS #46 mapping, say, fold and then check:
`Identifiers.Check(Normalization.Nfkc(text), level)`. Store
`UnicodeData.Version` beside anything you keep, and recompute when it changes.

Cost, measured in Release on an Apple M-series laptop: a skeleton of a 12-character mixed-script
string is under a microsecond, a restriction level about a third of one, NFKC about half. Every
call allocates its result; nothing is cached or pooled.

## What the answers are

**Skeleton** is `internalSkeleton` in the specification's current terms: NFD, remove
Default_Ignorable_Code_Point characters, replace each character by its prototype from
`confusables.txt`, NFD again. That is what `skeleton(X)` was before revision 27 added
bidirectional reordering, and what ICU computes. The bidirectional `bidiSkeleton` is not
implemented; for left-to-right text with no right-to-left characters the two are the same. For
Arabic or Hebrew identifiers, or any that mix directions, this means the skeleton is the one
every implementation has computed so far, and it may differ from what a future ICU computes
once it implements the reordering.

A skeleton is an intermediate form: not for display, and not stable across Unicode versions. Store
them if you like, and recompute them when the data changes.

**Restriction levels** follow section 5.2 by the numbers, with one place where the text and ICU
differ: ICU admits Latin plus any script other than Cyrillic, Greek and Cherokee at the
Moderately Restrictive level, where the text requires a *Recommended* script (UAX #31 Table 5).
This library follows the text. The two agree whenever the profile is the General Security
Profile, because every Allowed character is in a Recommended script; a caller-supplied profile
that admits, say, Tifinagh can tell them apart.

**The profile** used by default is the General Security Profile, Identifier_Status = Allowed. Pass
your own `Func<int, bool>` to `RestrictionLevelOf` to use another.

**Normalization** is here so that the whole canonicalisation of an identifier runs at one Unicode
version. The runtime's `string.Normalize` is the machine's ICU on Linux and macOS and the
operating system's on Windows, each at whatever version that build shipped with, and in
globalization-invariant mode it returns non-ASCII text unchanged without saying so. NFKC from
one machine and a skeleton from another is the version drift this library exists to remove.

## The trap in the table

Section 4 promises that the mappings are idempotent. They are not quite: `ǆ` (U+01C6) maps to
`d` + `ž`, whose final NFD is `d z caron`, while a caron on its own maps to a breve. So
`Skeleton("ǆ")` and `Skeleton("dž")` differ by one combining mark, and applying the skeleton
twice changes the answer. ICU does the same; the library pins it in a test rather than "fixing"
it into disagreeing with every other implementation.

## Where the answers come from

Everything under `ucd/` is the Unicode 17.0 data, unmodified: `confusables.txt`,
`IdentifierStatus.txt` and `IdentifierType.txt` from the security data; `Scripts.txt`,
`ScriptExtensions.txt`, `PropertyValueAliases.txt`, `UnicodeData.txt`,
`DerivedCoreProperties.txt` and `DerivedNormalizationProps.txt` from the UCD. `Squint.Generator` turns them into the tables under
`Squint/Generated`, which are committed; a test regenerates them in memory and fails if they are
stale. To update the data, replace the files and run:

```bash
dotnet run --project Squint.Generator
```

The generator refuses data that breaks a promise the library relies on: two prototypes for one
code point, a decimal digit not in a run of ten from its zero, or `Identifier_Status` disagreeing
with `Identifier_Type`.

## How it is tested

Three ways, because the specification is prose and prose is read differently by different people.

By example: ICU4J's own test vectors for skeletons, confusable classes, restriction levels and
mixed numbers, and the specification's worked examples, as ordinary unit tests.

Against the runtime: the library's four normalization forms against `string.Normalize` for
every code point the runtime's normaliser knows, for UAX #15's own hard cases, and for random
sequences of letters, compatibility characters and marks. The normaliser is a different ICU on
every operating system, at whatever Unicode version it shipped with, so the suite measures that
version first, by normalising one character of each Unicode age, and leaves out characters
younger than it. CI runs this on Linux, macOS and Windows.

Against ICU at the same Unicode version, twice: `tools/icu-oracle.py` asks a built ICU4C 78.3,
and `tools/Icu4jOracle.java` asks ICU4J 78.3, the code the script-set and restriction-level logic
was ported from. Both are Unicode 17.0 and both write a fixture of the same layout under
`Squint.Test/Fixtures/`. The tests compare skeletons, confusable classes, restriction levels
under two profiles, number systems, the four normalization forms, and every code point's Script, Script_Extensions,
Identifier_Type, Identifier_Status, Default_Ignorable_Code_Point and digit value against each.
The fixtures are committed, so the suite needs no ICU to run. To regenerate them:

```bash
ICU_DATA=/path/to/icu/source/data/in python3 tools/icu-oracle.py /path/to/icu-build/lib
```

```bash
javac -cp icu4j-78.3.jar -d out tools/Icu4jOracle.java && java -cp icu4j-78.3.jar:out Icu4jOracle ucd/confusables.txt Squint.Test/Fixtures/icu4j-oracle.json
```

## Not implemented

- The bidirectional skeleton of section 4 (revision 27 and later), which needs the Unicode
  Bidirectional Algorithm.
- The whole-script confusable *search* of section 4.1: whether some string in another script is
  confusable with this one. `Classify` answers the question for a given pair.
- The optional detections of section 5.4: combining-mark sequence limits, the hidden overlay
  check, exemplar-set checks.

## API

Five static classes, one value type and five enums, all in namespace `Squint`. Every method
takes a `string` or an `int` code point, throws on null or an out-of-range code point, and never
touches the runtime's Unicode tables.

**`Confusables`**, UTS #39 section 4

- `string Skeleton(string text)`
- `bool AreConfusable(string first, string second)`
- `ConfusableClass Classify(string first, string second)`: `NotConfusable`, `SingleScript`,
  `MixedScript` or `WholeScript`
- `string? Prototype(int codePoint)`: the raw table entry, null when the character is its own
  prototype
- `bool IsDefaultIgnorable(int codePoint)`

**`Scripts`**, section 5.1

- `UnicodeScript Of(int codePoint)`: the Script property
- `ScriptSet ExtensionsOf(int codePoint)`: Script_Extensions
- `ScriptSet AugmentedSetOf(int codePoint)`: adds Hanb, Jpan and Kore, and turns Common and
  Inherited into every script
- `ScriptSet ResolvedSetOf(string text)` and `bool IsSingleScript(string text)`
- `ScriptSet Recommended`: UAX #31 Table 5
- `string Code(UnicodeScript)`, `string Name(UnicodeScript)`, `bool TryParse(string, out UnicodeScript)`

**`Identifiers`**, sections 3.1, 5.2 and 5.3

- `IdentifierCheck Check(string text, RestrictionLevel permitted)`: the whole check in order,
  with an overload taking a `Func<int, bool>` profile. The result carries `Input`, `Normalized`,
  `Skeleton`, `Level`, `NumberSystems`, `Problems` (a flags enum: `OutsideProfile`,
  `ExceedsRestrictionLevel`, `MixedNumbers`) and `IsAccepted`
- `IdentifierType TypeOf(int codePoint)`: a flags enum of the twelve types
- `IdentifierStatus StatusOf(int codePoint)`: `Allowed` or `Restricted`
- `bool IsAllowed(string text)`
- `RestrictionLevel RestrictionLevelOf(string text)` against the General Security Profile, and
  an overload taking a `Func<int, bool>` profile of your own
- `bool HasMixedNumbers(string text)`, `IReadOnlyList<int> NumberSystemsOf(string text)` as the
  zero of each system, `int? DecimalDigitValue(int codePoint)`

**`Normalization`**, UAX #15

- `string Nfd(string)`, `Nfc`, `Nfkd`, `Nfkc`
- `int CombiningClass(int codePoint)`

**`UnicodeData`**

- `string Version`, currently `17.0.0`

**`ScriptSet`** is an immutable 256-bit set of `UnicodeScript`: `Empty`, `All`,
`Of(params UnicodeScript[])`, `Contains`, `Add`, `Remove`, `Intersect`, `Union`, `Intersects`,
`IsSubsetOf`, `IsEmpty`, `IsAll`, `Count`, equality, enumeration, and a `ToString` that prints
`{Latn, Cyrl}` in the specification's own notation.

**`UnicodeScript`** is the generated enum of the 176 Script property values plus
`HanWithBopomofo`, `Japanese` and `Korean`. Its numeric values are set positions and may change
between versions, so persist `Scripts.Code` rather than the number.

**`RestrictionLevel`** is numbered as the specification numbers it, `AsciiOnly` at 1 through
`Unrestricted` at 6, so `level <= permitted` is the acceptance test.

Every enum but the two flags enums has an `Undefined = 0` sentinel: the value an uninitialised field has, never
returned by the library and refused wherever one of its values is expected. `UnicodeScript.Unknown`
is not that sentinel but the real property value of an unassigned code point. The exceptions are
the flags enums `IdentifierType` and `IdentifierProblems`, whose zero is `None`: "none of these",
which masking can legitimately produce, and so not a value nobody chose.

## Licence

The code is under the Mozilla Public License 2.0. The data under `ucd/` and the tables generated from it are under
the Unicode Licence v3, reproduced in `LICENSE.unicode`, and the algorithms follow UTS #39 and the
Unicode-licensed ICU4J `SpoofChecker`.
