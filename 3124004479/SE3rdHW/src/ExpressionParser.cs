using System;
using System.Numerics;

namespace ArithGenerator
{
    /// <summary>
    /// 表达式解析器：把 "1 + 2"、"2'3/8 × 2"、"(1 + 2) ÷ 3" 这样的文本还原成表达式树。
    /// 采用递归下降，优先级由低到高为：加减 → 乘除 → 括号／数值 → 数字。
    /// 兼容 ASCII 写法（* / -）与数学符号（× ÷ −），方便人工编辑过的题目文件也能判题。
    /// </summary>
    public sealed class ExpressionParser
    {
        private readonly string _text;
        private int _pos;

        private ExpressionParser(string text)
        {
            _text = text;
            _pos = 0;
        }

        /// <summary>解析表达式，失败时抛 FormatException。</summary>
        public static Expression Parse(string text)
        {
            Expression result;
            if (!TryParse(text, out result))
            {
                throw new FormatException("无法解析的表达式：" + (text == null ? "(null)" : text));
            }
            return result;
        }

        /// <summary>尝试解析表达式，失败返回 false 而不抛异常。</summary>
        public static bool TryParse(string text, out Expression result)
        {
            result = null;
            if (text == null)
            {
                return false;
            }
            try
            {
                ExpressionParser parser = new ExpressionParser(text);
                Expression expression = parser.ParseAddSub();
                parser.SkipSpaces();
                if (parser._pos != parser._text.Length)
                {
                    return false;   // 还有没被消费掉的字符，说明文本不规范
                }
                result = expression;
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        // ---------- 文法规则：加减 → 乘除 → 括号/数值 ----------

        /// <summary>加减：加法与减法优先级相同，左结合。</summary>
        private Expression ParseAddSub()
        {
            Expression left = ParseTerm();
            while (true)
            {
                SkipSpaces();
                if (_pos >= _text.Length)
                {
                    return left;
                }
                char c = _text[_pos];
                if (c == '+')
                {
                    _pos++;
                    left = Expression.Binary(NodeKind.Add, left, ParseTerm());
                }
                else if (c == '-' || c == '−')
                {
                    _pos++;
                    left = Expression.Binary(NodeKind.Sub, left, ParseTerm());
                }
                else
                {
                    return left;
                }
            }
        }

        /// <summary>乘除：优先级高于加减，左结合。</summary>
        private Expression ParseTerm()
        {
            Expression left = ParseFactor();
            while (true)
            {
                SkipSpaces();
                if (_pos >= _text.Length)
                {
                    return left;
                }
                char c = _text[_pos];
                if (c == '*' || c == '×')
                {
                    _pos++;
                    left = Expression.Binary(NodeKind.Mul, left, ParseFactor());
                }
                else if (c == '/' || c == '÷')
                {
                    _pos++;
                    left = Expression.Binary(NodeKind.Div, left, ParseFactor());
                }
                else
                {
                    return left;
                }
            }
        }

        /// <summary>因子：括号表达式、一元正负号、或一个数值。</summary>
        private Expression ParseFactor()
        {
            SkipSpaces();
            if (_pos >= _text.Length)
            {
                throw new FormatException("表达式意外结束。");
            }

            char c = _text[_pos];
            if (c == '(')
            {
                _pos++;
                Expression inner = ParseAddSub();
                SkipSpaces();
                if (_pos >= _text.Length || _text[_pos] != ')')
                {
                    throw new FormatException("缺少右括号。");
                }
                _pos++;
                return inner;
            }
            if (c == '-' || c == '−')
            {
                // 一元负号：0 - x，本题不产生负数，这里是为了容忍人工输入的答案
                _pos++;
                return Expression.Binary(NodeKind.Sub, Expression.Leaf(Fraction.Zero), ParseFactor());
            }
            if (c == '+')
            {
                _pos++;
                return ParseFactor();
            }
            return Expression.Leaf(ParseNumber());
        }

        /// <summary>数值：自然数、真分数 a/b、带分数 q'a/b 三种写法。</summary>
        private Fraction ParseNumber()
        {
            string wholeText = ReadDigits();
            if (wholeText.Length == 0)
            {
                throw new FormatException("无法识别的字符：" + _text[_pos]);
            }

            bool hasQuote = false;
            if (_pos < _text.Length
                && (_text[_pos] == Fraction.MixedSeparator || _text[_pos] == Fraction.MixedSeparatorFullWidth))
            {
                hasQuote = true;
                _pos++;
            }

            string numeratorText = ReadDigits();
            if (numeratorText.Length == 0)
            {
                if (hasQuote)
                {
                    throw new FormatException("带分数缺少分子。");
                }
                return new Fraction(BigInteger.Parse(wholeText));
            }

            if (_pos < _text.Length && _text[_pos] == '/')
            {
                _pos++;
                string denominatorText = ReadDigits();
                if (denominatorText.Length == 0)
                {
                    throw new FormatException("分数缺少分母。");
                }
                Fraction fractionPart = new Fraction(BigInteger.Parse(numeratorText),
                                                     BigInteger.Parse(denominatorText));
                if (hasQuote)
                {
                    // 带分数 2'3/8 = 2 + 3/8
                    return new Fraction(BigInteger.Parse(wholeText)) + fractionPart;
                }
                return fractionPart;
            }

            if (hasQuote)
            {
                throw new FormatException("带分数缺少分母。");
            }
            throw new FormatException("数值后面出现了意外的字符。");
        }

        /// <summary>读取连续的阿拉伯数字，读不到时返回空串。</summary>
        private string ReadDigits()
        {
            int start = _pos;
            while (_pos < _text.Length && _text[_pos] >= '0' && _text[_pos] <= '9')
            {
                _pos++;
            }
            return _text.Substring(start, _pos - start);
        }

        private void SkipSpaces()
        {
            while (_pos < _text.Length && (_text[_pos] == ' ' || _text[_pos] == '\t'))
            {
                _pos++;
            }
        }
    }
}
