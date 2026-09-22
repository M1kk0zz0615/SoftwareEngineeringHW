using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ArithGenerator
{
    /// <summary>
    /// 自检：把关键算法与整体流程完整跑一遍，命令行用 `Myapp --selftest` 触发。
    /// 这里的每条断言都对应一个可以复现的测试用例，既是回归测试，也是作业"测试运行"一节的素材。
    /// </summary>
    public static class SelfTest
    {
        private static int _passed;
        private static int _failed;
        private static readonly List<string> _failures = new List<string>();

        public static int Run()
        {
            _passed = 0;
            _failed = 0;
            _failures.Clear();

            Console.WriteLine("=== Myapp 自检开始 ===");
            Console.WriteLine();

            FractionTests();
            ParseTests();
            CalculatorTests();
            RenderTests();
            CanonicalTests();
            GeneratorConstraintTests();
            UniquenessTests();
            ScaleTests();
            SmallRangeTests();
            GradingTests();

            Console.WriteLine();
            Console.WriteLine("=== 自检结束：通过 {0} 项，失败 {1} 项 ===", _passed, _failed);
            if (_failed > 0)
            {
                Console.WriteLine("失败清单：");
                for (int i = 0; i < _failures.Count; i++)
                {
                    Console.WriteLine("  - " + _failures[i]);
                }
            }
            return _failed == 0 ? 0 : 1;
        }

        // ==================== 一、分数运算 ====================

        private static void FractionTests()
        {
            Section("一、分数类 Fraction");

            CheckEqual("约分：6/8 → 3/4", "3/4", Fraction.Parse("6/8").ToString());
            CheckEqual("加法：1/6 + 1/8 = 7/24（作业原例）", "7/24",
                       (Fraction.Parse("1/6") + Fraction.Parse("1/8")).ToString());
            CheckEqual("减法：1/2 − 1/3 = 1/6", "1/6",
                       (Fraction.Parse("1/2") - Fraction.Parse("1/3")).ToString());
            CheckEqual("乘法：1/2 × 2/3 = 1/3", "1/3",
                       (Fraction.Parse("1/2") * Fraction.Parse("2/3")).ToString());
            CheckEqual("除法：1/2 ÷ 1/3 = 1'1/2（假分数输出成带分数）", "1'1/2",
                       (Fraction.Parse("1/2") / Fraction.Parse("1/3")).ToString());
            CheckEqual("带分数输出：19/8 → 2'3/8", "2'3/8", Fraction.Parse("19/8").ToString());
            Check("比较大小：1/3 < 1/2", Fraction.Parse("1/3") < Fraction.Parse("1/2"));
            Check("真分数判定：1/2 是真分数，3/2 不是",
                  Fraction.Parse("1/2").IsProperFraction && !Fraction.Parse("3/2").IsProperFraction);

            bool thrown = false;
            try
            {
                new Fraction(1, 0);
            }
            catch (ArgumentException)
            {
                thrown = true;
            }
            Check("分母为 0 时抛出 ArgumentException", thrown);
        }

        // ==================== 二、分数文本解析 ====================

        private static void ParseTests()
        {
            Section("二、分数文本解析 Fraction.TryParse");

            // 注意：19/8 的显示形式是带分数 2'3/8，比较时用数值本身
            Check("带分数：2'3/8 解析为 19/8", Fraction.Parse("2'3/8") == Fraction.Parse("19/8"));
            Check("中文撇号写法 2’3/8 也认", Fraction.Parse("2’3/8") == Fraction.Parse("19/8"));
            CheckEqual("带分数的显示形式：19/8 显示成 2'3/8", "2'3/8", Fraction.Parse("19/8").ToString());
            CheckEqual("整数：12", "12", Fraction.Parse("12").ToString());
            CheckEqual("零：0", "0", Fraction.Parse("0").ToString());

            Fraction ignored;
            Check("非法文本 abc 解析失败", !Fraction.TryParse("abc", out ignored));
            Check("空串解析失败", !Fraction.TryParse("", out ignored));
            Check("分母为 0 的 1/0 解析失败", !Fraction.TryParse("1/0", out ignored));
        }

        // ==================== 三、表达式求值 ====================

        private static void CalculatorTests()
        {
            Section("三、表达式求值 ExpressionParser + Expression");

            CheckEqual("1/6 + 1/8 = 7/24", "7/24", Solve("1/6 + 1/8"));
            CheckEqual("2'3/8 × 2 = 4'3/4", "4'3/4", Solve("2'3/8 × 2"));
            CheckEqual("5 − 1/2 = 4'1/2", "4'1/2", Solve("5 − 1/2"));
            CheckEqual("1 ÷ 3 × 3 = 1（左结合）", "1", Solve("1 ÷ 3 × 3"));
            CheckEqual("(1 + 2) × 3 = 9（括号优先）", "9", Solve("(1 + 2) × 3"));
            CheckEqual("1 ÷ 4 = 1/4", "1/4", Solve("1 ÷ 4"));
            CheckEqual("1 − 1/2 = 1/2", "1/2", Solve("1 − 1/2"));
            CheckEqual("ASCII 写法 1/2 * 4 也能算 = 2", "2", Solve("1/2 * 4"));

            Expression ignored;
            Check("残缺表达式 1 + 解析失败", !ExpressionParser.TryParse("1 + ", out ignored));
            Check("括号不匹配 (1 + 2 解析失败", !ExpressionParser.TryParse("(1 + 2", out ignored));
        }

        // ==================== 四、括号渲染 ====================

        private static void RenderTests()
        {
            Section("四、表达式的括号渲染（只加必须的括号）");

            CheckEqual("1 + 2 + 3", "1 + 2 + 3", ExpressionParser.Parse("1 + 2 + 3").ToInfix());
            CheckEqual("(1 + 2) × 3", "(1 + 2) × 3", ExpressionParser.Parse("(1 + 2) × 3").ToInfix());
            CheckEqual("1 × (2 + 3)", "1 × (2 + 3)", ExpressionParser.Parse("1 × (2 + 3)").ToInfix());
            CheckEqual("1 ÷ (2 ÷ 3) 右侧必须保留括号", "1 ÷ (2 ÷ 3)",
                       ExpressionParser.Parse("1 ÷ (2 ÷ 3)").ToInfix());
            CheckEqual("1 ÷ 2 ÷ 3 左侧不必加括号", "1 ÷ 2 ÷ 3",
                       ExpressionParser.Parse("1 ÷ 2 ÷ 3").ToInfix());
            CheckEqual("1 − (2 − 3) 右侧必须保留括号", "1 − (2 − 3)",
                       ExpressionParser.Parse("1 − (2 − 3)").ToInfix());
            CheckEqual("1 − 2 − 3 左侧不必加括号", "1 − 2 − 3",
                       ExpressionParser.Parse("1 − 2 − 3").ToInfix());
        }

        // ==================== 五、查重用的规范形式 ====================

        private static void CanonicalTests()
        {
            Section("五、查重（需求 7）");

            CheckEqual("23 + 45 与 45 + 23 视为同一道题",
                       ExpressionParser.Parse("23 + 45").ToCanonical(),
                       ExpressionParser.Parse("45 + 23").ToCanonical());
            CheckEqual("6 × 8 与 8 × 6 视为同一道题",
                       ExpressionParser.Parse("6 × 8").ToCanonical(),
                       ExpressionParser.Parse("8 × 6").ToCanonical());
            CheckEqual("3 + (2 + 1) 与 1 + 2 + 3 视为同一道题",
                       ExpressionParser.Parse("3 + (2 + 1)").ToCanonical(),
                       ExpressionParser.Parse("1 + 2 + 3").ToCanonical());
            CheckEqual("4 + 2 × 3 与 2 × 3 + 4 视为同一道题",
                       ExpressionParser.Parse("4 + 2 × 3").ToCanonical(),
                       ExpressionParser.Parse("2 × 3 + 4").ToCanonical());
            Check("1 + 2 + 3 与 3 + 2 + 1 不是同一道题",
                  ExpressionParser.Parse("1 + 2 + 3").ToCanonical()
                  != ExpressionParser.Parse("3 + 2 + 1").ToCanonical());
            Check("1 − 2 与 2 − 1 不是同一道题",
                  ExpressionParser.Parse("1 − 2").ToCanonical()
                  != ExpressionParser.Parse("2 − 1").ToCanonical());
            Check("1 ÷ 2 与 2 ÷ 1 不是同一道题",
                  ExpressionParser.Parse("1 ÷ 2").ToCanonical()
                  != ExpressionParser.Parse("2 ÷ 1").ToCanonical());
        }

        // ==================== 六、生成题目的约束 ====================

        private static void GeneratorConstraintTests()
        {
            const int range = 10;
            const int count = 300;
            Section(string.Format("六、生成题目的约束（-r {0}，{1} 道）", range, count));

            ProblemGenerator generator = new ProblemGenerator(range, 20240101);
            List<Problem> problems = generator.Generate(count);
            CheckEqual("题目数量正确", count.ToString(), problems.Count.ToString());

            Fraction limit = new Fraction(range);
            List<string> errors = new List<string>();
            int zeroCount = 0;
            int fractionCount = 0;

            for (int i = 0; i < problems.Count; i++)
            {
                Problem problem = problems[i];
                if (problem.Expr.OperatorCount() > ProblemGenerator.MaxOperatorCount)
                {
                    errors.Add("运算符超过 3 个：" + problem.Text);
                }
                if (!problem.Text.EndsWith(" = "))
                {
                    errors.Add("题面没有以 \" = \" 结尾：" + problem.Text);
                }
                Fraction answer;
                if (!Grader.TrySolve(problem.Text, out answer) || answer != problem.Answer)
                {
                    errors.Add("题面与答案对不上：" + problem.Text);
                }
                if (problem.Answer.IsZero)
                {
                    zeroCount++;
                }
                CheckExpression(problem.Expr, limit, errors);

                List<Fraction> leaves = new List<Fraction>();
                problem.Expr.CollectLeaves(leaves);
                for (int k = 0; k < leaves.Count; k++)
                {
                    if (!leaves[k].IsInteger)
                    {
                        fractionCount++;
                        break;
                    }
                }
            }

            Check("所有题目都满足：不出现负数、除法结果是真分数、数值都在范围内（" + errors.Count + " 处异常）",
                  errors.Count == 0);
            if (errors.Count > 0)
            {
                for (int i = 0; i < Math.Min(5, errors.Count); i++)
                {
                    Console.WriteLine("        异常样例：" + errors[i]);
                }
            }
            Console.WriteLine("        统计：含分数的题目 {0} 道，答案为 0 的题目 {1} 道", fractionCount, zeroCount);
            Check("题目中确实出现了分数（说明分数分支被覆盖到）", fractionCount > 0);
        }

        /// <summary>逐节点检查题目约束：减法不为负、除法的被除数介于 0 与除数之间（结果为真分数）、数值不超范围。</summary>
        private static void CheckExpression(Expression expression, Fraction limit, List<string> errors)
        {
            if (expression.IsLeaf)
            {
                if (expression.Value.Numerator.Sign < 0)
                {
                    errors.Add("出现负数数值：" + expression.Value);
                }
                if (expression.Value >= limit)
                {
                    errors.Add("数值超出 -r 范围：" + expression.Value);
                }
                return;
            }

            if (expression.Kind == NodeKind.Sub && expression.Left.Value < expression.Right.Value)
            {
                errors.Add("减法会产生负数：" + expression.ToInfix());
            }
            if (expression.Kind == NodeKind.Div)
            {
                // 需求 5：e1 ÷ e2 的结果必须是真分数，等价于 0 < e1 < e2
                Fraction dividend = expression.Left.Value;
                Fraction divisor = expression.Right.Value;
                if (!divisor.IsPositive)
                {
                    errors.Add("除数为 0：" + expression.ToInfix());
                }
                else if (!dividend.IsPositive || dividend >= divisor)
                {
                    errors.Add("除法的结果不是真分数：" + expression.ToInfix());
                }
            }

            CheckExpression(expression.Left, limit, errors);
            CheckExpression(expression.Right, limit, errors);
        }

        // ==================== 七、不重复 ====================

        private static void UniquenessTests()
        {
            const int count = 500;
            Section(string.Format("七、一次运行内不产生重复题目（{0} 道）", count));

            ProblemGenerator generator = new ProblemGenerator(10, 777);
            List<Problem> problems = generator.Generate(count);

            HashSet<string> canonicalForms = new HashSet<string>();
            int duplicates = 0;
            for (int i = 0; i < problems.Count; i++)
            {
                if (!canonicalForms.Add(problems[i].Canonical))
                {
                    duplicates++;
                }
            }

            CheckEqual("题目数 = " + count, count.ToString(), problems.Count.ToString());
            CheckEqual("规范形式去重后仍是 " + count + " 个（重复 0 道）", "0", duplicates.ToString());

            // 再换一个种子，验证去重不是碰巧
            ProblemGenerator another = new ProblemGenerator(15, 888);
            List<Problem> anotherProblems = another.Generate(count);
            HashSet<string> anotherForms = new HashSet<string>();
            int anotherDuplicates = 0;
            for (int i = 0; i < anotherProblems.Count; i++)
            {
                if (!anotherForms.Add(anotherProblems[i].Canonical))
                {
                    anotherDuplicates++;
                }
            }
            CheckEqual("换一组随机种子（-r 15）后同样无重复", "0", anotherDuplicates.ToString());
        }

        // ==================== 八、规模：一万道题 ====================

        private static void ScaleTests()
        {
            const int count = 10000;
            Section(string.Format("八、规模测试：一次生成 {0} 道题并判题", count));

            string directory = CreateTempDirectory();
            try
            {
                string exercisePath = Path.Combine(directory, "Exercises.txt");
                string answerPath = Path.Combine(directory, "Answers.txt");

                Stopwatch stopwatch = Stopwatch.StartNew();
                ProblemGenerator generator = new ProblemGenerator(10, 10001);
                List<Problem> problems = generator.Generate(count);
                ProblemGenerator.WriteFiles(exercisePath, answerPath, problems);
                stopwatch.Stop();

                Console.WriteLine("        生成 + 写文件耗时 {0} 毫秒", stopwatch.ElapsedMilliseconds);
                CheckEqual(string.Format("成功生成 {0} 道题", count), count.ToString(), problems.Count.ToString());

                string[] exerciseLines = File.ReadAllLines(exercisePath);
                string[] answerLines = File.ReadAllLines(answerPath);
                CheckEqual("Exercises.txt 行数正确", count.ToString(), exerciseLines.Length.ToString());
                CheckEqual("Answers.txt 行数正确", count.ToString(), answerLines.Length.ToString());

                // 逐题核对：把题面重新解析一遍，结果必须与答案文件里的值一致
                int mismatch = 0;
                for (int i = 0; i < problems.Count; i++)
                {
                    Fraction reparsed;
                    bool parsed = Grader.TrySolve(problems[i].Text, out reparsed);
                    if (!parsed || reparsed != problems[i].Answer)
                    {
                        mismatch++;
                        if (mismatch <= 5)
                        {
                            Console.WriteLine("        不一致：题面 [{0}] 答案 [{1}] 重新解析 [{2}]",
                                              problems[i].Text,
                                              problems[i].AnswerText,
                                              parsed ? reparsed.ToString() : "解析失败");
                        }
                    }
                }
                CheckEqual("10000 道题的题面重新解析后与答案一致（不一致 0 处）",
                           "0", mismatch.ToString());

                // 把生成出来的答案当成"学生答案"回判，必须全对；这同时验证了生成与判题两条链路
                Stopwatch gradeWatch = Stopwatch.StartNew();
                GradeResult result = Grader.Grade(exercisePath, answerPath);
                gradeWatch.Stop();

                Console.WriteLine("        判题耗时 {0} 毫秒", gradeWatch.ElapsedMilliseconds);
                CheckEqual(string.Format("判题：{0} 道全对", count), count.ToString(), result.CorrectCount.ToString());
                CheckEqual("判题：错题数为 0", "0", result.WrongCount.ToString());
                CheckEqual("判题：没有告警", "0", result.Warnings.Count.ToString());
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        // ==================== 九、极端数值范围 ====================

        private static void SmallRangeTests()
        {
            Section("九、极端数值范围（-r 1 / -r 2）");

            bool thrown = false;
            List<Problem> problems = null;
            try
            {
                ProblemGenerator generator = new ProblemGenerator(1, 5);
                problems = generator.Generate(100);
            }
            catch (Exception)
            {
                thrown = true;
            }
            Check("-r 1 不抛异常", !thrown);
            Check("-r 1 至少能生成 1 道题（数值只能是 0，题目极其有限）",
                  problems != null && problems.Count >= 1);

            bool thrown2 = false;
            List<Problem> problems2 = null;
            try
            {
                ProblemGenerator generator = new ProblemGenerator(2, 5);
                problems2 = generator.Generate(200);
            }
            catch (Exception)
            {
                thrown2 = true;
            }
            Check("-r 2 不抛异常", !thrown2);
            Check("-r 2 生成的题目仍然满足全部约束",
                  problems2 != null && AllConstraintsHold(problems2, 2));
        }

        private static bool AllConstraintsHold(List<Problem> problems, int range)
        {
            Fraction limit = new Fraction(range);
            List<string> errors = new List<string>();
            for (int i = 0; i < problems.Count; i++)
            {
                CheckExpression(problems[i].Expr, limit, errors);
            }
            return errors.Count == 0;
        }

        // ==================== 十、判题 ====================

        private static void GradingTests()
        {
            Section("十、判题（-e 题目文件 -a 答案文件）");

            string directory = CreateTempDirectory();
            try
            {
                // 人造 10 道题：第 2、4、6、8、10 题的答案故意写错，
                // 期望输出与作业示例完全一致：Correct: 5 (1, 3, 5, 7, 9) / Wrong: 5 (2, 4, 6, 8, 10)
                string[] exercises = new string[]
                {
                    "1 + 2 = ",
                    "5 − 3 = ",
                    "1/6 + 1/8 = ",
                    "2 × 3 = ",
                    "2'3/8 × 2 = ",
                    "9 ÷ 3 = ",
                    "(1 + 2) × 3 = ",
                    "1 ÷ 4 = ",
                    "1 − 1/2 = ",
                    "7 × 0 = "
                };
                string[] answers = new string[]
                {
                    "3", "5", "7/24", "5", "4'3/4", "4", "9", "1/5", "1/2", "7"
                };

                string exercisePath = Path.Combine(directory, "Exercises.txt");
                string answerPath = Path.Combine(directory, "Answers.txt");
                WriteAllLines(exercisePath, exercises);
                WriteAllLines(answerPath, answers);

                GradeResult result = Grader.Grade(exercisePath, answerPath);

                CheckEqual("对题数 = 5", "5", result.CorrectCount.ToString());
                CheckEqual("错题数 = 5", "5", result.WrongCount.ToString());
                CheckEqual("对题编号 = 1, 3, 5, 7, 9", "1,3,5,7,9", JoinForTest(result.CorrectNumbers));
                CheckEqual("错题编号 = 2, 4, 6, 8, 10", "2,4,6,8,10", JoinForTest(result.WrongNumbers));

                string expectedReport = "Correct: 5 (1, 3, 5, 7, 9)" + Environment.NewLine
                                        + "Wrong: 5 (2, 4, 6, 8, 10)" + Environment.NewLine;
                CheckEqual("Grade.txt 内容与作业示例一致", expectedReport, Grader.FormatReport(result));

                // 约分后相等也判对：正确答案 1/2，写成 3/6 应当算对
                string[] equivalent = new string[] { "1 ÷ 2 = ", "2'3/8 × 2 = " };
                string[] equivalentAnswers = new string[] { "3/6", "19/4" };
                WriteAllLines(exercisePath, equivalent);
                WriteAllLines(answerPath, equivalentAnswers);
                GradeResult equivalentResult = Grader.Grade(exercisePath, answerPath);
                CheckEqual("答案写成 3/6、19/4（未约分／未化成带分数）同样判对",
                           "2", equivalentResult.CorrectCount.ToString());

                // 行数不一致时给出告警，且只比较前面的行
                WriteAllLines(answerPath, new string[] { "3" });
                GradeResult shortResult = Grader.Grade(exercisePath, answerPath);
                CheckEqual("行数不一致时只比较前 1 行", "1", shortResult.Total.ToString());
                Check("行数不一致时给出提示", shortResult.Warnings.Count == 1);
            }
            finally
            {
                DeleteDirectory(directory);
            }
        }

        // ==================== 工具方法 ====================

        private static string Solve(string expressionText)
        {
            return ExpressionParser.Parse(expressionText).Value.ToString();
        }

        private static void Section(string title)
        {
            Console.WriteLine("--- " + title + " ---");
        }

        private static void Check(string name, bool condition)
        {
            if (condition)
            {
                _passed++;
                Console.WriteLine("[通过] " + name);
            }
            else
            {
                _failed++;
                _failures.Add(name);
                Console.WriteLine("[失败] " + name);
            }
        }

        private static void CheckEqual(string name, string expected, string actual)
        {
            if (expected == actual)
            {
                _passed++;
                Console.WriteLine("[通过] " + name);
            }
            else
            {
                _failed++;
                _failures.Add(name);
                Console.WriteLine(string.Format("[失败] {0}：期望 {1}，实际 {2}", name, expected, actual));
            }
        }

        private static string JoinForTest(List<int> numbers)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < numbers.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }
                builder.Append(numbers[i]);
            }
            return builder.ToString();
        }

        private static void WriteAllLines(string path, string[] lines)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                builder.AppendLine(lines[i]);
            }
            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(true));
        }

        private static string CreateTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "MyappSelfTest");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }

        private static void DeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (IOException)
            {
                // 临时文件被占用时无需影响自检结果
            }
        }
    }
}
