// SPDX-License-Identifier: LGPL-2.1-or-later
// Copyright (c) 2026 Henrik O. Sørensen

import com.ibm.icu.lang.UCharacter;
import com.ibm.icu.lang.UCharacterCategory;
import com.ibm.icu.lang.UProperty;
import com.ibm.icu.lang.UScript;
import com.ibm.icu.text.Normalizer2;
import com.ibm.icu.text.SpoofChecker;
import com.ibm.icu.text.UnicodeSet;
import com.ibm.icu.util.VersionInfo;

import java.io.BufferedWriter;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.BitSet;
import java.util.EnumSet;
import java.util.HashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Map;
import java.util.Random;
import java.util.TreeSet;

/**
 * Writes Squint.Test/Fixtures/icu4j-oracle.json from ICU4J, in the same layout tools/icu-oracle.py
 * writes from ICU4C, so the same tests read both. ICU4J's SpoofChecker is the code the library's
 * script-set and restriction-level logic was ported from, which makes this the closer oracle of
 * the two; ICU4C is the independent one.
 *
 *   javac -cp icu4j-78.3.jar tools/Icu4jOracle.java -d out
 *   java -cp icu4j-78.3.jar:out Icu4jOracle ucd/confusables.txt Squint.Test/Fixtures/icu4j-oracle.json
 *
 * The strings are the confusable table's keys, the curated vectors, and seeded random strings
 * from the same pool as the Python oracle, so the two fixtures cover the same ground without
 * being byte-identical.
 */
public final class Icu4jOracle {

    private static final String[] IDENTIFIER_TYPES = {
        "NotCharacter", "Deprecated", "DefaultIgnorable", "NotNfkc", "NotXid", "Exclusion",
        "Obsolete", "Technical", "UncommonUse", "LimitedUse", "Inclusion", "Recommended",
    };

    private static final Map<SpoofChecker.RestrictionLevel, String> LEVELS = new HashMap<>();

    static {
        LEVELS.put(SpoofChecker.RestrictionLevel.ASCII, "AsciiOnly");
        LEVELS.put(SpoofChecker.RestrictionLevel.SINGLE_SCRIPT_RESTRICTIVE, "SingleScript");
        LEVELS.put(SpoofChecker.RestrictionLevel.HIGHLY_RESTRICTIVE, "HighlyRestrictive");
        LEVELS.put(SpoofChecker.RestrictionLevel.MODERATELY_RESTRICTIVE, "ModeratelyRestrictive");
        LEVELS.put(SpoofChecker.RestrictionLevel.MINIMALLY_RESTRICTIVE, "MinimallyRestrictive");
        LEVELS.put(SpoofChecker.RestrictionLevel.UNRESTRICTIVE, "Unrestricted");
    }

    private final SpoofChecker confusable = new SpoofChecker.Builder().setChecks(SpoofChecker.CONFUSABLE).build();
    private final SpoofChecker underProfile;
    private final SpoofChecker underEverything;
    private final Normalizer2[] normalizers = {
        Normalizer2.getNFDInstance(), Normalizer2.getNFCInstance(), Normalizer2.getNFKDInstance(), Normalizer2.getNFKCInstance(),
    };
    private static final String[] FORMS = { "nfd", "nfc", "nfkd", "nfkc" };

    private Icu4jOracle() {
        UnicodeSet profile = new UnicodeSet().addAll(SpoofChecker.RECOMMENDED).addAll(SpoofChecker.INCLUSION);
        underProfile = levelChecker(profile);
        underEverything = levelChecker(new UnicodeSet(0, 0x10FFFF));
    }

    private static SpoofChecker levelChecker(UnicodeSet allowed) {
        return new SpoofChecker.Builder()
                .setChecks(SpoofChecker.RESTRICTION_LEVEL | SpoofChecker.MIXED_NUMBERS)
                .setRestrictionLevel(SpoofChecker.RestrictionLevel.MINIMALLY_RESTRICTIVE)
                .setAllowedChars(allowed)
                .build();
    }

    public static void main(String[] args) throws IOException {
        if (args.length < 2) {
            System.err.println("usage: Icu4jOracle <confusables.txt> <output.json>");
            System.exit(2);
        }

        Icu4jOracle icu = new Icu4jOracle();
        System.out.println("ICU4J " + threePart(VersionInfo.ICU_VERSION) + ", Unicode " + threePart(UCharacter.getUnicodeVersion()));

        Random rng = new Random(0x39);
        LinkedHashSet<String> unique = new LinkedHashSet<>(Inputs.curated());
        for (int key : Inputs.confusableKeys(Path.of(args[0]))) {
            unique.add(new String(Character.toChars(key)));
        }
        unique.addAll(Inputs.random(rng, 4000));
        List<String> strings = new ArrayList<>(unique);

        try (BufferedWriter out = Files.newBufferedWriter(Path.of(args[1]), StandardCharsets.UTF_8)) {
            Json json = new Json(out);
            json.raw("{");
            json.key("icuVersion").string(threePart(VersionInfo.ICU_VERSION)).raw(",");
            json.key("unicodeVersion").string(threePart(UCharacter.getUnicodeVersion())).raw(",");

            json.key("skeleton").raw("[");
            for (int i = 0; i < strings.size(); i++) {
                String s = strings.get(i);
                json.sep(i).raw("[").string(s).raw(",").string(icu.confusable.getSkeleton(s)).raw("]");
            }
            json.raw("],");

            json.key("restrictionLevel").raw("[");
            for (int i = 0; i < strings.size(); i++) {
                String s = strings.get(i);
                json.sep(i).raw("[").string(s).raw(",").string(icu.level(s, true)).raw(",").string(icu.level(s, false)).raw("]");
            }
            json.raw("],");

            json.key("numerics").raw("[");
            for (int i = 0; i < strings.size(); i++) {
                String s = strings.get(i);
                json.sep(i).raw("[").string(s).raw(",").ints(icu.numerics(s)).raw("]");
            }
            json.raw("],");

            json.key("confusable").raw("[");
            List<String[]> pairs = icu.confusablePairs(strings, rng);
            for (int i = 0; i < pairs.size(); i++) {
                String[] pair = pairs.get(i);
                json.sep(i).raw("[").string(pair[0]).raw(",").string(pair[1]).raw(",").raw(Integer.toString(icu.confusable.areConfusable(pair[0], pair[1]))).raw("]");
            }
            json.raw("],");

            for (int f = 0; f < FORMS.length; f++) {
                Normalizer2 normalizer = icu.normalizers[f];
                json.key(FORMS[f]).raw("[");
                int count = 0;
                for (int cp = 0; cp < 0x110000; cp++) {
                    if (cp >= 0xD800 && cp <= 0xDFFF) {
                        continue;
                    }
                    if (cp >= 0xAC00 && cp <= 0xD7A3 && (cp - 0xAC00) % 7 != 0) {
                        continue;
                    }
                    String s = new String(Character.toChars(cp));
                    String n = normalizer.normalize(s);
                    if (!n.equals(s)) {
                        json.sep(count++).raw("[").string(s).raw(",").string(n).raw("]");
                    }
                }
                for (int i = 0; i < 5000; i++) {
                    String s = Inputs.markSequence(rng);
                    json.sep(count++).raw("[").string(s).raw(",").string(normalizer.normalize(s)).raw("]");
                }
                json.raw("],");
            }

            json.key("properties").raw("[");
            String previous = null;
            int start = 0;
            int runs = 0;
            for (int cp = 0; cp < 0x110000; cp++) {
                String value = icu.properties(cp);
                if (!value.equals(previous)) {
                    if (previous != null) {
                        json.sep(runs++).raw("[").raw(Integer.toString(start)).raw(",").raw(Integer.toString(cp - 1)).raw(",").raw(previous).raw("]");
                    }
                    previous = value;
                    start = cp;
                }
            }
            json.sep(runs).raw("[").raw(Integer.toString(start)).raw(",").raw("1114111").raw(",").raw(previous).raw("]");
            json.raw("]}");
        }

        System.out.println("wrote " + args[1] + " (" + Files.size(Path.of(args[1])) + " bytes)");
    }

    /** major.minor.milli, the form the Python oracle and the library use. */
    private static String threePart(VersionInfo version) {
        return version.getMajor() + "." + version.getMinor() + "." + version.getMilli();
    }

    private String level(String s, boolean profile) {
        SpoofChecker.CheckResult result = new SpoofChecker.CheckResult();
        (profile ? underProfile : underEverything).failsChecks(s, result);
        return LEVELS.get(result.restrictionLevel);
    }

    private List<Integer> numerics(String s) {
        SpoofChecker.CheckResult result = new SpoofChecker.CheckResult();
        underEverything.failsChecks(s, result);
        TreeSet<Integer> zeros = new TreeSet<>();
        for (String item : result.numerics) {
            zeros.add(item.codePointAt(0));
        }
        return new ArrayList<>(zeros);
    }

    private List<String[]> confusablePairs(List<String> strings, Random rng) {
        Map<String, List<String>> buckets = new HashMap<>();
        for (String s : strings) {
            buckets.computeIfAbsent(confusable.getSkeleton(s), k -> new ArrayList<>()).add(s);
        }
        List<String[]> pairs = new ArrayList<>();
        for (List<String> members : buckets.values()) {
            for (int i = 0; i < Math.min(members.size() - 1, 3); i++) {
                pairs.add(new String[] { members.get(i), members.get(i + 1) });
            }
        }
        for (int i = 0; i < 1500; i++) {
            String a = strings.get(rng.nextInt(strings.size()));
            pairs.add(new String[] { a, confusable.getSkeleton(a) });
        }
        for (int i = 0; i < 500; i++) {
            pairs.add(new String[] { strings.get(rng.nextInt(strings.size())), strings.get(rng.nextInt(strings.size())) });
        }
        return pairs;
    }

    /** The six per-code-point fields, already JSON-encoded, as the run value. */
    private String properties(int cp) {
        String script = UScript.getShortName(UCharacter.getIntPropertyValue(cp, UProperty.SCRIPT));

        BitSet extensions = new BitSet();
        UScript.getScriptExtensions(cp, extensions);
        TreeSet<String> codes = new TreeSet<>();
        for (int i = extensions.nextSetBit(0); i >= 0; i = extensions.nextSetBit(i + 1)) {
            codes.add(UScript.getShortName(i));
        }

        EnumSet<UCharacter.IdentifierType> types = EnumSet.noneOf(UCharacter.IdentifierType.class);
        UCharacter.getIdentifierTypes(cp, types);
        TreeSet<String> typeNames = new TreeSet<>();
        for (UCharacter.IdentifierType type : types) {
            typeNames.add(IDENTIFIER_TYPES[type.ordinal()]);
        }

        String status = UCharacter.getIntPropertyValue(cp, UProperty.IDENTIFIER_STATUS) == 1 ? "Allowed" : "Restricted";
        boolean ignorable = UCharacter.hasBinaryProperty(cp, UProperty.DEFAULT_IGNORABLE_CODE_POINT);
        int digit = UCharacter.getType(cp) == UCharacterCategory.DECIMAL_DIGIT_NUMBER ? UCharacter.getNumericValue(cp) : -1;

        return Json.quote(script) + "," + Json.quote(String.join(" ", codes)) + "," + Json.quote(String.join(" ", typeNames))
                + "," + Json.quote(status) + "," + ignorable + "," + digit;
    }

    /** A minimal ASCII-only JSON writer, so the fixture survives any editor. */
    private static final class Json {
        private final BufferedWriter out;

        Json(BufferedWriter out) {
            this.out = out;
        }

        Json raw(String text) throws IOException {
            out.write(text);
            return this;
        }

        Json key(String name) throws IOException {
            return raw(quote(name)).raw(":");
        }

        Json sep(int index) throws IOException {
            return index > 0 ? raw(",") : this;
        }

        Json string(String value) throws IOException {
            return raw(quote(value));
        }

        Json ints(List<Integer> values) throws IOException {
            raw("[");
            for (int i = 0; i < values.size(); i++) {
                sep(i).raw(Integer.toString(values.get(i)));
            }
            return raw("]");
        }

        static String quote(String value) {
            StringBuilder b = new StringBuilder(value.length() + 2).append('"');
            for (int i = 0; i < value.length(); i++) {
                char c = value.charAt(i);
                if (c == '"' || c == '\\') {
                    b.append('\\').append(c);
                } else if (c >= 0x20 && c < 0x7F) {
                    b.append(c);
                } else {
                    b.append(String.format("\\u%04X", (int) c));
                }
            }
            return b.append('"').toString();
        }
    }

    /** The same inputs as tools/icu-oracle.py: curated vectors, the table's keys, a character pool. */
    private static final class Inputs {
        static List<String> curated() {
            return List.of(
                "", "a", "henrik", "søren", "hеnrik", "paypal", "pаypаl", "scope", "ѕсоре", "modern", "rnodern",
                "desparejado", "ԁеѕрагејаԁо", "dеsраrејаdо", "ǉeto", "ljeto", "ǆ", "dž", "ž", "ž",
                "Circle", "СігсӀе", "Сirсlе", "Circ1e", "C𝗂𝗋𝖼𝗅𝖾", "〆切", "ねガ",
                "aγ♥", "γ", "aアー", "aअ", "aγ", "a♥", "a〼", "aー〼", "aー〼ア", "アaー〼", "a1١", "a1١۱", "١ー〼aア1१۱", "aアー〼1१١۱",
                "Ωmega", "Teχ", "HλLF-LIFE", "Toys-Я-Us", "aⵣ", "aᏣ", "aא", "a한", "a漢", "aㄅ", "a漢ㄅ", "a한漢", "aひ漢カ",
                "hen‍rik", "hen​rik", "­henrik", "henrik﻿", "1ove", "00PS", "ʹidentifier'", "֜",
                "⩴", "⑾", "ﷻ", "ಃ", "Α", "Ꮟ", "\"", "1", "१", "1१", "١۱", "²", "Ⅳ", "①",
                "𝗉𝖺𝗒𝗉𝖺𝗅", "hello 𝐩𝐚𝐲𝐩𝐚𝐥 world", "pay​pal");
        }

        static List<Integer> confusableKeys(Path confusables) throws IOException {
            List<Integer> keys = new ArrayList<>();
            for (String line : Files.readAllLines(confusables, StandardCharsets.UTF_8)) {
                if (line.startsWith("﻿")) {
                    line = line.substring(1);
                }
                int semicolon = line.indexOf(';');
                if (semicolon > 0 && line.substring(0, semicolon).trim().matches("[0-9A-F]+")) {
                    keys.add(Integer.parseInt(line.substring(0, semicolon).trim(), 16));
                }
            }
            return keys;
        }

        static List<String> pool() {
            List<String> chars = new ArrayList<>();
            for (int c = 'a'; c <= 'z'; c++) {
                chars.add(String.valueOf((char) c));
            }
            chars.addAll(List.of("0", "1", "5", "-", ".", "_", "'", "|"));
            chars.addAll(List.of("å", "ø", "æ", "ß", "ı", "þ", "đ", "ł", "č", "ž", "ǆ", "ǉ", "ĳ"));
            for (int c = 0x0430; c < 0x0450; c++) {
                chars.add(String.valueOf((char) c));
            }
            chars.addAll(List.of("і", "Ӏ", "ԁ", "ѕ"));
            for (int c = 0x03B1; c < 0x03CA; c++) {
                chars.add(String.valueOf((char) c));
            }
            chars.addAll(List.of("Ω", "λ"));
            chars.addAll(List.of("́", "̀", "̈", "̣", "̧", "̌", "̆", "̇", "ͅ", "̈́"));
            chars.addAll(List.of("​", "‌", "‍", "­", "﻿", "⁠", "͏"));
            chars.addAll(List.of("٠", "١", "۰", "۱", "०", "१", "৪", "０", "１", "𝟘", "²", "Ⅰ", "①"));
            chars.addAll(List.of("א", "ב", "ְ", "ا", "ب", "َ", "अ", "क", "़", "ก", "ั", "Ꭰ", "Ꮟ", "ⵣ"));
            chars.addAll(List.of("一", "切", "う", "ね", "ガ", "ー", "〼", "々", "ㄅ", "가", "한", "ᄀ", "ᅡ"));
            chars.addAll(List.of("☃", "♥", "©", "Ω", "Å", "⫝̸", "ẛ", "ấ", "ཱི", "𝗂", "😀", "", "￿", "𝅗𝅥"));
            return chars;
        }

        static List<String> random(Random rng, int count) {
            List<String> chars = pool();
            List<String> result = new ArrayList<>();
            for (int i = 0; i < count; i++) {
                int length = 1 + rng.nextInt(7);
                StringBuilder b = new StringBuilder();
                for (int j = 0; j < length; j++) {
                    b.append(chars.get(rng.nextInt(chars.size())));
                }
                result.add(b.toString());
            }
            return result;
        }

        private static final String[] MARKS = {
            "a", "e", "q", "å", "ấ", "ṩ", "ཱི", "각", "퓛", "̈́", "́", "̣", "̧",
            "̨", "̇", "̛", "ְ", "ֹ", "ཱ", "ི", "़", "̀", "̈", "ͅ",
            "𝅥", "𝅗𝅥", "Ω", "Å", "ʹ", "·", "क़", "ේ", "⫝̸",
            "ᄀ", "ᅡ", "ᆨ", "क", "ﬁ", "Ａ", "²", "\uD835\uDDC9", "ẛ", "\u00A0", "①", "㌀", "ﷺ", "ཷ", "\uD83C\uDD00",
        };

        static String markSequence(Random rng) {
            int length = 1 + rng.nextInt(8);
            StringBuilder b = new StringBuilder();
            for (int i = 0; i < length; i++) {
                b.append(MARKS[rng.nextInt(MARKS.length)]);
            }
            return b.toString();
        }
    }
}
