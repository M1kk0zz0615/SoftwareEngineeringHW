/*
 * test_check.c —— 计算模块单元测试
 *
 * 设计思路（对应博客「单元测试」一节）：
 *   1. 等价类划分：完全相同 / 完全不同 / 插入 / 删除 / 空白差异 / 空文件
 *   2. 边界值：空串、单字符、长度恰好落在 64 位边界（63/64/65/127/128/129）
 *   3. 差分测试：用经典 DP 当参照答案，海量随机串上验证位并行实现
 */
#include "utest.h"
#include "../check.h"

#include <stdlib.h>
#include <string.h>

/* ------------------------------------------------------------------ */
/* 测试辅助                                                            */
/* ------------------------------------------------------------------ */

/* 去空白后按生产口径算重复率；出错返回 -1 */
static double rate_of(const char *a, const char *b)
{
    size_t la = strlen(a);
    size_t lb = strlen(b);
    unsigned char *oa = (unsigned char *)malloc(la + 1u);
    unsigned char *ob = (unsigned char *)malloc(lb + 1u);
    size_t na = 0;
    size_t nb = 0;
    CheckResult r;
    CkStatus st = CK_OK;

    if (oa == NULL || ob == NULL) {
        free(oa);
        free(ob);
        return -1.0;
    }

    na = check_strip_blank((const unsigned char *)a, la, oa);
    nb = check_strip_blank((const unsigned char *)b, lb, ob);
    st = check_similarity(oa, na, ob, nb, &r);

    free(oa);
    free(ob);
    return (st == CK_OK) ? r.rate : -1.0;
}

/* 直接比较两个 C 串的 LCS 长度 */
static size_t lcs_str(const char *a, const char *b)
{
    return check_lcs((const unsigned char *)a, strlen(a),
                     (const unsigned char *)b, strlen(b));
}

/* 可复现的伪随机数发生器（xorshift64） */
static unsigned long long g_seed = 88172645463325252ULL;

static unsigned int rnd_below(unsigned int bound)
{
    g_seed ^= g_seed << 13;
    g_seed ^= g_seed >> 7;
    g_seed ^= g_seed << 17;
    return (unsigned int)((g_seed >> 16) % bound);
}

/* ------------------------------------------------------------------ */
/* 用例                                                                */
/* ------------------------------------------------------------------ */

void run_check_tests(void)
{
    /* --- 用例 01：两篇完全相同 --- */
    UT_BEGIN("用例01 原文与抄袭版完全相同 -> 1.00");
    UT_CHECK_NEAR(rate_of("今天是星期天，天气晴。", "今天是星期天，天气晴。"), 1.00, 1e-9);

    /* --- 用例 02：两篇毫无关系 --- */
    UT_BEGIN("用例02 两篇毫无公共字符 -> 0.00");
    UT_CHECK_NEAR(rate_of("abcd", "wxyz"), 0.00, 1e-9);

    /* --- 用例 03：只插入干扰字，原文一字未删 --- */
    UT_BEGIN("用例03 纯插入干扰字 -> 1.00（子序列性质）");
    UT_CHECK_NEAR(rate_of("abcdef", "aXbYcZdQeWf"), 1.00, 1e-9);

    /* --- 用例 04：精确删除 20% 的内容 --- */
    UT_BEGIN("用例04 等间隔删除 20% -> 0.80");
    {
        /* 100 个字符，每逢 5 的倍数删掉一个，剩 80 个 */
        char orig[101];
        char copy[101];
        size_t i = 0;
        size_t j = 0;

        for (i = 0; i < 100; i++) {
            orig[i] = (char)('a' + (i % 20));
        }
        orig[100] = '\0';

        for (i = 0; i < 100; i++) {
            if ((i % 5) != 4) {
                copy[j] = orig[i];
                j++;
            }
        }
        copy[j] = '\0';

        UT_CHECK_SIZE(j, 80);
        UT_CHECK_NEAR(rate_of(orig, copy), 0.80, 1e-9);
    }

    /* --- 用例 05：抄袭版在原文尾部追加内容 --- */
    UT_BEGIN("用例05 尾部追加内容不改变重复率 -> 1.00");
    UT_CHECK_NEAR(rate_of("hello world", "hello world!!!appended"), 1.00, 1e-9);

    /* --- 用例 06：原文为空（除零保护） --- */
    UT_BEGIN("用例06 原文为空 -> 0.00 且不除零");
    UT_CHECK_NEAR(rate_of("", "任意内容"), 0.00, 1e-9);

    /* --- 用例 07：抄袭版为空 --- */
    UT_BEGIN("用例07 抄袭版为空 -> 0.00");
    UT_CHECK_NEAR(rate_of("原文内容", ""), 0.00, 1e-9);

    /* --- 用例 08：只有空白字符差异 --- */
    UT_BEGIN("用例08 仅有空白差异 -> 1.00（空白不参与比较）");
    UT_CHECK_NEAR(rate_of("a b\tc\nd", "a\r\nb  c   d"), 1.00, 1e-9);

    /* --- 用例 09：单字符边界 --- */
    UT_BEGIN("用例09 单字符边界值");
    UT_CHECK_NEAR(rate_of("a", "a"), 1.00, 1e-9);
    UT_CHECK_NEAR(rate_of("a", "b"), 0.00, 1e-9);

    /* --- 用例 10：大小写敏感 --- */
    UT_BEGIN("用例10 大小写敏感，ABC 与 abc 视为不同");
    UT_CHECK_NEAR(rate_of("ABC", "abc"), 0.00, 1e-9);

    /* --- 用例 11：UTF-8 中文（按字节比较，注意其副作用） --- */
    UT_BEGIN("用例11 UTF-8 中文文本");
    UT_CHECK_NEAR(rate_of("活着前言", "活着前言"), 1.00, 1e-9);
    /*
     * 两个毫不相关的中文串，字节级 LCS 却不为 0：
     *   "活着前言" = E6 B4 BB E7 9D 80 E5 89 8D E8 A8 80
     *   "完全不同" = E5 AE 8C E5 85 A8 E4 B8 8D E5 90 8C
     * 公共字节是 E5 和 8D，共 2 个，于是得 2/12 = 0.1667。
     * 这是「按字节而非按字符比较」的固有代价，已在博客中说明。
     */
    UT_CHECK_NEAR(rate_of("活着前言", "完全不同"), 2.0 / 12.0, 1e-9);

    /* --- 用例 12：公共内容夹在中间，考验前后缀裁剪 --- */
    UT_BEGIN("用例12 公共内容在中间，前后缀裁剪后仍正确");
    {
        /* 两端各 4 个不同字符，中间 "hello" 相同 -> LCS = 5，原文长 13 */
        UT_CHECK_NEAR(rate_of("PPPPhelloQQQQ", "RRRRhelloSSSS"), 5.0 / 13.0, 1e-9);
    }

    /* --- 用例 13：长度恰好跨 64 位字边界 --- */
    UT_BEGIN("用例13 跨 64 位字边界（63/64/65/127/128/129）");
    {
        static const size_t lens[] = {63u, 64u, 65u, 127u, 128u, 129u};
        size_t t = 0;

        for (t = 0; t < sizeof(lens) / sizeof(lens[0]); t++) {
            size_t len = lens[t];
            unsigned char *a = (unsigned char *)malloc(len + 1u);
            unsigned char *b = (unsigned char *)malloc(len + 1u);
            size_t i = 0;
            size_t got = 0;
            size_t want = 0;

            if (a == NULL || b == NULL) {
                free(a);
                free(b);
                UT_CHECK(0);
                continue;
            }

            /* a 为递增序列，b 与 a 完全相同 -> LCS 必等于 len */
            for (i = 0; i < len; i++) {
                a[i] = (unsigned char)('a' + (int)(i % 26));
            }
            memcpy(b, a, len);

            got = check_lcs(a, len, b, len);
            want = check_lcs_dp(a, len, b, len);
            UT_CHECK_SIZE(got, want);
            UT_CHECK_SIZE(got, len);

            free(a);
            free(b);
        }
    }

    /* --- 用例 14：随机串差分测试（位并行 vs 动态规划） --- */
    UT_BEGIN("用例14 随机差分测试 2000 组");
    {
        int round = 0;
        int mismatches = 0;

        for (round = 0; round < 2000; round++) {
            unsigned int la = rnd_below(120u);
            unsigned int lb = rnd_below(120u);
            /* 显式清零：长度为 0 时也保证数组已初始化，静态分析工具不会有歧义 */
            unsigned char a[120] = {0};
            unsigned char b[120] = {0};

            for (unsigned int i = 0; i < la; i++) {
                a[i] = (unsigned char)('A' + (int)rnd_below(4u));
            }
            for (unsigned int i = 0; i < lb; i++) {
                b[i] = (unsigned char)('A' + (int)rnd_below(4u));
            }

            if (check_lcs(a, la, b, lb) != check_lcs_dp(a, la, b, lb)) {
                mismatches++;
            }
        }
        UT_CHECK_SIZE(mismatches, 0);
    }

    /* --- 用例 15：高度重复的字符 --- */
    UT_BEGIN("用例15 全同字符");
    /* 分母是「原文」长度：原文 10 个 a，抄袭版只有 5 个，LCS 最多取到 5 */
    UT_CHECK_NEAR(rate_of("aaaaaaaaaa", "aaaaa"), 0.50, 1e-9);
    /* 反过来原文只有 5 个 a，被全部命中 */
    UT_CHECK_NEAR(rate_of("aaaaa", "aaaaaaaaaa"), 1.00, 1e-9);

    /* --- 用例 16：完全逆序 --- */
    UT_BEGIN("用例16 完全逆序的字符串");
    {
        /* "abcdefghij" 与 "jihgfedcba" 的 LCS 只有 1 个字符 */
        UT_CHECK_NEAR(rate_of("abcdefghij", "jihgfedcba"), 0.10, 1e-9);
    }

    /* --- 用例 17：去空白函数本身 --- */
    UT_BEGIN("用例17 check_strip_blank 原地过滤");
    {
        unsigned char buf[32];
        size_t n = 0;

        memcpy(buf, " a\tb\n c ", 8);
        n = check_strip_blank(buf, 8, buf);   /* 允许原地过滤 */
        UT_CHECK_SIZE(n, 3);
        UT_CHECK(buf[0] == 'a' && buf[1] == 'b' && buf[2] == 'c');
    }

    /* --- 用例 18：空白字符判定 --- */
    UT_BEGIN("用例18 check_is_blank 覆盖 6 种空白字符");
    UT_CHECK(check_is_blank(' ') != 0);
    UT_CHECK(check_is_blank('\t') != 0);
    UT_CHECK(check_is_blank('\n') != 0);
    UT_CHECK(check_is_blank('\r') != 0);
    UT_CHECK(check_is_blank('\f') != 0);
    UT_CHECK(check_is_blank('\v') != 0);
    UT_CHECK(check_is_blank('x') == 0);
    UT_CHECK(check_is_blank((unsigned char)0xE6) == 0);   /* UTF-8 首字节不是空白 */

    /* --- 用例 19：long 串下 LCS 等于自身长度 --- */
    UT_BEGIN("用例19 长文本自身比较 LCS 等于全长");
    {
        unsigned char *a = (unsigned char *)malloc(5000u);

        if (a == NULL) {
            UT_CHECK(0);
        } else {
            for (size_t i = 0; i < 5000u; i++) {
                a[i] = (unsigned char)('a' + (int)rnd_below(6u));
            }

            UT_CHECK_SIZE(check_lcs(a, 5000u, a, 5000u), 5000u);
            free(a);
        }
    }

    /* --- 用例 20：check_similarity 的中间量 --- */
    UT_BEGIN("用例20 check_similarity 结果结构体字段");
    {
        const unsigned char a[] = "abcde";
        const unsigned char b[] = "  a b c d e  ";
        unsigned char sb[32];
        size_t nb = check_strip_blank(b, sizeof(b) - 1u, sb);
        CheckResult r;
        CkStatus st = check_similarity(a, 5u, sb, nb, &r);

        UT_CHECK(st == CK_OK);
        UT_CHECK_SIZE(r.orig_len, 5u);
        UT_CHECK_SIZE(r.copy_len, 5u);
        UT_CHECK_SIZE(r.common_len, 5u);
        UT_CHECK_NEAR(r.rate, 1.00, 1e-9);
    }

    (void)lcs_str;
}
