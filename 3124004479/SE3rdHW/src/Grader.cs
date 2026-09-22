using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ArithGenerator
{
    /// <summary>判题结果：对／错的题目编号，以及过程中的告警信息。</summary>
    public sealed class GradeResult
    {
        private readonly List<int> _correctNumbers = new List<int>();
        private readonly List<int> _wrongNumbers = new List<int>();
        private readonly List<string> _warnings = new List<string>();

        /// <summary>做对的题目编号（从 1 开始），按题号升序。</summary>
        public List<int> CorrectNumbers { get { return _correctNumbers; } }

        /// <summary>做错的题目编号（从 1 开始），按题号升序。</summary>
        public List<int> WrongNumbers { get { return _wrongNumbers; } }

        /// <summary>无法解析的行等异常情况提示。</summary>
        public List<string> Warnings { get { return _warnings; } }

        /// <summary>做对的题目数。</summary>
        public int CorrectCount { get { return _correctNumbers.Count; } }

        /// <summary>做错的题目数。</summary>
        public int WrongCount { get { return _wrongNumbers.Count; } }

        /// <summary>参与判定的题目总数。</summary>
        public int Total { get { return _correctNumbers.Count + _wrongNumbers.Count; } }
    }

    /// <summary>
    /// 判题器：对照题目文件与答案文件逐行判定对错。
    /// 做法是先解析题目里的表达式自行算出正确答案，再和答案文件中的值比较，
    /// 因此答案写成 6/4 还是 3/2 都会被判为正确（约分后相等即算对）。
    /// </summary>
    public static class Grader
    {
        /// <summary>判题结果的输出文件名，写在执行程序的当前目录。</summary>
        public const string ReportFileName = "Grade.txt";

        /// <summary>判定题目文件与答案文件，返回统计结果。</summary>
        public static GradeResult Grade(string exercisePath, string answerPath)
        {
            string[] exerciseLines = ReadLines(exercisePath);
            string[] answerLines = ReadLines(answerPath);
            GradeResult result = new GradeResult();

            if (exerciseLines.Length != answerLines.Length)
            {
                result.Warnings.Add(string.Format(
                    "题目文件有 {0} 行，答案文件有 {1} 行，行数不一致，只比较前 {2} 行。",
                    exerciseLines.Length, answerLines.Length, Math.Min(exerciseLines.Length, answerLines.Length)));
            }

            int total = Math.Min(exerciseLines.Length, answerLines.Length);
            for (int i = 0; i < total; i++)
            {
                int number = i + 1;      // 题号从 1 开始
                bool matched;

                Fraction correctAnswer;
                Fraction studentAnswer;

                if (!TrySolve(exerciseLines[i], out correctAnswer))
                {
                    result.Warnings.Add(string.Format("第 {0} 行的题目无法解析：{1}", number, exerciseLines[i]));
                    matched = false;
                }
                else if (!Fraction.TryParse(answerLines[i], out studentAnswer))
                {
                    result.Warnings.Add(string.Format("第 {0} 行的答案无法解析：{1}", number, answerLines[i]));
                    matched = false;
                }
                else
                {
                    matched = correctAnswer == studentAnswer;
                }

                if (matched)
                {
                    result.CorrectNumbers.Add(number);
                }
                else
                {
                    result.WrongNumbers.Add(number);
                }
            }
            return result;
        }

        /// <summary>
        /// 由题目文本算出正确答案。题目形如 "1 + 2 = "，先去掉等号及其后面的内容，再解析表达式。
        /// </summary>
        public static bool TrySolve(string exerciseLine, out Fraction answer)
        {
            answer = null;
            if (exerciseLine == null)
            {
                return false;
            }

            int equalsIndex = exerciseLine.LastIndexOf('=');
            string expressionText = equalsIndex >= 0 ? exerciseLine.Substring(0, equalsIndex) : exerciseLine;

            Expression expression;
            if (!ExpressionParser.TryParse(expressionText, out expression))
            {
                return false;
            }
            answer = expression.Value;
            return true;
        }

        /// <summary>把判题结果写入文件（UTF-8 带 BOM）。</summary>
        public static void WriteReport(string path, GradeResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException("result");
            }
            File.WriteAllText(path, FormatReport(result), new UTF8Encoding(true));
        }

        /// <summary>
        /// 生成 Grade.txt 的内容，格式为：
        /// Correct: 5 (1, 3, 5, 7, 9)
        /// Wrong: 5 (2, 4, 6, 8, 10)
        /// </summary>
        public static string FormatReport(GradeResult result)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("Correct: ").Append(result.CorrectCount)
                   .Append(" (").Append(JoinNumbers(result.CorrectNumbers)).AppendLine(")");
            builder.Append("Wrong: ").Append(result.WrongCount)
                   .Append(" (").Append(JoinNumbers(result.WrongNumbers)).AppendLine(")");
            return builder.ToString();
        }

        private static string JoinNumbers(List<int> numbers)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < numbers.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }
                builder.Append(numbers[i]);
            }
            return builder.ToString();
        }

        /// <summary>
        /// 读入文本文件的所有非空行。
        /// 先按 UTF-8 严格解码，若不是合法 UTF-8 再退回 GBK（记事本另存为 ANSI 时的编码），
        /// 这样别人手工编辑过的题目文件也能正常判题。
        /// </summary>
        private static string[] ReadLines(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("找不到文件：" + path, path);
            }

            byte[] bytes = File.ReadAllBytes(path);
            string text;
            try
            {
                UTF8Encoding strictUtf8 = new UTF8Encoding(false, true);
                text = strictUtf8.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                Encoding gbk = TryGetGbkEncoding();
                text = gbk != null ? gbk.GetString(bytes) : Encoding.UTF8.GetString(bytes);
            }

            text = RemoveBom(text);   // 去掉 UTF-8 BOM 解出来的零宽字符
            return SplitLines(text);
        }

        /// <summary>去掉文本开头的 UTF-8 BOM 字符（U+FEFF）。</summary>
        private static string RemoveBom(string text)
        {
            const char bom = (char)0xFEFF;
            if (text.Length > 0 && text[0] == bom)
            {
                return text.Substring(1);
            }
            return text;
        }

        private static Encoding TryGetGbkEncoding()
        {
            try
            {
                return Encoding.GetEncoding(936);
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
        }

        /// <summary>按 CRLF / LF / CR 切分成行，并忽略空行。</summary>
        private static string[] SplitLines(string text)
        {
            List<string> lines = new List<string>();
            string[] rawLines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < rawLines.Length; i++)
            {
                string line = rawLines[i].Trim();
                if (line.Length > 0)
                {
                    lines.Add(line);
                }
            }
            return lines.ToArray();
        }
    }
}
