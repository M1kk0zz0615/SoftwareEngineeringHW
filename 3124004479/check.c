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
/* 最长公共子序列：经典动态规划                                        */
/* ------------------------------------------------------------------ */

size_t check_lcs(const unsigned char *a, size_t m, const unsigned char *b, size_t n)
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
/* 对外接口：算重复率                                                  */
/* ------------------------------------------------------------------ */

CkStatus check_similarity(const unsigned char *orig, size_t orig_len,
                          const unsigned char *copy, size_t copy_len,
                          CheckResult *result)
{
    size_t common = 0;

    result->orig_len = orig_len;
    result->copy_len = copy_len;
    result->common_len = 0;
    result->rate = 0.0;

    /* 原文为空：没有内容可被抄袭，约定重复率为 0，顺带避免除零 */
    if (orig_len == 0) {
        return CK_OK;
    }

    common = check_lcs(orig, orig_len, copy, copy_len);
    if (common == SIZE_MAX) {
        return CK_ERR_MEM;
    }

    result->common_len = common;
    result->rate = (double)common / (double)orig_len;
    return CK_OK;
}
