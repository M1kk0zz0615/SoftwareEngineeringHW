/*
 * bench.c —— 计算模块性能对比工具
 *
 * 对同一对输入文件，分别用两套算法求 LCS 并计时：
 *   A. check_lcs_dp  —— 经典动态规划，O(m*n)，优化前的版本
 *   B. check_lcs     —— 位并行实现，O(m*n/64)，优化后的版本
 *
 * 用法： bench.exe <原文文件> <抄袭版文件> [重复次数]
 */
#include "../check.h"
#include "../fileio.h"

#include <stdio.h>
#include <stdlib.h>
#include <time.h>

static double now_sec(void)
{
    return (double)clock() / (double)CLOCKS_PER_SEC;
}

int main(int argc, char **argv)
{
    unsigned char *orig_raw = NULL;
    unsigned char *copy_raw = NULL;
    unsigned char *orig = NULL;
    unsigned char *copy = NULL;
    size_t orig_raw_len = 0;
    size_t copy_raw_len = 0;
    size_t orig_len = 0;
    size_t copy_len = 0;
    size_t mid_a = 0;
    size_t mid_b = 0;
    size_t lcs_dp = 0;
    size_t lcs_bit = 0;
    int repeat = 1;
    int i = 0;
    double t0 = 0.0;
    double t_dp = 0.0;
    double t_bit = 0.0;

    if (argc < 3) {
        fprintf(stderr, "用法：%s <原文文件> <抄袭版文件> [重复次数]\n", argv[0]);
        return 2;
    }
    if (argc >= 4) {
        repeat = atoi(argv[3]);
        if (repeat < 1) {
            repeat = 1;
        }
    }

    if (fileio_read_all(argv[1], &orig_raw, &orig_raw_len) != CK_OK) {
        return 3;
    }
    if (fileio_read_all(argv[2], &copy_raw, &copy_raw_len) != CK_OK) {
        free(orig_raw);
        return 3;
    }

    orig = (unsigned char *)malloc(orig_raw_len + 1u);
    copy = (unsigned char *)malloc(copy_raw_len + 1u);
    if (orig == NULL || copy == NULL) {
        free(orig_raw);
        free(copy_raw);
        free(orig);
        free(copy);
        return 4;
    }

    orig_len = check_strip_blank(orig_raw, orig_raw_len, orig);
    copy_len = check_strip_blank(copy_raw, copy_raw_len, copy);
    free(orig_raw);
    free(copy_raw);

    /* 为了公平对比，两个算法都直接作用在完整串上（不裁剪前后缀） */
    mid_a = orig_len;
    mid_b = copy_len;
    if (mid_a > mid_b) {
        unsigned char *tc = orig;
        size_t tl = mid_a;
        orig = copy;
        mid_a = mid_b;
        copy = tc;
        mid_b = tl;
    }

    printf("原文长度(去空白) = %llu 字节\n", (unsigned long long)orig_len);
    printf("抄袭版长度(去空白) = %llu 字节\n", (unsigned long long)copy_len);
    printf("待比较规模 = %llu x %llu = %.2e\n",
           (unsigned long long)mid_a, (unsigned long long)mid_b,
           (double)mid_a * (double)mid_b);
    printf("重复次数 = %d\n\n", repeat);

    t0 = now_sec();
    for (i = 0; i < repeat; i++) {
        lcs_dp = check_lcs_dp(orig, mid_a, copy, mid_b);
    }
    t_dp = (now_sec() - t0) / (double)repeat;

    t0 = now_sec();
    for (i = 0; i < repeat; i++) {
        lcs_bit = check_lcs(orig, mid_a, copy, mid_b);
    }
    t_bit = (now_sec() - t0) / (double)repeat;

    printf("A. 经典动态规划 O(m*n)     : LCS=%-8llu 耗时 %8.3f s\n",
           (unsigned long long)lcs_dp, t_dp);
    printf("B. 位并行       O(m*n/64)  : LCS=%-8llu 耗时 %8.3f s\n",
           (unsigned long long)lcs_bit, t_bit);

    if (t_bit > 0.0) {
        printf("\n加速比 = %.1f 倍\n", t_dp / t_bit);
    }
    printf("两个算法结果%s\n", (lcs_dp == lcs_bit) ? "一致（正确性校验通过）" : "不一致（有 bug！）");

    free(orig);
    free(copy);
    return (lcs_dp == lcs_bit) ? 0 : 1;
}
