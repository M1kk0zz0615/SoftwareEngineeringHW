using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ArithGenerator
{
    /// <summary>
    /// 程序入口。
    /// 用法一：Myapp -n 10 -r 10      生成 10 道数值范围 10 以内的题目
    /// 用法二：Myapp -e Exercises.txt -a Answers.txt   判定答案对错
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            // 设置控制台输出为 UTF-8，保证 × ÷ 等符号正常显示；输出被重定向时可能失败，忽略即可
            try
            {
                Console.OutputEncoding = Encoding.UTF8;
            }
            catch (IOException)
            {
            }
            catch (System.Security.SecurityException)
            {
            }

            CliOptions options = CliOptions.Parse(args);

            if (options.Mode == RunMode.SelfTest)
            {
                return SelfTest.Run();
            }
            if (options.ShowHelp)
            {
                PrintHelp(Console.Out);
                return 0;
            }
            if (options.ErrorMessage != null)
            {
                // 需求 3：参数不合法时给出错误信息与帮助信息
                Console.Error.WriteLine("错误：" + options.ErrorMessage);
                Console.Error.WriteLine();
                PrintHelp(Console.Error);
                return 1;
            }

            try
            {
                if (options.Mode == RunMode.Grade)
                {
                    return RunGrade(options);
                }
                return RunGenerate(options);
            }
            catch (Exception ex)
            {
                // 兜底：任何未预料的异常都以友好信息结束，不把调用栈甩给用户
                Console.Error.WriteLine("错误：" + ex.Message);
                return 1;
            }
        }

        /// <summary>生成模式：造题、算答案、写文件。</summary>
        private static int RunGenerate(CliOptions options)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            ProblemGenerator generator = new ProblemGenerator(options.Range);
            List<Problem> problems = generator.Generate(options.Count);

            string exercisePath = Path.Combine(Environment.CurrentDirectory, ProblemGenerator.ExerciseFileName);
            string answerPath = Path.Combine(Environment.CurrentDirectory, ProblemGenerator.AnswerFileName);
            ProblemGenerator.WriteFiles(exercisePath, answerPath, problems);

            stopwatch.Stop();

            Console.WriteLine("数值范围：-r {0}（题目中出现的数值都小于 {0}）", options.Range);
            Console.WriteLine("已生成 {0} 道不重复的题目，用时 {1} 毫秒。", problems.Count, stopwatch.ElapsedMilliseconds);
            Console.WriteLine("题目文件：{0}", exercisePath);
            Console.WriteLine("答案文件：{0}", answerPath);

            if (generator.Exhausted)
            {
                // 例如 -r 1 时全部数值只能是 0，能出的题目非常有限，这里给出明确提示而不是默默少写
                Console.Error.WriteLine();
                Console.Error.WriteLine("提示：数值范围 -r {0} 内可出的不重复题目已经用尽，"
                                        + "实际只生成了 {1} 道（要求 {2} 道）。请把 -r 调大一些再试。",
                                        options.Range, problems.Count, options.Count);
            }
            return 0;
        }

        /// <summary>判题模式：对照题目文件与答案文件，把统计结果写入 Grade.txt。</summary>
        private static int RunGrade(CliOptions options)
        {
            GradeResult result = Grader.Grade(options.ExerciseFile, options.AnswerFile);

            string reportPath = Path.Combine(Environment.CurrentDirectory, Grader.ReportFileName);
            Grader.WriteReport(reportPath, result);

            for (int i = 0; i < result.Warnings.Count; i++)
            {
                Console.Error.WriteLine("提示：" + result.Warnings[i]);
            }

            Console.WriteLine("共判定 {0} 道题：对 {1} 道，错 {2} 道。",
                              result.Total, result.CorrectCount, result.WrongCount);
            Console.WriteLine("统计结果：{0}", reportPath);
            return 0;
        }

        /// <summary>打印帮助信息。</summary>
        public static void PrintHelp(TextWriter writer)
        {
            writer.WriteLine("小学四则运算题目生成器 Myapp");
            writer.WriteLine();
            writer.WriteLine("用法：");
            writer.WriteLine("  Myapp -n <题目个数> -r <数值范围>      生成题目与答案");
            writer.WriteLine("  Myapp -e <题目文件> -a <答案文件>      判定答案对错");
            writer.WriteLine("  Myapp -h                              显示本帮助");
            writer.WriteLine();
            writer.WriteLine("参数：");
            writer.WriteLine("  -n <个数>   要生成的题目个数，默认 10，必须是不小于 1 的自然数");
            writer.WriteLine("  -r <范围>   题目中数值的范围，题目里的自然数、分数的分子和分母都小于该值；");
            writer.WriteLine("              必须是不小于 1 的自然数，本参数必须给定");
            writer.WriteLine("  -e <文件>   题目文件，每行一道题，形如 1 + 2 =");
            writer.WriteLine("  -a <文件>   答案文件，每行一个答案，与题目文件逐行对应");
            writer.WriteLine();
            writer.WriteLine("输出：");
            writer.WriteLine("  生成模式：在当前目录写出 Exercises.txt（题目）与 Answers.txt（答案）");
            writer.WriteLine("  判题模式：在当前目录写出 Grade.txt，格式为");
            writer.WriteLine("              Correct: 5 (1, 3, 5, 7, 9)");
            writer.WriteLine("              Wrong: 5 (2, 4, 6, 8, 10)");
            writer.WriteLine();
            writer.WriteLine("示例：");
            writer.WriteLine("  Myapp -n 10 -r 10");
            writer.WriteLine("  Myapp -e Exercises.txt -a Answers.txt");
        }
    }
}
