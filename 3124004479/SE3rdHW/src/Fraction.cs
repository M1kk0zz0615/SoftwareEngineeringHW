using System;
using System.Numerics;

namespace ArithGenerator
{
    /// <summary>
    /// 有理数（分数）。
    /// 内部用 BigInteger 保存分子与分母，构造时立即约分，并保证分母恒为正数，
    /// 因此全程没有浮点误差，"相等"判断退化为分子分母的直接比较。
    /// </summary>
    public sealed class Fraction : IComparable<Fraction>, IEquatable<Fraction>
    {
        /// <summary>数值 0。</summary>
        public static readonly Fraction Zero = new Fraction(BigInteger.Zero, BigInteger.One);

        /// <summary>数值 1。</summary>
        public static readonly Fraction One = new Fraction(BigInteger.One, BigInteger.One);

        /// <summary>带分数的分隔撇号（英文写法），输出时使用。</summary>
        public const char MixedSeparator = '\'';

        /// <summary>带分数的分隔撇号（中文全角写法），解析时一并兼容。</summary>
        public const char MixedSeparatorFullWidth = '’';

        private readonly BigInteger _numerator;
        private readonly BigInteger _denominator;

        /// <summary>分子。符号只保存在分子上。</summary>
        public BigInteger Numerator { get { return _numerator; } }

        /// <summary>分母。恒为正数。</summary>
        public BigInteger Denominator { get { return _denominator; } }

        /// <summary>用分子分母构造分数，自动约分并规范化符号。</summary>
        public Fraction(BigInteger numerator, BigInteger denominator)
        {
            if (denominator.IsZero)
            {
                throw new ArgumentException("分母不能为 0。", "denominator");
            }

            // 统一符号：负号只保留在分子上，保证分母恒正
            if (denominator.Sign < 0)
            {
                numerator = -numerator;
                denominator = -denominator;
            }

            // 约分
            BigInteger gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
            if (gcd > BigInteger.One)
            {
                numerator = numerator / gcd;
                denominator = denominator / gcd;
            }

            _numerator = numerator;
            _denominator = denominator;
        }

        /// <summary>用整数构造分数。</summary>
        public Fraction(BigInteger value)
            : this(value, BigInteger.One)
        {
        }

        /// <summary>是否等于 0。</summary>
        public bool IsZero { get { return _numerator.IsZero; } }

        /// <summary>是否大于 0。</summary>
        public bool IsPositive { get { return _numerator.Sign > 0; } }

        /// <summary>是否为整数（分母为 1）。</summary>
        public bool IsInteger { get { return _denominator.IsOne; } }

        /// <summary>是否是真分数：严格大于 0 且严格小于 1。需求 5 用这个判定除法的结果。</summary>
        public bool IsProperFraction
        {
            get { return _numerator.Sign > 0 && _numerator < _denominator; }
        }

        /// <summary>取相反数。</summary>
        public Fraction Negate()
        {
            return new Fraction(-_numerator, _denominator);
        }

        /// <summary>取绝对值。</summary>
        public Fraction Abs()
        {
            return new Fraction(BigInteger.Abs(_numerator), _denominator);
        }

        public static Fraction operator +(Fraction a, Fraction b)
        {
            // a/b + c/d = (a*d + c*b) / (b*d)，构造时自动约分
            return new Fraction(a._numerator * b._denominator + b._numerator * a._denominator,
                                a._denominator * b._denominator);
        }

        public static Fraction operator -(Fraction a, Fraction b)
        {
            return new Fraction(a._numerator * b._denominator - b._numerator * a._denominator,
                                a._denominator * b._denominator);
        }

        public static Fraction operator *(Fraction a, Fraction b)
        {
            return new Fraction(a._numerator * b._numerator, a._denominator * b._denominator);
        }

        public static Fraction operator /(Fraction a, Fraction b)
        {
            if (b.IsZero)
            {
                throw new DivideByZeroException("除数不能为 0。");
            }
            return new Fraction(a._numerator * b._denominator, a._denominator * b._numerator);
        }

        /// <summary>比较大小。分母恒正，交叉相乘即可，不产生浮点误差。</summary>
        public int CompareTo(Fraction other)
        {
            if (other == null)
            {
                return 1;
            }
            return (_numerator * other._denominator).CompareTo(other._numerator * _denominator);
        }

        public bool Equals(Fraction other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }
            // 构造时已约分且分母恒正，直接比较分子分母即可
            return _numerator == other._numerator && _denominator == other._denominator;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Fraction);
        }

        public override int GetHashCode()
        {
            return _numerator.GetHashCode() * 397 ^ _denominator.GetHashCode();
        }

        public static bool operator ==(Fraction a, Fraction b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }
            if (ReferenceEquals(a, null) || ReferenceEquals(b, null))
            {
                return false;
            }
            return a.Equals(b);
        }

        public static bool operator !=(Fraction a, Fraction b)
        {
            return !(a == b);
        }

        public static bool operator <(Fraction a, Fraction b)
        {
            return a.CompareTo(b) < 0;
        }

        public static bool operator >(Fraction a, Fraction b)
        {
            return a.CompareTo(b) > 0;
        }

        public static bool operator <=(Fraction a, Fraction b)
        {
            return a.CompareTo(b) <= 0;
        }

        public static bool operator >=(Fraction a, Fraction b)
        {
            return a.CompareTo(b) >= 0;
        }

        /// <summary>
        /// 按作业要求的格式输出：
        /// 整数直接输出（如 5）；真分数输出 a/b（如 3/5）；
        /// 假分数输出成带分数（如 19/8 输出 2'3/8）。
        /// </summary>
        public override string ToString()
        {
            BigInteger numerator = _numerator;
            string sign = string.Empty;
            if (numerator.Sign < 0)
            {
                sign = "-";
                numerator = -numerator;
            }

            if (_denominator.IsOne)
            {
                return sign + numerator.ToString();
            }

            BigInteger whole = numerator / _denominator;   // 整数部分
            BigInteger rest = numerator % _denominator;    // 真分数部分的分子

            if (whole.IsZero)
            {
                return sign + rest.ToString() + "/" + _denominator.ToString();
            }
            if (rest.IsZero)
            {
                return sign + whole.ToString();
            }
            return sign + whole.ToString() + MixedSeparator + rest.ToString() + "/" + _denominator.ToString();
        }

        /// <summary>
        /// 解析分数文本，兼容四种写法：
        /// "5" → 5，"3/5" → 3/5，"2'3/8" → 19/8（带分数，撇号兼容 ' 与 ’），"-3/5" → -3/5。
        /// </summary>
        public static Fraction Parse(string text)
        {
            Fraction result;
            if (!TryParse(text, out result))
            {
                throw new FormatException("无法解析为分数的文本：" + (text == null ? "(null)" : text));
            }
            return result;
        }

        /// <summary>尝试解析分数文本，失败返回 false 而不抛异常。</summary>
        public static bool TryParse(string text, out Fraction result)
        {
            result = null;
            if (text == null)
            {
                return false;
            }

            string s = text.Trim();
            if (s.Length == 0)
            {
                return false;
            }

            // 符号
            bool negative = false;
            if (s[0] == '-' || s[0] == '−')   // 兼容 ASCII 减号与数学减号
            {
                negative = true;
                s = s.Substring(1).Trim();
                if (s.Length == 0)
                {
                    return false;
                }
            }
            else if (s[0] == '+')
            {
                s = s.Substring(1).Trim();
            }

            BigInteger whole = BigInteger.Zero;
            string fractionPart = s;

            // 拆出带分数的整数部分
            int quote = s.IndexOf(MixedSeparator);
            if (quote < 0)
            {
                quote = s.IndexOf(MixedSeparatorFullWidth);
            }
            if (quote >= 0)
            {
                if (!BigInteger.TryParse(s.Substring(0, quote).Trim(), out whole))
                {
                    return false;
                }
                fractionPart = s.Substring(quote + 1).Trim();
            }

            BigInteger numerator;
            BigInteger denominator;

            int slash = fractionPart.IndexOf('/');
            if (slash < 0)
            {
                if (!BigInteger.TryParse(fractionPart, out numerator))
                {
                    return false;
                }
                denominator = BigInteger.One;
            }
            else
            {
                if (!BigInteger.TryParse(fractionPart.Substring(0, slash).Trim(), out numerator))
                {
                    return false;
                }
                if (!BigInteger.TryParse(fractionPart.Substring(slash + 1).Trim(), out denominator))
                {
                    return false;
                }
                if (denominator.IsZero)
                {
                    return false;
                }
            }

            // 带分数的分母也要参与，2'3/8 = 2 + 3/8 = (2*8 + 3)/8
            if (!whole.IsZero && denominator != BigInteger.One)
            {
                numerator = whole * denominator + numerator;
            }
            else if (!whole.IsZero)
            {
                numerator = whole + numerator;
            }

            if (negative)
            {
                numerator = -numerator;
            }

            result = new Fraction(numerator, denominator);
            return true;
        }
    }
}
