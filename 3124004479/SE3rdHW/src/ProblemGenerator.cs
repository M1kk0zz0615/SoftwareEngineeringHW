using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ArithGenerator
{
    /// <summary>一道四则运算题目：题面、答案，以及用于查重的规范形式。</summary>
    public sealed class Problem
    {
        /// <summary>表达式的根节点。</summary>
        public Expression Expr { get; private set; }

        /// <summary>正确答案。</summary>
        public Fraction Answer { get; private set; }

        /// <summary>题面文本，形如 "1 + 2 = "。</summary>
        public string Text { get; private set; }

        /// <summary>答案文本，形如 "3" 或 "2'3/8"。</summary>
        public string AnswerText { get; private set; }

        /// <summary>查重用的规范形式，见 <see cref="Expression.ToCanonical"/>。</summary>
        public string Canonical { get; private set; }

        internal Problem(Expression expr)
        {
            Expr = expr;
            Answer = expr.Value;
            Text = expr.ToInfix() + " = ";
            AnswerText = expr.Value.ToString();
            Canonical = expr.ToCanonical();
        }
    }

    /// <summary>
    /// 题目生成器。
    /// 随机生成运算符个数不超过 3 的表达式树，并在生成过程中保证三条题目约束：
    ///   1) 不出现负数：减法子表达式 e1 − e2 一定满足 e1 ≥ e2；
    ///   2) 除法的结果一定是真分数：e1 ÷ e2 一定满足 0 &lt; e1 &lt; e2；
    ///   3) 一次运行内不产生重复题目：用表达式的规范形式在哈希表里查重。
    /// </summary>
    public sealed class ProblemGenerator
    {
        /// <summary>需求 6：每道题目中出现的运算符个数不超过 3 个。</summary>
        public const int MaxOperatorCount = 3;

        /// <summary>单道题目的最大尝试次数；超过说明该数值范围下已几乎无新题目可出。</summary>
        private const int MaxAttemptsPerProblem = 1000;

        /// <summary>生成分数形态题目的概率（百分比）。</summary>
        private const int FractionPercent = 30;

        /// <summary>生成数值 0 的概率（百分比），让题目有点变化。</summary>
        private const int ZeroPercent = 10;

        /// <summary>生成数值 1 的概率（百分比），刻意压低，避免出现 "1 × 1 × 1 × 2" 这类没什么意义的题目。</summary>
        private const int OnePercent = 8;

        /// <summary>一道题里数值为 0 或 1 的操作数最多允许的个数，超过就重新出题，免得题目太水。</summary>
        private const int MaxTrivialLeafCount = 2;

        /// <summary>生成题目时的输出文件名，写在执行程序的当前目录。</summary>
        public const string ExerciseFileName = "Exercises.txt";

        /// <summary>生成答案时的输出文件名。</summary>
        public const string AnswerFileName = "Answers.txt";

        private readonly int _range;
        private readonly Random _random;
        private readonly HashSet<string> _generated = new HashSet<string>();
        private bool _exhausted;

        /// <summary>用随机种子创建生成器。</summary>
        public ProblemGenerator(int range)
            : this(range, 0)
        {
        }

        /// <summary>用指定种子创建生成器，seed 为 0 表示每次运行都是不同的题目。</summary>
        public ProblemGenerator(int range, int seed)
        {
            if (range < 1)
            {
                throw new ArgumentOutOfRangeException("range", "数值范围 -r 必须是大于 0 的自然数。");
            }
            _range = range;
            _random = seed > 0
                ? new Random(seed)
                : new Random(Environment.TickCount ^ Guid.NewGuid().GetHashCode());
        }

        /// <summary>数值范围 r，题目中出现的自然数与分数的分子、分母都小于 r。</summary>
        public int Range { get { return _range; } }

        /// <summary>本次运行已生成的题目数。</summary>
        public int GeneratedCount { get { return _generated.Count; } }

        /// <summary>是否因为数值范围太小、可出的题目已被穷尽而没能凑够指定的题目数。</summary>
        public bool Exhausted { get { return _exhausted; } }

        /// <summary>
        /// 生成 count 道互不重复的题目。
        /// 返回的列表可能少于 count（例如 -r 1 时全部数值只能是 0，可出的题目极其有限），
        /// 这时 <see cref="Exhausted"/> 为 true，调用方应给出提示。
        /// </summary>
        public List<Problem> Generate(int count)
        {
            if (count < 1)
            {
                throw new ArgumentOutOfRangeException("count", "题目个数 -n 必须是大于 0 的自然数。");
            }

            List<Problem> problems = new List<Problem>(count);
            for (int i = 0; i < count; i++)
            {
                Problem problem = GenerateOne();
                if (problem == null)
                {
                    _exhausted = true;
                    break;
                }
                problems.Add(problem);
            }
            return problems;
        }

        /// <summary>生成一道不与已有题目重复的题目；尝试次数用尽仍失败时返回 null。</summary>
        public Problem GenerateOne()
        {
            for (int attempt = 0; attempt < MaxAttemptsPerProblem; attempt++)
            {
                // 运算符个数取 1 ~ 3
                Expression expression = BuildRandom(_random.Next(1, MaxOperatorCount + 1));

                // 数值范围足够大时过滤掉太水的题目；-r 1、-r 2 时数值本来就只有 0 和 1，不能过滤
                if (_range >= 3 && CountTrivialLeaves(expression) > MaxTrivialLeafCount)
                {
                    continue;
                }

                Problem problem = new Problem(expression);

                // 需求 7：同一道题的不同写法（交换 + 和 × 的左右操作数）算重复，用规范形式查重
                if (_generated.Contains(problem.Canonical))
                {
                    continue;
                }
                _generated.Add(problem.Canonical);
                return problem;
            }
            return null;
        }

        /// <summary>统计数值为 0 或 1 的叶子个数，用于过滤掉内容过于平淡的题目。</summary>
        private static int CountTrivialLeaves(Expression expression)
        {
            if (expression.IsLeaf)
            {
                return expression.Value.IsZero || expression.Value == Fraction.One ? 1 : 0;
            }
            return CountTrivialLeaves(expression.Left) + CountTrivialLeaves(expression.Right);
        }

        /// <summary>递归构造一棵随机的表达式树，operators 为这棵树要使用的运算符个数。</summary>
        private Expression BuildRandom(int operators)
        {
            if (operators <= 0)
            {
                return Expression.Leaf(RandomValue());
            }

            // 把剩下的运算符分配给左右子树
            int leftOperators = _random.Next(0, operators);
            int rightOperators = operators - 1 - leftOperators;

            Expression left = BuildRandom(leftOperators);
            Expression right = BuildRandom(rightOperators);

            // Add / Sub / Mul / Div 对应 1 ~ 4
            NodeKind kind = (NodeKind)_random.Next((int)NodeKind.Add, (int)NodeKind.Div + 1);
            return MakeNode(kind, left, right);
        }

        /// <summary>
        /// 按指定运算符连接左右子树，并保证题目约束成立。
        /// 减法：若左值小于右值就交换左右子树（交换后依然是另一道合法的题目，且结果不为负）。
        /// 除法：只有 0 &lt; 左值 &lt; 右值 才能让结果成为真分数，否则交换左右；交换也无法满足时
        ///       改用加、减、乘三者之一，避免整道题作废、白白浪费一次尝试。
        /// </summary>
        private Expression MakeNode(NodeKind kind, Expression left, Expression right)
        {
            if (kind == NodeKind.Sub)
            {
                if (left.Value < right.Value)
                {
                    return Expression.Binary(NodeKind.Sub, right, left);
                }
                return Expression.Binary(NodeKind.Sub, left, right);
            }

            if (kind == NodeKind.Div)
            {
                if (left.Value.IsPositive && left.Value < right.Value)
                {
                    return Expression.Binary(NodeKind.Div, left, right);
                }
                if (right.Value.IsPositive && right.Value < left.Value)
                {
                    return Expression.Binary(NodeKind.Div, right, left);
                }
                // 左右相等或含 0，除法凑不出真分数，退化成加减乘中的一种
                NodeKind fallback = (NodeKind)_random.Next((int)NodeKind.Add, (int)NodeKind.Mul + 1);
                return MakeNode(fallback, left, right);
            }

            return Expression.Binary(kind, left, right);
        }

        /// <summary>
        /// 随机生成一个题面数值。
        /// 需求 3：-r 控制自然数、分数的分子与分母的范围，所以它们都落在 [0, r) 内。
        /// 需求 2：分数可以是真分数，也可以是带分数（如 1'1/2，对应 3/2）。
        /// </summary>
        private Fraction RandomValue()
        {
            if (_range >= 3 && _random.Next(100) < FractionPercent)
            {
                int denominator = _random.Next(2, _range);   // 分母 ∈ [2, r)
                int numerator = _random.Next(1, _range);     // 分子 ∈ [1, r)
                return new Fraction(numerator, denominator);
            }

            if (_range >= 2)
            {
                int dice = _random.Next(100);
                if (dice < ZeroPercent)
                {
                    return Fraction.Zero;
                }
                if (dice < ZeroPercent + OnePercent)
                {
                    return Fraction.One;
                }
                // 其余自然数在 [2, r) 内均匀取值
                if (_range >= 3)
                {
                    return new Fraction(_random.Next(2, _range));
                }
                return Fraction.One;   // r == 2 时自然数只有 0 和 1
            }

            return Fraction.Zero;   // r == 1 时数值只能是 0
        }

        /// <summary>
        /// 把题目与答案写入文件。使用 UTF-8(BOM) 编码，保证记事本打开时
        /// 中文与 × ÷ − 这些符号不会乱码。
        /// </summary>
        public static void WriteFiles(string exercisePath, string answerPath, List<Problem> problems)
        {
            if (problems == null)
            {
                throw new ArgumentNullException("problems");
            }

            StringBuilder exerciseBuilder = new StringBuilder();
            StringBuilder answerBuilder = new StringBuilder();
            for (int i = 0; i < problems.Count; i++)
            {
                exerciseBuilder.AppendLine(problems[i].Text);
                answerBuilder.AppendLine(problems[i].AnswerText);
            }

            UTF8Encoding encoding = new UTF8Encoding(true);
            File.WriteAllText(exercisePath, exerciseBuilder.ToString(), encoding);
            File.WriteAllText(answerPath, answerBuilder.ToString(), encoding);
        }
    }
}
