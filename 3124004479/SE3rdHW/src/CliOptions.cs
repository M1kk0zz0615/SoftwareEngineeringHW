using System;
using System.Collections.Generic;

namespace ArithGenerator
{
    /// <summary>程序的运行模式。</summary>
    public enum RunMode
    {
        /// <summary>生成题目与答案。</summary>
        Generate = 0,

        /// <summary>判定答案对错。</summary>
        Grade = 1,

        /// <summary>运行自检（隐藏命令 --selftest）。</summary>
        SelfTest = 2
    }

    /// <summary>
    /// 命令行参数的解析结果。
    /// 支持 "Myapp -n 10 -r 10" 与 "Myapp -e 题目文件 -a 答案文件" 两种用法，
    /// 参数取值既可以空格分隔，也可以用等号连接（如 -n=10）。
    /// </summary>
    public sealed class CliOptions
    {
        /// <summary>题目个数 -n 的默认值。</summary>
        public const int DefaultCount = 10;

        /// <summary>题目个数上限，防止一次生成过多题目把内存撑爆。</summary>
        public const int MaxCount = 1000000;

        /// <summary>运行模式。</summary>
        public RunMode Mode { get; private set; }

        /// <summary>题目个数 -n。</summary>
        public int Count { get; private set; }

        /// <summary>数值范围 -r，未指定时为 -1。</summary>
        public int Range { get; private set; }

        /// <summary>判题模式下的题目文件。</summary>
        public string ExerciseFile { get; private set; }

        /// <summary>判题模式下的答案文件。</summary>
        public string AnswerFile { get; private set; }

        /// <summary>参数错误信息，为 null 表示参数合法。</summary>
        public string ErrorMessage { get; private set; }

        /// <summary>是否请求显示帮助信息。</summary>
        public bool ShowHelp { get; private set; }

        private CliOptions()
        {
            Mode = RunMode.Generate;
            Count = DefaultCount;
            Range = -1;
        }

        /// <summary>解析命令行参数。参数不合法时 ErrorMessage 非空，由调用方打印帮助信息。</summary>
        public static CliOptions Parse(string[] args)
        {
            CliOptions options = new CliOptions();
            if (args == null)
            {
                options.ErrorMessage = "没有收到任何参数。";
                return options;
            }

            // 第一步：把参数整理成"名称 + 取值"的形式，顺便处理 -h / 未知参数
            List<string> names = new List<string>();
            List<string> values = new List<string>();

            for (int i = 0; i < args.Length; i++)
            {
                string argument = args[i];
                if (argument == null || argument.Trim().Length == 0)
                {
                    continue;
                }

                string name = argument;
                string value = null;
                int equalsIndex = argument.IndexOf('=');
                if (equalsIndex > 0)
                {
                    name = argument.Substring(0, equalsIndex);
                    value = argument.Substring(equalsIndex + 1);
                }
                name = name.Trim();

                if (name == "-h" || name == "--help" || name == "-help" || name == "/?")
                {
                    options.ShowHelp = true;
                    return options;
                }
                if (name == "--selftest")
                {
                    options.Mode = RunMode.SelfTest;
                    return options;
                }
                if (!IsKnownOption(name))
                {
                    options.ErrorMessage = "无法识别的参数：" + argument;
                    return options;
                }
                if (value == null)
                {
                    if (i + 1 >= args.Length)
                    {
                        options.ErrorMessage = "参数 " + name + " 后面缺少取值。";
                        return options;
                    }
                    value = args[++i];
                }
                names.Add(name);
                values.Add(value);
            }

            // 第二步：按名称逐项校验取值
            string exerciseFile = null;
            string answerFile = null;

            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                string value = values[i];

                if (name == "-n")
                {
                    int count;
                    if (!int.TryParse(value, out count) || count < 1)
                    {
                        options.ErrorMessage = "-n 的取值必须是不小于 1 的自然数，当前为：" + value;
                        return options;
                    }
                    if (count > MaxCount)
                    {
                        options.ErrorMessage = "-n 的取值不能超过 " + MaxCount + "，当前为：" + value;
                        return options;
                    }
                    options.Count = count;
                }
                else if (name == "-r")
                {
                    int range;
                    if (!int.TryParse(value, out range) || range < 1)
                    {
                        options.ErrorMessage = "-r 的取值必须是不小于 1 的自然数，当前为：" + value;
                        return options;
                    }
                    options.Range = range;
                }
                else if (name == "-e")
                {
                    exerciseFile = value;
                }
                else if (name == "-a")
                {
                    answerFile = value;
                }
            }

            // 第三步：决定运行模式
            if (exerciseFile != null || answerFile != null)
            {
                if (exerciseFile == null || answerFile == null)
                {
                    options.ErrorMessage = "判题需要同时给出题目文件与答案文件，例如 -e Exercises.txt -a Answers.txt。";
                    return options;
                }
                if (exerciseFile.Trim().Length == 0 || answerFile.Trim().Length == 0)
                {
                    options.ErrorMessage = "-e 与 -a 的文件名不能为空。";
                    return options;
                }
                options.Mode = RunMode.Grade;
                options.ExerciseFile = exerciseFile;
                options.AnswerFile = answerFile;
                return options;
            }

            // 需求 3：-r 必须给定，否则报错并给出帮助信息
            if (options.Range < 1)
            {
                options.ErrorMessage = "必须用 -r 指定数值范围，例如 Myapp -n 10 -r 10。";
                return options;
            }

            options.Mode = RunMode.Generate;
            return options;
        }

        private static bool IsKnownOption(string name)
        {
            return name == "-n" || name == "-r" || name == "-e" || name == "-a";
        }
    }
}
