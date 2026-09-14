#include "check.h"

#include <stdint.h>
#include <stdlib.h>

/* ------------------------------------------------------------------ */
/* 文本预处理                                                          */
/* ------------------------------------------------------------------ */

int check_is_blank(unsigned char c)
{
    return c == ' ' || c == '\t' || c == '\n' ||
           c == '\r' || c == '\f' || c == '\v';
}

size_t check_strip_blank(const unsigned char *src, size_t len, unsigned char *dst)
{
    unsigned char *out = dst;
    size_t i = 0;

    for (i = 0; i < len; i++) {
        unsigned char c = src[i];

        if (!check_is_blank(c)) {
            *out = c;
            out++;
        }
    }
    return (size_t)(out - dst);
}

/* ------------------------------------------------------------------ */
/* 参照实现：经典动态规划 LCS                                          */
/* ------------------------------------------------------------------ */

size_t check_lcs_dp(const unsigned char *a, size_t m, const unsigned char *b, size_t n)
{
    size_t result = 0;
    size_t *prev = NULL;
    size_t *curr = NULL;

    if (m == 0 || n == 0) {
        return 0;
    }

    /* 滚动数组按较长的那条串分配，空间 O(min(m,n)) */
    if (m > n) {
        const unsigned char *tc = a;
        size_t tl = m;
        a = b;
        m = n;
        b = tc;
        n = tl;
    }

    prev = (size_t *)calloc(n + 1, sizeof(size_t));
    curr = (size_t *)calloc(n + 1, sizeof(size_t));
    if (prev == NULL || curr == NULL) {
        free(prev);
        free(curr);
        return SIZE_MAX;
    }

    for (size_t i = 1; i <= m; i++) {
        for (size_t j = 1; j <= n; j++) {
            if (a[i - 1] == b[j - 1]) {
                /* 末尾字符相同：接在两者都去掉末尾的那一格后面 */
                curr[j] = prev[j - 1] + 1;
            } else {
                /* 否则取「丢掉 a 末尾」和「丢掉 b 末尾」中较大的那个 */
                curr[j] = (prev[j] > curr[j - 1]) ? prev[j] : curr[j - 1];
            }
        }
        /* 交换两行，prev 始终指向上一行 */
        {
            size_t *t = prev;
            prev = curr;
            curr = t;
        }
    }

    result = prev[n];
    free(prev);
    free(curr);
    return result;
}

/* ------------------------------------------------------------------ */
/* 生产实现：位并行 LCS（Crochemore 等人的位向量算法）                 */
/* ------------------------------------------------------------------ */

/* 一个位向量需要多少个 64 位字 */
static size_t bitset_words(size_t bits)
{
    return (bits + 63u) / 64u;
}

size_t check_lcs(const unsigned char *a, size_t m, const unsigned char *b, size_t n)
{
    size_t w = 0;              /* 位向量的字数 */
    size_t i = 0;
    size_t j = 0;
    size_t k = 0;
    size_t ones = 0;
    uint64_t top = 0;          /* 最高位所在的字里，有效位的掩码 */
    uint64_t *mask = NULL;     /* 字符 → 该字符在 A 中出现位置的位图 */
    uint64_t *s = NULL;        /* 位向量 S */

    if (m == 0 || n == 0) {
        return 0;
    }

    /* 位向量建在较短的那条串上：字数更少，时间不变但内存更省 */
    if (m > n) {
        const unsigned char *tc = a;
        size_t tl = m;
        a = b;
        m = n;
        b = tc;
        n = tl;
    }

    w = bitset_words(m);
    if (w > SIZE_MAX / 256u / sizeof(uint64_t)) {
        return SIZE_MAX;   /* 长度大到乘法会溢出，直接放弃 */
    }

    mask = (uint64_t *)calloc(256u * w, sizeof(uint64_t));
    s = (uint64_t *)malloc(w * sizeof(uint64_t));
    if (mask == NULL || s == NULL) {
        free(mask);
        free(s);
        return SIZE_MAX;
    }

    /* 建表：mask[c] 的第 i 位为 1 当且仅当 a[i] == c */
    for (i = 0; i < m; i++) {
        uint64_t *row = mask + (size_t)a[i] * w;
        row[i >> 6] |= (uint64_t)1 << (i & 63u);
    }

    /* S 初始全 1（只保留低 m 位有效） */
    for (k = 0; k < w; k++) {
        s[k] = ~(uint64_t)0;
    }
    top = ((m & 63u) != 0u) ? (((uint64_t)1 << (m & 63u)) - 1u) : ~(uint64_t)0;
    s[w - 1] = top;

    /*
     * 逐字符扫描 B。每轮做一次「无分支」的三件事：
     *   X   = S & mask[b[j]]      —— 本轮能匹配上的位置
     *   S+X —— 进位传播，等价于把匹配点整体上移一格
     *   S-X —— 借位传播，等价于把不匹配的洞补上
     *   S   = (S+X) | (S-X)
     * 全部位运算，没有逐位分支，所以是 O(m/64) 而不是 O(m)。
     */
    for (j = 0; j < n; j++) {
        const uint64_t *col = mask + (size_t)b[j] * w;
        uint64_t carry = 0;
        uint64_t borrow = 0;

        for (k = 0; k < w; k++) {
            uint64_t sk = s[k];
            uint64_t xk = sk & col[k];

            /* 一次加法最多产生一次进位，两次比较不会同时成立，故用 + 合成一位 */
            uint64_t t  = sk + xk;
            uint64_t ta = t + carry;
            carry = (uint64_t)((t < sk) + (ta < t));

            /* 减法同理，一次借位 */
            uint64_t d  = sk - xk;
            uint64_t ds = d - borrow;
            borrow = (uint64_t)((sk < xk) + (d < borrow));

            s[k] = ta | ds;
        }

        /* 砍掉溢出到 m 位以上的部分 */
        s[w - 1] &= top;
    }

    /* S 中 0 的个数就是两个串的最长公共子序列长度 */
    for (k = 0; k < w; k++) {
        ones += (size_t)__builtin_popcountll(s[k]);
    }

    free(mask);
    free(s);
    return m - ones;
}

/* ------------------------------------------------------------------ */
/* 对外接口：算重复率                                                  */
/* ------------------------------------------------------------------ */

/*
 * 裁掉两串公共的前缀与后缀。
 * a、b 会被更新为中间部分的起点，m、n 更新为中间部分的长度。
 * 返回被裁掉的总字节数。
 *
 * 这一步很值钱：真实的抄袭文本往往是大段照抄，公共前后缀能一口气吃掉
 * 绝大部分内容，剩下的中间部分才需要真正跑 O(m*n/64) 的 LCS。
 */
static size_t trim_common_affix(const unsigned char **a, size_t *m,
                                const unsigned char **b, size_t *n)
{
    size_t prefix = 0;
    size_t suffix = 0;

    while (prefix < *m && prefix < *n && (*a)[prefix] == (*b)[prefix]) {
        prefix++;
    }

    /*
     * 后缀不能越过前缀已经占用的部分。
     * cppcheck 的取值流分析会误判下面这个条件恒为假——它假定 prefix 已经吃满
     * 整串（*m - prefix == 0），但 prefix 只是「公共前缀」的长度，完全可能为 0。
     * 该分支不是死代码，由单元测试「用例12 公共内容夹在中间」实际覆盖。
     */
    /* cppcheck-suppress knownConditionTrueFalse */
    while (suffix < (*m - prefix) && suffix < (*n - prefix) &&
           (*a)[*m - 1 - suffix] == (*b)[*n - 1 - suffix]) {
        suffix++;
    }

    *a += prefix;
    *m -= prefix + suffix;
    *b += prefix;
    *n -= prefix + suffix;
    return prefix + suffix;
}

CkStatus check_similarity(const unsigned char *orig, size_t orig_len,
                          const unsigned char *copy, size_t copy_len,
                          CheckResult *result)
{
    const unsigned char *pa = orig;
    const unsigned char *pb = copy;
    size_t ma = orig_len;
    size_t nb = copy_len;
    size_t common = 0;

    result->orig_len = orig_len;
    result->copy_len = copy_len;
    result->common_len = 0;
    result->rate = 0.0;

    /* 原文为空：没有内容可被抄袭，约定重复率为 0，顺带避免除零 */
    if (orig_len == 0) {
        return CK_OK;
    }

    /* 第一步：裁掉公共前后缀，把待算的规模压小 */
    common = trim_common_affix(&pa, &ma, &pb, &nb);

    /* 第二步：只剩中间那段时才真正跑 LCS */
    if (ma > 0 && nb > 0) {
        size_t mid = check_lcs(pa, ma, pb, nb);

        if (mid == SIZE_MAX) {
            return CK_ERR_MEM;
        }
        common += mid;
    }

    result->common_len = common;
    result->rate = (double)common / (double)orig_len;
    return CK_OK;
}
