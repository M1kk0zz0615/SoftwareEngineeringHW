/*
 * check.h —— 计算模块（查重核心算法）
 *
 * 对外只暴露「长度 → 长度」的纯函数，不碰任何文件，方便单元测试。
 */
#ifndef CHECK_H
#define CHECK_H

#include <stddef.h>
#include "common.h"

/* 单个输入文件允许的最大字节数，防止恶意/异常输入把内存打爆 */
#define CK_MAX_FILE_BYTES ((size_t)16 * 1024 * 1024)

/*
 * 复现率计算的完整结果。
 * 把中间量一起带出来，单元测试就能直接断言 LCS 长度而不必反推。
 */
typedef struct {
    double rate;          /* 重复率，范围 [0, 1] */
    size_t orig_len;      /* 原文去空白后的字符数（分母） */
    size_t copy_len;      /* 抄袭版去空白后的字符数 */
    size_t common_len;    /* 去掉公共前后缀后，再求得的 LCS 长度（含前后缀） */
} CheckResult;

/* 判断一个字节是否为空白字符 */
int check_is_blank(unsigned char c);

/*
 * 去掉 src 中的空白字符并写入 dst。
 * dst 必须至少有 len 字节可用空间；返回写入的长度。
 * 允许 dst == src（原地过滤）。
 */
size_t check_strip_blank(const unsigned char *src, size_t len, unsigned char *dst);

/*
 * 最长公共子序列长度——位并行实现，生产环境使用。
 * 时间复杂度 O(m*n/64)，空间 O(m/64)（m 取较短串，减少位向量长度）。
 * 分配失败时返回 SIZE_MAX。
 */
size_t check_lcs(const unsigned char *a, size_t m, const unsigned char *b, size_t n);

/*
 * 最长公共子序列长度——经典动态规划实现，仅在单元测试中作对照答案。
 * 时间复杂度 O(m*n)，空间 O(min(m,n))。分配失败时返回 SIZE_MAX。
 */
size_t check_lcs_dp(const unsigned char *a, size_t m, const unsigned char *b, size_t n);

/*
 * 计算两段（已去空白）文本的重复率。
 * 口径：重复率 = LCS(原文, 抄袭版) / 原文长度。
 * 原文为空时约定重复率为 0，从而避免除零。
 */
CkStatus check_similarity(const unsigned char *orig, size_t orig_len,
                          const unsigned char *copy, size_t copy_len,
                          CheckResult *result);

#endif /* CHECK_H */
