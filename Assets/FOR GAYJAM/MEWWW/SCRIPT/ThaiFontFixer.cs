using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using TMPro;

/// <summary>
/// Fixes Thai font issues in TextMeshPro for Itim and other Thai fonts:
/// 1. Vowel and tone mark overlapping (สระกับวรรณยุกต์ทับกัน, ชนหางพยัญชนะ) via <voffset> and <space>.
/// 2. Natural line wrapping / word breaking: inserts Zero-Width Spaces (\u200B) at word boundaries
///    so TextMeshPro wraps long sentences naturally instead of stacking or waiting until sentence end.
/// </summary>
public static class ThaiFontFixer
{
    // Zero-Width Space for invisible word break opportunities
    public const char ZeroWidthSpace = '\u200B';

    // Thai character classifications (Unicode range 0x0E01 - 0x0E5B)
    public const string UpperVowels = "\u0E31\u0E34\u0E35\u0E36\u0E37\u0E47\u0E4D"; // ั ิ ี ึ ื ็ ํ
    public const string ToneMarks = "\u0E48\u0E49\u0E4A\u0E4B\u0E4C\u0E4E";       // ่ ้ ๊ ๋ ์ ๎
    public const string LowerVowels = "\u0E38\u0E39\u0E3A";                        // ุ ู ฺ
    public const string FrontVowels = "\u0E40\u0E41\u0E42\u0E43\u0E44";             // เ แ โ ใ ไ
    public const string FollowVowels = "\u0E30\u0E32\u0E33\u0E45";                   // ะ า ำ ๅ
    public const string TallConsonants = "\u0E1B\u0E1D\u0E1F\u0E2C";                 // ป ฝ ฟ ฬ
    public const string DescenderConsonants = "\u0E0D\u0E10";                     // ญ ฐ

    // Tuned offsets for Itim font
    public const float DefaultToneUpperOffset = 0.24f;      // Vertical lift for tone above upper vowel
    public const float DefaultTallConsonantShift = -0.05f;   // Horizontal shift for mark on tall consonant
    public const float DefaultDescenderOffset = -0.12f;     // Downward shift for lower vowel under ญ/ฐ

    public static bool IsUpperVowel(char c) => UpperVowels.IndexOf(c) >= 0;
    public static bool IsToneMark(char c) => ToneMarks.IndexOf(c) >= 0;
    public static bool IsLowerVowel(char c) => LowerVowels.IndexOf(c) >= 0;
    public static bool IsFrontVowel(char c) => FrontVowels.IndexOf(c) >= 0;
    public static bool IsFollowVowel(char c) => FollowVowels.IndexOf(c) >= 0;
    public static bool IsTallConsonant(char c) => TallConsonants.IndexOf(c) >= 0;
    public static bool IsDescenderConsonant(char c) => DescenderConsonants.IndexOf(c) >= 0;
    public static bool IsThaiCharacter(char c) => c >= '\u0E01' && c <= '\u0E5B';

    // High-frequency Thai vocabulary for dialogue, story, and conversations
    private static readonly HashSet<string> ThaiDictionary = new HashSet<string>()
    {
        // Pronouns & Titles
        "ฉัน", "ผม", "เรา", "เธอ", "เขา", "มัน", "ท่าน", "นาย", "ตัวเอง", "ข้า", "เจ้า", "แก", "มึง", "กู",
        "พี่", "น้อง", "พ่อ", "แม่", "ลูก", "เพื่อน", "คน", "ตัว", "ลุง", "ป้า", "น้า", "อา", "ปู่", "ย่า", "ตา", "ยาย",
        "คุณ", "ผู้ชาย", "ผู้หญิง", "เด็ก", "ผู้ใหญ่", "หมอ", "ครู", "อาจารย์", "หัวหน้า", "ตำรวจ", "คนร้าย", "ทุกคน", "ใคร",

        // Conjunctions & Prepositions
        "และ", "หรือ", "แต่", "ถ้า", "ถ้าหาก", "เพราะ", "เพราะว่า", "ที่", "ซึ่ง", "อัน", "กับ", "แก่", "แด่", "เพื่อ",
        "สำหรับ", "ต่อ", "จาก", "ด้วย", "โดย", "ตาม", "ถึง", "ใน", "บน", "ใต้", "นอก", "ระหว่าง", "ตลอด", "ก่อน", "หลัง",
        "ขณะ", "ตอน", "เมื่อ", "เวลา", "จน", "จนกระทั่ง", "แม้", "แม้ว่า", "ถึงแม้", "ทั้งๆที่", "เหมือน", "เหมือนกับ",
        "ราวกับ", "เท่า", "เท่ากับ", "กว่า", "ดัง", "ดั่ง", "เช่น", "เช่นเดียวกับ", "หาก",

        // Auxiliaries & Modals
        "คือ", "เป็น", "อยู่", "มี", "ได้", "ไป", "มา", "ให้", "จะ", "ต้อง", "ควร", "อาจ", "อาจจะ", "กำลัง", "เคย",
        "ไม่", "ไม่ใช่", "ไม่ได้", "ไม่อยาก", "อยาก", "อยากจะ", "ช่วย", "ขอ", "ลอง", "เริ่ม", "หยุด", "เสร็จ", "พอ",
        "คง", "น่าจะ", "ชอบ", "ยอม", "พร้อม",

        // Common Verbs
        "ทำ", "คิด", "รู้", "รู้สึก", "เห็น", "มอง", "ดู", "ฟัง", "ได้ยิน", "พูด", "บอก", "เล่า", "ถาม", "ตอบ", "อธิบาย",
        "เข้าใจ", "จำ", "ลืม", "รอ", "คอย", "พบ", "เจอ", "หา", "หาย", "ซื้อ", "ขาย", "เอา", "รับ", "ส่ง", "เดิน", "วิ่ง",
        "กระโดด", "ยืน", "นั่ง", "นอน", "ตื่น", "กิน", "ดื่ม", "ทาน", "ใช้", "ทำงาน", "เล่น", "สู้", "ต่อสู้", "โจมตี", "ป้องกัน",
        "ร้อง", "ร้องไห้", "หัวเราะ", "ยิ้ม", "โกรธ", "กลัว", "ตกใจ", "เสียใจ", "ดีใจ", "รัก", "เกลียด", "รอด", "ตาย", "ฆ่า",
        "เกิด", "เปลี่ยน", "เปลี่ยนแปลง", "เลือก", "ตัดสินใจ", "จัดการ", "สร้าง", "ทำลาย", "เปิด", "ปิด", "จับ", "ปล่อย", "ดึง",
        "ผลัก", "ขยับ", "หนี", "ขอบคุณ", "ขอโทษ", "สวัสดี", "ยินดี", "ช่วยเหลือ", "เชื่อ", "หวัง", "ต้องการ", "ห้าม", "ระวัง",
        "ตรวจ", "ตรวจสอบ", "ติดต่อ", "ตามหา", "ค้นหา",

        // Adverbs, Question Words, Quantifiers
        "มาก", "น้อย", "นิดหน่อย", "นิดเดียว", "มากมาย", "ทั้งหมด", "ทุก", "ทุกอย่าง", "บาง", "บางคน", "บางอย่าง", "หลาย",
        "หลายคน", "อีก", "อีกครั้ง", "ใหม่", "เก่า", "เร็ว", "ช้า", "ดี", "แย่", "สวย", "น่ารัก", "น่ากลัว", "แปลก", "สำคัญ",
        "จำเป็น", "จริง", "จริงๆ", "แน่นอน", "แน่ใจ", "ใช่ไหม", "หรือเปล่า", "ไหม", "อะไร", "ทำไม", "อย่างไร", "ยังไง",
        "ที่ไหน", "ไหน", "เมื่อไหร่", "เท่าไหร่", "กี่", "อย่างนี้", "อย่างนั้น", "เช่นนี้", "เช่นนั้น", "เท่านี้", "เท่านั้น",
        "แค่", "เพียง", "เพียงแต่", "ก็", "จึง", "เลย", "ต่าง", "กัน", "เอง", "ด้วยกัน", "แล้ว", "หรือยัง", "ยัง", "ยังคง",
        "เสมอ", "ตลอดเวลา", "บ่อย", "บ่อยๆ", "บางครั้ง", "ทันที", "ตอนนี้", "เดี๋ยวนี้", "เร็วๆนี้",

        // Particles & Politeness
        "ครับ", "ค่ะ", "นะ", "น่ะ", "จ้ะ", "จ้า", "ซิ", "สิ", "ละ", "ล่ะ", "หรอก", "เนอะ", "เถอะ", "เถิด", "วะ", "โว้ย",
        "ยะ", "เหรอ", "ปะ", "ป่าว", "มั้ง", "มะ", "ฮึ", "เอ๊ะ", "อ๊ะ", "โอ้", "ว้าว", "เออ", "ใช่แล้ว", "ถูกต้อง", "โอเค",

        // Story & Game Words
        "เรื่อง", "เรื่องราว", "ปัญหา", "ความคิด", "ความรู้สึก", "จิตใจ", "อารมณ์", "ความสุข", "ความเศร้า", "ความกลัว",
        "ความเครียด", "ความรัก", "ความหวัง", "ความจริง", "ความลับ", "ความตาย", "ชีวิต", "วิญญาณ", "ร่างกาย", "หัวใจ", "สมอง",
        "ตา", "หู", "ปาก", "มือ", "เท้า", "เลือด", "เสียง", "ภาพ", "เงา", "แสง", "มืด", "ความมืด", "ห้อง", "บ้าน", "โรงเรียน",
        "โรงพยาบาล", "เมือง", "ถนน", "ทาง", "ประตู", "หน้าต่าง", "บันได", "ลิฟต์", "กุญแจ", "หนังสือ", "จดหมาย", "ข้อความ",
        "คำ", "ประโยค", "ภาษา", "ชื่อ", "วัน", "วันนี้", "พรุ่งนี้", "เมื่อวาน", "คืน", "คืนนี้", "เช้า", "เที่ยง", "บ่าย",
        "เย็น", "นาที", "ชั่วโมง", "ปี", "เดือน", "โลก", "สถานที่", "ที่นี่", "ที่นั่น", "ที่โน่น", "ด้าน", "ด้านหน้า", "ด้านหลัง",
        "ซ้าย", "ขวา", "บน", "ล่าง", "ตรง", "เกม", "เควส", "คะแนน", "รางวัล", "ภารกิจ", "อันตราย", "ปลอดภัย", "แผนที่", "ชั้น"
    };

    /// <summary>
    /// Adjusts Thai text so vowels and tone marks never overlap, and inserts Zero-Width Spaces
    /// at word boundaries so long dialogue lines wrap naturally without waiting for sentence end.
    /// Safe to call multiple times (idempotent). Preserves existing rich text tags.
    /// </summary>
    public static string Adjust(string input, float toneOffset = DefaultToneUpperOffset, float tallShift = DefaultTallConsonantShift, float descenderOffset = DefaultDescenderOffset)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        // 1. Insert Zero-Width Spaces at word boundaries for natural line wrapping
        string segmented = InsertWordBreaks(input);

        // 2. Adjust overlapping vowels and tone marks
        return AdjustGlyphPositions(segmented, toneOffset, tallShift, descenderOffset);
    }

    /// <summary>
    /// Inserts Zero-Width Spaces (\u200B) at Thai word/syllable boundaries.
    /// Allows TextMeshPro to break long lines naturally across words instead of cramming or stacking.
    /// </summary>
    public static string InsertWordBreaks(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        // Avoid double-inserting if already has ZWSP
        if (text.IndexOf(ZeroWidthSpace) >= 0)
        {
            return text;
        }

        StringBuilder sb = new StringBuilder(text.Length + 64);
        int len = text.Length;
        int i = 0;

        while (i < len)
        {
            char c = text[i];

            // 1. Skip rich text tags <...>
            if (c == '<')
            {
                int closeTag = text.IndexOf('>', i);
                if (closeTag > i)
                {
                    sb.Append(text.Substring(i, closeTag - i + 1));
                    i = closeTag + 1;
                    continue;
                }
            }

            // 2. Non-Thai characters (Whitespace, Latin, Numbers, Punctuation)
            if (!IsThaiCharacter(c))
            {
                sb.Append(c);
                i++;
                continue;
            }

            // 3. Thai character: try dictionary longest-match
            bool matched = false;
            int maxCheck = Mathf.Min(12, len - i);

            for (int wlen = maxCheck; wlen >= 2; wlen--)
            {
                string sub = text.Substring(i, wlen);
                if (ThaiDictionary.Contains(sub))
                {
                    if (ShouldInsertBreakBefore(sb))
                    {
                        sb.Append(ZeroWidthSpace);
                    }

                    sb.Append(sub);
                    i += wlen;
                    matched = true;
                    break;
                }
            }

            if (matched)
            {
                continue;
            }

            // 4. Syllable heuristic fallback for unknown words
            if (IsFrontVowel(c) && ShouldInsertBreakBefore(sb))
            {
                sb.Append(ZeroWidthSpace);
            }
            else if (c == '\u0E46' || c == '\u0E2F') // ๆ or ฯ
            {
                sb.Append(c);
                if (i + 1 < len && IsThaiCharacter(text[i + 1]))
                {
                    sb.Append(ZeroWidthSpace);
                }
                i++;
                continue;
            }

            // Consume one full Thai cluster (Consonant + upper/lower vowels + tone marks)
            int clusterEnd = GetClusterEnd(text, i);
            sb.Append(text.Substring(i, clusterEnd - i));
            i = clusterEnd;
        }

        return sb.ToString();
    }

    private static bool ShouldInsertBreakBefore(StringBuilder sb)
    {
        if (sb.Length == 0) return false;
        char last = sb[sb.Length - 1];
        if (last == ZeroWidthSpace || char.IsWhiteSpace(last) || last == '>' || last == '\n' || last == '\r')
        {
            return false;
        }
        return IsThaiCharacter(last);
    }

    private static int GetClusterEnd(string text, int start)
    {
        int len = text.Length;
        int i = start + 1;

        // Consume any following combining vowels/tone marks
        while (i < len)
        {
            char c = text[i];
            if (IsUpperVowel(c) || IsToneMark(c) || IsLowerVowel(c) || IsFollowVowel(c))
            {
                i++;
            }
            else
            {
                break;
            }
        }

        return i;
    }

    /// <summary>
    /// Adjusts Thai text so vowels and tone marks never overlap in TextMeshPro.
    /// Safe to call multiple times (idempotent). Preserves existing rich text tags.
    /// </summary>
    private static string AdjustGlyphPositions(string input, float toneOffset, float tallShift, float descenderOffset)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        // Avoid double-adjusting if already processed
        if (input.Contains("<voffset="))
        {
            return input;
        }

        // Normalize inverted typing order: Consonant + Tone + UpperVowel -> Consonant + UpperVowel + Tone
        string normalized = NormalizeToneOrder(input);

        StringBuilder sb = new StringBuilder(normalized.Length + 64);
        int len = normalized.Length;
        int i = 0;

        char lastConsonant = '\0';

        string voffsetToneOpen = $"<voffset={toneOffset:F2}em>";
        const string voffsetClose = "</voffset>";
        string descenderOpen = $"<voffset={descenderOffset:F2}em>";
        string tallShiftOpen = $"<space={tallShift:F2}em>";
        string tallShiftClose = $"<space={-tallShift:F2}em>";

        while (i < len)
        {
            char c = normalized[i];

            // Skip existing rich text tags
            if (c == '<')
            {
                int closeTag = normalized.IndexOf('>', i);
                if (closeTag > i)
                {
                    sb.Append(normalized.Substring(i, closeTag - i + 1));
                    i = closeTag + 1;
                    continue;
                }
            }

            // Track last Thai consonant
            if (c >= '\u0E01' && c <= '\u0E2E')
            {
                lastConsonant = c;
                sb.Append(c);
                i++;
                continue;
            }

            // Reset consonant on whitespace, ZWSP, or punctuation
            if (char.IsWhiteSpace(c) || char.IsPunctuation(c) || c == ZeroWidthSpace)
            {
                lastConsonant = '\0';
                sb.Append(c);
                i++;
                continue;
            }

            // Case 1: Upper Vowel followed by Tone Mark (เช่น ที่, นี้, ขึ้น, ดื่ม, ศักดิ์)
            if (IsUpperVowel(c))
            {
                char upperVowel = c;
                char nextChar = (i + 1 < len) ? normalized[i + 1] : '\0';

                if (IsToneMark(nextChar))
                {
                    char tone = nextChar;

                    // If preceded by tall consonant (ป, ฝ, ฟ, ฬ), shift slightly left so doesn't hit stalk
                    if (IsTallConsonant(lastConsonant))
                    {
                        sb.Append(tallShiftOpen);
                        sb.Append(upperVowel);
                        sb.Append(voffsetToneOpen);
                        sb.Append(tone);
                        sb.Append(voffsetClose);
                        sb.Append(tallShiftClose);
                    }
                    else
                    {
                        sb.Append(upperVowel);
                        sb.Append(voffsetToneOpen);
                        sb.Append(tone);
                        sb.Append(voffsetClose);
                    }

                    i += 2; // consumed both upper vowel and tone mark
                    continue;
                }

                // Upper vowel alone on tall consonant
                if (IsTallConsonant(lastConsonant))
                {
                    sb.Append(tallShiftOpen);
                    sb.Append(upperVowel);
                    sb.Append(tallShiftClose);
                    i++;
                    continue;
                }

                sb.Append(upperVowel);
                i++;
                continue;
            }

            // Case 2: Tone Mark directly on tall consonant (เช่น ป่า, ฟ้า, ฝั่ง)
            if (IsToneMark(c) && IsTallConsonant(lastConsonant))
            {
                sb.Append(tallShiftOpen);
                sb.Append(c);
                sb.Append(tallShiftClose);
                i++;
                continue;
            }

            // Case 3: Lower vowel under descender consonant (เช่น ญี่ปุ่น, ฐาน)
            if (IsLowerVowel(c) && IsDescenderConsonant(lastConsonant))
            {
                sb.Append(descenderOpen);
                sb.Append(c);
                sb.Append(voffsetClose);
                i++;
                continue;
            }

            // Default character
            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Normalizes typing order: if a user typed [Tone Mark] then [Upper Vowel], swap them to [Upper Vowel] then [Tone Mark].
    /// </summary>
    private static string NormalizeToneOrder(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        StringBuilder sb = new StringBuilder(text.Length);
        int len = text.Length;

        for (int i = 0; i < len; i++)
        {
            if (i + 1 < len && IsToneMark(text[i]) && IsUpperVowel(text[i + 1]))
            {
                sb.Append(text[i + 1]);
                sb.Append(text[i]);
                i++;
            }
            else
            {
                sb.Append(text[i]);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Strips generated Thai adjustment tags and ZWSP from text if clean raw text is needed.
    /// </summary>
    public static string StripAdjustments(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        string pattern = @"<voffset=[^>]+>|</voffset>|<space=[^>]+>";
        string stripped = Regex.Replace(text, pattern, string.Empty);
        return stripped.Replace(ZeroWidthSpace.ToString(), string.Empty);
    }
}
