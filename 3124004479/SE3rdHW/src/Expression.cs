using System;
using System.Collections.Generic;
using System.Text;

namespace ArithGenerator
{
    /// <summary>表达式树的节点类型。</summary>
    public enum NodeKind
    {
        /// <summary>叶子节点：一个自然数或分数。</summary>
        Leaf = 0,

        /// <summary>加法。</summary>
        Add = 1,

        /// <summary>减法。</summary>
        Sub = 2,

        /// <summary>乘法。</summary>
        Mul = 3,

        /// <summary>除法。</summary>
        Div = 4
    }

    /// <summary>
    /// 算术表达式（二叉树）。
    /// 对应作业里的文法 e = n | e1 + e2 | e1 − e2 | e1 × e2 | e1 ÷ e2 | (e)：
    /// 括号不占节点，由渲染阶段按优先级自动决定要不要补，因此同一棵树上不会出现多余的括号。
    /// 每个节点的运算结果在构造时算好并缓存，避免重复求值。
    /// </summary>
    public sealed class Expression
    {
        private readonly NodeKind _kind;
        private readonly Fraction _value;
        private readonly Expression _left;
        private readonly Expression _right;

        private Expression(NodeKind kind, Fraction value, Expression left, Expression right)
        {
            _kind = kind;
            _value = value;
            _left = left;
            _right = right;
        }

        /// <summary>节点类型。</summary>
        public NodeKind Kind { get { return _kind; } }

        /// <summary>以本节点为根的子树的计算结果。</summary>
        public Fraction Value { get { return _value; } }

        /// <summary>左子树，叶子节点为 null。</summary>
        public Expression Left { get { return _left; } }

        /// <summary>右子树，叶子节点为 null。</summary>
        public Expression Right { get { return _right; } }

        /// <summary>是否为叶子节点。</summary>
        public bool IsLeaf { get { return _kind == NodeKind.Leaf; } }

        /// <summary>构造叶子节点（一个数值）。</summary>
        public static Expression Leaf(Fraction value)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }
            return new Expression(NodeKind.Leaf, value, null, null);
        }

        /// <summary>
        /// 构造二元运算节点，结果自底向上算好并缓存。
        /// 注意：这里只保证除法的除数不为 0；"不出现负数""除法结果是真分数"这两条
        /// 题目约束由生成器 ProblemGenerator 负责，本类保持为通用表达式树，供判题时复用。
        /// </summary>
        public static Expression Binary(NodeKind kind, Expression left, Expression right)
        {
            if (kind == NodeKind.Leaf)
            {
                throw new ArgumentException("二元节点的类型不能是 Leaf。", "kind");
            }
            if (left == null || right == null)
            {
                throw new ArgumentNullException("left/right");
            }

            Fraction value;
            switch (kind)
            {
                case NodeKind.Add:
                    value = left._value + right._value;
                    break;
                case NodeKind.Sub:
                    value = left._value - right._value;
                    break;
                case NodeKind.Mul:
                    value = left._value * right._value;
                    break;
                case NodeKind.Div:
                    value = left._value / right._value;   // 除数为 0 时内部会抛异常
                    break;
                default:
                    throw new ArgumentException("未知的节点类型：" + kind, "kind");
            }
            return new Expression(kind, value, left, right);
        }

        /// <summary>运算符字符，叶子节点返回空字符。</summary>
        public static char OperatorChar(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Add: return '+';
                case NodeKind.Sub: return '−';   // 数学减号 U+2212，作业规定的写法
                case NodeKind.Mul: return '×';
                case NodeKind.Div: return '÷';
                default: return '\0';
            }
        }

        /// <summary>运算优先级：加减 1，乘除 2，叶子 3（越大越"紧"）。</summary>
        private int Precedence
        {
            get
            {
                switch (_kind)
                {
                    case NodeKind.Add:
                    case NodeKind.Sub:
                        return 1;
                    case NodeKind.Mul:
                    case NodeKind.Div:
                        return 2;
                    default:
                        return 3;
                }
            }
        }

        /// <summary>本节点是否为不满足结合律的运算（减法、除法），渲染时需要给右子树加括号。</summary>
        private bool IsNonAssociative
        {
            get { return _kind == NodeKind.Sub || _kind == NodeKind.Div; }
        }

        /// <summary>统计整棵树的运算符个数。需求 6 要求不超过 3 个。</summary>
        public int OperatorCount()
        {
            if (IsLeaf)
            {
                return 0;
            }
            return 1 + _left.OperatorCount() + _right.OperatorCount();
        }

        /// <summary>收集所有叶子节点的数值，供测试校验数值范围用。</summary>
        public void CollectLeaves(List<Fraction> into)
        {
            if (IsLeaf)
            {
                into.Add(_value);
                return;
            }
            _left.CollectLeaves(into);
            _right.CollectLeaves(into);
        }

        /// <summary>
        /// 输出带最少括号的中缀表达式，如 "(1 + 2) × 3"、"1 ÷ (2 × 3)"、"1 + 2 + 3"。
        /// 括号规则：子表达式优先级低于父节点时必须加括号；同优先级时，
        /// 只有处在减法／除法的右侧才必须加括号（这两个运算不满足结合律）。
        /// </summary>
        public string ToInfix()
        {
            return Render(0, false);
        }

        private string Render(int parentPrecedence, bool needsParenOnEqualPrecedence)
        {
            if (IsLeaf)
            {
                return _value.ToString();
            }

            int myPrecedence = Precedence;
            string text = _left.Render(myPrecedence, false)
                          + " " + OperatorChar(_kind) + " "
                          + _right.Render(myPrecedence, IsNonAssociative);

            if (myPrecedence < parentPrecedence || (myPrecedence == parentPrecedence && needsParenOnEqualPrecedence))
            {
                return "(" + text + ")";
            }
            return text;
        }

        /// <summary>
        /// 输出用于查重的规范形式：每个二元节点整体加一对括号，加法和乘法的左右子树按字典序排序后拼接。
        /// 这样"能通过有限次交换 + 和 × 左右表达式变成同一个题目"的两棵树，规范串必然相同；
        /// 而 1+2+3（即 (1+2)+3）与 3+2+1（即 (3+2)+1）的结构不同，规范串也不同，
        /// 与作业需求 7 的说明完全一致。
        /// </summary>
        public string ToCanonical()
        {
            if (IsLeaf)
            {
                return _value.ToString();
            }

            string left = _left.ToCanonical();
            string right = _right.ToCanonical();

            // 加法和乘法满足交换律，把两个子树按字典序排好，消除左右顺序的差异
            if (_kind == NodeKind.Add || _kind == NodeKind.Mul)
            {
                if (string.CompareOrdinal(left, right) > 0)
                {
                    string temp = left;
                    left = right;
                    right = temp;
                }
            }

            StringBuilder builder = new StringBuilder();
            builder.Append('(').Append(left).Append(OperatorChar(_kind)).Append(right).Append(')');
            return builder.ToString();
        }

        public override string ToString()
        {
            return ToInfix();
        }
    }
}
