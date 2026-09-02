#!/usr/bin/env python3
# SPDX-License-Identifier: LGPL-2.1-or-later
# Copyright (c) 2026 Henrik O. Sørensen
"""Writes Squint.Test/Fixtures/icu-oracle.json by asking a built ICU4C the same questions the
library answers, so the tests compare against a second implementation at the same Unicode
version rather than against this author's reading of the specification.

    tools/icu-oracle.py <icu lib dir> [<output path>]

The lib dir is the ``lib/`` of an ICU4C build (``runConfigureICU`` + ``make``); the symbols
carry ICU's version suffix, which this script discovers. Everything comes from ICU's C API
through ctypes: the four normalisers, the spoof checker (skeleton, confusable class, restriction
level, mixed numbers), and the per-code-point properties the tables are generated from (Script,
Script_Extensions, Identifier_Type, Identifier_Status, Default_Ignorable_Code_Point, decimal
digit value), the last as runs over the whole code space.

The strings are the confusable table's own keys, the specification's and ICU's test vectors, and
random strings from a pool of characters chosen to exercise every branch: several scripts, several
number systems, combining marks, default ignorables, East Asian writing systems, symbols. The
random stream is seeded, so regenerating the fixture from the same ICU gives the same file.
"""

import ctypes
import json
import os
import random
import re
import sys


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2

    lib_dir = sys.argv[1]
    data_dir = os.environ.get("ICU_DATA")
    repo = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    output = sys.argv[2] if len(sys.argv) > 2 else os.path.join(repo, "Squint.Test", "Fixtures", "icu4c-oracle.json")

    icu = Icu(lib_dir, data_dir)
    print(f"ICU {icu.version}, Unicode {icu.unicode_version}")

    keys = confusable_keys(os.path.join(repo, "ucd", "confusables.txt"))
    rng = random.Random(0x39)
    strings = curated_strings() + [chr(k) for k in keys] + random_strings(rng, 4000)
    unique = []
    seen = set()
    for s in strings:
        if s not in seen:
            seen.add(s)
            unique.append(s)
    strings = unique

    fixture = {
        "icuVersion": icu.version,
        "unicodeVersion": icu.unicode_version,
        "skeleton": [[s, icu.skeleton(s)] for s in strings],
        "restrictionLevel": [[s, icu.restriction_level(s, profile=True), icu.restriction_level(s, profile=False)] for s in strings],
        "numerics": [[s, icu.numerics(s)] for s in strings],
        "confusable": confusable_pairs(icu, strings, rng),
        "nfd": normalization_cases(icu, rng, icu.nfd),
        "nfc": normalization_cases(icu, rng, icu.nfc),
        "nfkd": normalization_cases(icu, rng, icu.nfkd),
        "nfkc": normalization_cases(icu, rng, icu.nfkc),
        "properties": property_runs(icu),
    }

    with open(output, "w", encoding="utf-8") as f:
        json.dump(fixture, f, ensure_ascii=True, separators=(",", ":"))
    print(f"wrote {output} ({os.path.getsize(output)} bytes)")
    return 0


# ---------------------------------------------------------------------------------------------
# ICU through ctypes

U_ZERO_ERROR = 0
USPOOF_RESTRICTION_LEVEL = 16
USPOOF_MIXED_NUMBERS = 128
USPOOF_CONFUSABLE = 7
USPOOF_MINIMALLY_RESTRICTIVE = 0x50000000
UCHAR_DEFAULT_IGNORABLE_CODE_POINT = 5
UCHAR_SCRIPT = 0x100A
UCHAR_IDENTIFIER_STATUS = 0x1019
U_DECIMAL_DIGIT_NUMBER = 9

RESTRICTION_LEVELS = {
    0x10000000: "AsciiOnly",
    0x20000000: "SingleScript",
    0x30000000: "HighlyRestrictive",
    0x40000000: "ModeratelyRestrictive",
    0x50000000: "MinimallyRestrictive",
    0x60000000: "Unrestricted",
}

IDENTIFIER_TYPES = [
    "NotCharacter", "Deprecated", "DefaultIgnorable", "NotNfkc", "NotXid", "Exclusion",
    "Obsolete", "Technical", "UncommonUse", "LimitedUse", "Inclusion", "Recommended",
]


class Icu:
    def __init__(self, lib_dir, data_dir):
        # The libraries reference each other by install name; loading them in dependency order
        # with RTLD_GLOBAL lets dyld satisfy the references from what is already loaded. A build
        # without the data tools has only the stub data library beside the others; the real data
        # then comes from the .dat file in the directory ICU_DATA names, which the source tarball
        # ships prebuilt under source/data/in.
        self.data = ctypes.CDLL(find_lib(lib_dir, "libicudata"), mode=ctypes.RTLD_GLOBAL)
        self.uc = ctypes.CDLL(find_lib(lib_dir, "libicuuc"), mode=ctypes.RTLD_GLOBAL)
        self.i18n = ctypes.CDLL(find_lib(lib_dir, "libicui18n"), mode=ctypes.RTLD_GLOBAL)
        self.suffix = self._find_suffix()

        if data_dir:
            self.fn(self.uc, "u_setDataDirectory", None, [ctypes.c_char_p])(data_dir.encode("utf-8"))

        buf = ctypes.create_string_buffer(4)
        self.fn(self.uc, "u_getVersion")(buf)
        self.version = ".".join(str(b) for b in buf.raw[:3])
        self.fn(self.uc, "u_getUnicodeVersion")(buf)
        self.unicode_version = ".".join(str(b) for b in buf.raw[:3])

        self.uspoof_getSkeleton = self.fn(self.i18n, "uspoof_getSkeleton", ctypes.c_int32,
                                          [ctypes.c_void_p, ctypes.c_uint32, ctypes.c_void_p, ctypes.c_int32, ctypes.c_void_p, ctypes.c_int32, ctypes.POINTER(ctypes.c_int)])
        self.uspoof_areConfusable = self.fn(self.i18n, "uspoof_areConfusable", ctypes.c_int32,
                                            [ctypes.c_void_p, ctypes.c_void_p, ctypes.c_int32, ctypes.c_void_p, ctypes.c_int32, ctypes.POINTER(ctypes.c_int)])
        self.uspoof_check2 = self.fn(self.i18n, "uspoof_check2", ctypes.c_int32,
                                     [ctypes.c_void_p, ctypes.c_void_p, ctypes.c_int32, ctypes.c_void_p, ctypes.POINTER(ctypes.c_int)])
        self.uspoof_getCheckResultRestrictionLevel = self.fn(self.i18n, "uspoof_getCheckResultRestrictionLevel", ctypes.c_int, [ctypes.c_void_p, ctypes.POINTER(ctypes.c_int)])
        self.uspoof_getCheckResultNumerics = self.fn(self.i18n, "uspoof_getCheckResultNumerics", ctypes.c_void_p, [ctypes.c_void_p, ctypes.POINTER(ctypes.c_int)])
        self.uset_size = self.fn(self.uc, "uset_size", ctypes.c_int32, [ctypes.c_void_p])
        self.uset_charAt = self.fn(self.uc, "uset_charAt", ctypes.c_int32, [ctypes.c_void_p, ctypes.c_int32])
        self.unorm2_normalize = self.fn(self.uc, "unorm2_normalize", ctypes.c_int32,
                                        [ctypes.c_void_p, ctypes.c_void_p, ctypes.c_int32, ctypes.c_void_p, ctypes.c_int32, ctypes.POINTER(ctypes.c_int)])
        self.u_getIntPropertyValue = self.fn(self.uc, "u_getIntPropertyValue", ctypes.c_int32, [ctypes.c_int32, ctypes.c_int])
        self.u_hasBinaryProperty = self.fn(self.uc, "u_hasBinaryProperty", ctypes.c_bool, [ctypes.c_int32, ctypes.c_int])
        self.u_charType = self.fn(self.uc, "u_charType", ctypes.c_int8, [ctypes.c_int32])
        self.u_charDigitValue = self.fn(self.uc, "u_charDigitValue", ctypes.c_int32, [ctypes.c_int32])
        self.u_getIDTypes = self.fn(self.uc, "u_getIDTypes", ctypes.c_int32, [ctypes.c_int32, ctypes.POINTER(ctypes.c_int), ctypes.c_int32, ctypes.POINTER(ctypes.c_int)])
        self.uscript_getScriptExtensions = self.fn(self.uc, "uscript_getScriptExtensions", ctypes.c_int32, [ctypes.c_int32, ctypes.POINTER(ctypes.c_int), ctypes.c_int32, ctypes.POINTER(ctypes.c_int)])
        self.uscript_getShortName = self.fn(self.uc, "uscript_getShortName", ctypes.c_char_p, [ctypes.c_int])

        err = ctypes.c_int(U_ZERO_ERROR)
        self.nfd = self.fn(self.uc, "unorm2_getNFDInstance", ctypes.c_void_p, [ctypes.POINTER(ctypes.c_int)])(ctypes.byref(err))
        self.nfc = self.fn(self.uc, "unorm2_getNFCInstance", ctypes.c_void_p, [ctypes.POINTER(ctypes.c_int)])(ctypes.byref(err))
        self.nfkd = self.fn(self.uc, "unorm2_getNFKDInstance", ctypes.c_void_p, [ctypes.POINTER(ctypes.c_int)])(ctypes.byref(err))
        self.nfkc = self.fn(self.uc, "unorm2_getNFKCInstance", ctypes.c_void_p, [ctypes.POINTER(ctypes.c_int)])(ctypes.byref(err))
        self.check(err, "unorm2_get*Instance")

        uspoof_open = self.fn(self.i18n, "uspoof_open", ctypes.c_void_p, [ctypes.POINTER(ctypes.c_int)])
        uspoof_setChecks = self.fn(self.i18n, "uspoof_setChecks", None, [ctypes.c_void_p, ctypes.c_int32, ctypes.POINTER(ctypes.c_int)])
        uspoof_setRestrictionLevel = self.fn(self.i18n, "uspoof_setRestrictionLevel", None, [ctypes.c_void_p, ctypes.c_int])
        uspoof_setAllowedChars = self.fn(self.i18n, "uspoof_setAllowedChars", None, [ctypes.c_void_p, ctypes.c_void_p, ctypes.POINTER(ctypes.c_int)])
        uspoof_getRecommendedSet = self.fn(self.i18n, "uspoof_getRecommendedSet", ctypes.c_void_p, [ctypes.POINTER(ctypes.c_int)])
        uspoof_getInclusionSet = self.fn(self.i18n, "uspoof_getInclusionSet", ctypes.c_void_p, [ctypes.POINTER(ctypes.c_int)])
        uset_openEmpty = self.fn(self.uc, "uset_openEmpty", ctypes.c_void_p, [])
        uset_addAll = self.fn(self.uc, "uset_addAll", ctypes.c_void_p, [ctypes.c_void_p, ctypes.c_void_p])
        uset_addRange = self.fn(self.uc, "uset_addRange", None, [ctypes.c_void_p, ctypes.c_int32, ctypes.c_int32])
        uspoof_openCheckResult = self.fn(self.i18n, "uspoof_openCheckResult", ctypes.c_void_p, [ctypes.POINTER(ctypes.c_int)])

        # One checker for skeletons and confusability, with every check on.
        self.confusable_checker = uspoof_open(ctypes.byref(err))
        self.check(err, "uspoof_open")
        uspoof_setChecks(self.confusable_checker, USPOOF_CONFUSABLE, ctypes.byref(err))

        # One for restriction levels under the General Security Profile (Recommended + Inclusion,
        # which is Identifier_Status=Allowed) and one under a profile that admits every code point,
        # so the script logic is exercised on strings the profile would otherwise stop at step 1.
        profile = uset_openEmpty()
        uset_addAll(profile, uspoof_getRecommendedSet(ctypes.byref(err)))
        uset_addAll(profile, uspoof_getInclusionSet(ctypes.byref(err)))
        everything = uset_openEmpty()
        uset_addRange(everything, 0, 0x10FFFF)

        self.level_checkers = {}
        for name, allowed in (("profile", profile), ("everything", everything)):
            checker = uspoof_open(ctypes.byref(err))
            uspoof_setChecks(checker, USPOOF_RESTRICTION_LEVEL | USPOOF_MIXED_NUMBERS, ctypes.byref(err))
            uspoof_setRestrictionLevel(checker, USPOOF_MINIMALLY_RESTRICTIVE)
            uspoof_setAllowedChars(checker, allowed, ctypes.byref(err))
            self.check(err, "uspoof_setAllowedChars")
            self.level_checkers[name] = checker

        self.result = uspoof_openCheckResult(ctypes.byref(err))
        self.check(err, "uspoof_openCheckResult")

        # The identifier-type enum order this script assumes, checked on two characters whose
        # single type is beyond doubt.
        assert self.id_types(0x00AD) == ["DefaultIgnorable"], self.id_types(0x00AD)
        assert self.id_types(0x13A0) == ["LimitedUse"], self.id_types(0x13A0)
        assert self.id_types(0x0041) == ["Recommended"], self.id_types(0x0041)

    def _find_suffix(self):
        # ctypes does not enumerate symbols; probe the renamed and plain forms of one function.
        for suffix in ("_" + str(v) for v in range(60, 120)):
            if hasattr(self.uc, "u_getVersion" + suffix):
                return suffix
        if hasattr(self.uc, "u_getVersion"):
            return ""
        raise RuntimeError("Cannot find u_getVersion in libicuuc.")

    def fn(self, lib, name, restype=None, argtypes=None):
        f = getattr(lib, name + self.suffix)
        f.restype = restype
        if argtypes is not None:
            f.argtypes = argtypes
        return f

    @staticmethod
    def check(err, what):
        if err.value > U_ZERO_ERROR:
            raise RuntimeError(f"{what} failed with UErrorCode {err.value}")

    def _utf16(self, s):
        # ctypes maps c_wchar_p to UTF-32 on macOS, so pass UTF-16 code units by hand.
        units = s.encode("utf-16-le", "surrogatepass")
        buf = ctypes.create_string_buffer(units, len(units))
        return ctypes.cast(buf, ctypes.c_void_p), len(units) // 2, buf

    def skeleton(self, s):
        src, length, keep = self._utf16(s)
        dest = ctypes.create_string_buffer(4096)
        err = ctypes.c_int(U_ZERO_ERROR)
        n = self.uspoof_getSkeleton(self.confusable_checker, 0, src, length, dest, 2048, ctypes.byref(err))
        self.check(err, "uspoof_getSkeleton")
        return dest.raw[: n * 2].decode("utf-16-le", "surrogatepass")

    def confusable(self, a, b):
        src1, len1, keep1 = self._utf16(a)
        src2, len2, keep2 = self._utf16(b)
        err = ctypes.c_int(U_ZERO_ERROR)
        flags = self.uspoof_areConfusable(self.confusable_checker, src1, len1, src2, len2, ctypes.byref(err))
        self.check(err, "uspoof_areConfusable")
        return flags

    def _check2(self, s, profile):
        src, length, keep = self._utf16(s)
        err = ctypes.c_int(U_ZERO_ERROR)
        checker = self.level_checkers["profile" if profile else "everything"]
        self.uspoof_check2(checker, src, length, self.result, ctypes.byref(err))
        self.check(err, "uspoof_check2")

    def restriction_level(self, s, profile):
        self._check2(s, profile)
        err = ctypes.c_int(U_ZERO_ERROR)
        level = self.uspoof_getCheckResultRestrictionLevel(self.result, ctypes.byref(err))
        self.check(err, "uspoof_getCheckResultRestrictionLevel")
        return RESTRICTION_LEVELS[level]

    def numerics(self, s):
        self._check2(s, False)
        err = ctypes.c_int(U_ZERO_ERROR)
        uset = self.uspoof_getCheckResultNumerics(self.result, ctypes.byref(err))
        self.check(err, "uspoof_getCheckResultNumerics")
        return sorted(self.uset_charAt(uset, i) for i in range(self.uset_size(uset)))

    def normalize(self, normalizer, s):
        src, length, keep = self._utf16(s)
        dest = ctypes.create_string_buffer(4096)
        err = ctypes.c_int(U_ZERO_ERROR)
        n = self.unorm2_normalize(normalizer, src, length, dest, 2048, ctypes.byref(err))
        self.check(err, "unorm2_normalize")
        return dest.raw[: n * 2].decode("utf-16-le", "surrogatepass")

    def script(self, cp):
        return self.uscript_getShortName(self.u_getIntPropertyValue(cp, UCHAR_SCRIPT)).decode("ascii")

    def script_extensions(self, cp):
        codes = (ctypes.c_int * 64)()
        err = ctypes.c_int(U_ZERO_ERROR)
        n = self.uscript_getScriptExtensions(cp, codes, 64, ctypes.byref(err))
        self.check(err, "uscript_getScriptExtensions")
        return sorted(self.uscript_getShortName(codes[i]).decode("ascii") for i in range(n))

    def id_types(self, cp):
        types = (ctypes.c_int * 16)()
        err = ctypes.c_int(U_ZERO_ERROR)
        n = self.u_getIDTypes(cp, types, 16, ctypes.byref(err))
        self.check(err, "u_getIDTypes")
        return sorted(IDENTIFIER_TYPES[types[i]] for i in range(n))

    def id_status(self, cp):
        return "Allowed" if self.u_getIntPropertyValue(cp, UCHAR_IDENTIFIER_STATUS) == 1 else "Restricted"

    def default_ignorable(self, cp):
        return bool(self.u_hasBinaryProperty(cp, UCHAR_DEFAULT_IGNORABLE_CODE_POINT))

    def digit_value(self, cp):
        if self.u_charType(cp) != U_DECIMAL_DIGIT_NUMBER:
            return -1
        return self.u_charDigitValue(cp)


def find_lib(lib_dir, stem):
    directories = [lib_dir, os.path.join(os.path.dirname(lib_dir.rstrip("/")), "stubdata")]
    candidates = []
    for directory in directories:
        if os.path.isdir(directory):
            candidates += [os.path.join(directory, f) for f in os.listdir(directory)
                           if f.startswith(stem + ".") and (f.endswith(".dylib") or ".so" in f)]
    if not candidates:
        raise FileNotFoundError(f"No {stem} in {directories}")
    # Prefer the fully versioned file, which is the real one the symlinks point at.
    candidates.sort(key=len, reverse=True)
    return candidates[0]


# ---------------------------------------------------------------------------------------------
# Inputs

def confusable_keys(path):
    keys = []
    with open(path, encoding="utf-8") as f:
        for line in f:
            m = re.match(r"^([0-9A-F]+)\s*;", line)
            if m:
                keys.append(int(m.group(1), 16))
    return keys


def curated_strings():
    return [
        "", "a", "henrik", "søren", "hеnrik", "paypal", "pаypаl", "scope", "ѕсоре", "modern", "rnodern",
        "desparejado", "ԁеѕрагејаԁо", "dеsраrејаdо", "ǉeto", "ljeto", "ǆ", "dž", "ž", "ž",
        "Circle", "СігсӀе", "Сirсlе", "Circ1e", "C\U0001D5C2\U0001D5CB\U0001D5BC\U0001D5C5\U0001D5BE", "〆切", "ねガ",
        "aγ♥", "γ", "aアー", "aअ", "aγ", "a♥", "a〼", "aー〼", "aー〼ア", "アaー〼", "a1١", "a1١۱", "١ー〼aア1१۱", "aアー〼1१١۱",
        "Ωmega", "Teχ", "HλLF-LIFE", "Toys-Я-Us", "aⵣ", "aᏣ", "aא", "a한", "a漢", "aㄅ", "a漢ㄅ", "a한漢", "aひ漢カ",
        "hen‍rik", "hen​rik", "­henrik", "henrik﻿", "1ove", "00PS", "ʹidentifier'", "֜",
        "⩴", "⑾", "ﷻ", "ಃ", "Α", "Ꮟ", "\"", "1", "१", "1१", "١۱", "²", "Ⅳ", "①",
    ]


def pool():
    chars = []
    chars += [chr(c) for c in range(0x61, 0x7B)] + ["0", "1", "5", "-", ".", "_", "'", "|"]
    chars += ["å", "ø", "æ", "ß", "ı", "þ", "đ", "ł", "č", "ž", "ǆ", "ǉ", "ĳ"]
    chars += [chr(c) for c in range(0x0430, 0x0450)] + ["і", "Ӏ", "ԁ", "ѕ"]
    chars += [chr(c) for c in range(0x03B1, 0x03CA)] + ["Ω", "λ"]
    chars += ["́", "̀", "̈", "̣", "̧", "̌", "̆", "̇", "ͅ", "̈́"]
    chars += ["​", "‌", "‍", "­", "﻿", "⁠", "͏"]
    chars += ["٠", "١", "۰", "۱", "०", "१", "৪", "０", "１", "\U0001D7D8", "²", "Ⅰ", "①"]
    chars += ["א", "ב", "ְ", "ا", "ب", "َ", "अ", "क", "़", "ก", "ั", "Ꭰ", "Ꮟ", "ⵣ"]
    chars += ["一", "切", "う", "ね", "ガ", "ー", "〼", "々", "ㄅ", "가", "한", "ᄀ", "ᅡ"]
    chars += ["☃", "♥", "©", "Ω", "Å", "⫝̸", "ẛ", "ấ", "ཱི", "\U0001D5C2", "\U0001F600", "", "￿", "\U0001D15E"]
    return chars


def random_strings(rng, count):
    chars = pool()
    result = []
    for _ in range(count):
        length = rng.randint(1, 7)
        result.append("".join(rng.choice(chars) for _ in range(length)))
    return result


def confusable_pairs(icu, strings, rng):
    # Pairs from the same skeleton bucket (confusable) and neighbouring buckets (not), so both
    # halves of the classification are represented.
    buckets = {}
    for s in strings:
        buckets.setdefault(icu.skeleton(s), []).append(s)
    pairs = []
    for members in buckets.values():
        if len(members) > 1:
            for i in range(min(len(members) - 1, 3)):
                pairs.append((members[i], members[i + 1]))
    for _ in range(1500):
        a = rng.choice(strings)
        pairs.append((a, icu.skeleton(a)))
    for _ in range(500):
        pairs.append((rng.choice(strings), rng.choice(strings)))
    return [[a, b, icu.confusable(a, b)] for a, b in pairs]


def normalization_cases(icu, rng, normalizer):
    cases = []
    for cp in range(0x110000):
        if 0xD800 <= cp <= 0xDFFF:
            continue
        if 0xAC00 <= cp <= 0xD7A3 and (cp - 0xAC00) % 7 != 0:
            continue
        s = chr(cp)
        n = icu.normalize(normalizer, s)
        if n != s:
            cases.append([s, n])
    marks = ["a", "e", "q", "å", "ấ", "ṩ", "ཱི", "각", "퓛", "̈́", "́", "̣", "̧",
             "̨", "̇", "̛", "ְ", "ֹ", "ཱ", "ི", "़", "̀", "̈", "ͅ",
             "\U0001D165", "\U0001D15E", "Ω", "Å", "ʹ", "·", "क़", "ේ", "⫝̸"]
    for _ in range(5000):
        s = "".join(rng.choice(marks) for _ in range(rng.randint(1, 8)))
        cases.append([s, icu.normalize(normalizer, s)])
    return cases


def property_runs(icu):
    """Runs of [start, end, script, extensions, types, status, ignorable, digit] over the code space."""
    runs = []
    previous = None
    start = 0
    for cp in range(0x110000):
        value = (
            icu.script(cp),
            " ".join(icu.script_extensions(cp)),
            " ".join(icu.id_types(cp)),
            icu.id_status(cp),
            icu.default_ignorable(cp),
            icu.digit_value(cp),
        )
        if value != previous:
            if previous is not None:
                runs.append([start, cp - 1, *previous])
            previous = value
            start = cp
    runs.append([start, 0x10FFFF, *previous])
    return runs


if __name__ == "__main__":
    sys.exit(main())
