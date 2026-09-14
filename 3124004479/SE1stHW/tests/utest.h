/*
 * utest.h —— 一个极简的单元测试框架
 *
 * 刻意不引入第三方依赖：评测环境只要能跑 gcc 就能跑测试。
 * 计数器的定义放在 main_test.c 里，这里只做声明，避免多文件重复定义。
 */
#ifndef UTEST_H
#define UTEST_H

#include <stddef.h>
#include <stdio.h>

extern int utest_checks;          /* 断言总条数 */
extern int utest_failures;        /* 失败条数 */
extern const char *utest_case;    /* 当前用例名 */

/* 开始一个测试用例 */
#define UT_BEGIN(name)                     \
    do {                                   \
        utest_case = (name);               \
        printf("  [用例] %s\n", (name));   \
    } while (0)

/* 断言条件为真 */
#define UT_CHECK(cond)                                                     \
    do {                                                                   \
        utest_checks++;                                                    \
        if (!(cond)) {                                                     \
            utest_failures++;                                              \
            printf("      [失败] %s:%d  条件不成立: %s\n",                 \
                   __FILE__, __LINE__, #cond);                             \
        }                                                                  \
    } while (0)

/* 断言两个 size_t 相等 */
#define UT_CHECK_SIZE(actual, expected)                                    \
    do {                                                                   \
        unsigned long long a_ = (unsigned long long)(actual);              \
        unsigned long long e_ = (unsigned long long)(expected);            \
        utest_checks++;                                                    \
        if (a_ != e_) {                                                    \
            utest_failures++;                                              \
            printf("      [失败] %s:%d  期望 %llu，实际 %llu\n",           \
                   __FILE__, __LINE__, e_, a_);                            \
        }                                                                  \
    } while (0)

/* 断言浮点数在容差范围内相等 */
#define UT_CHECK_NEAR(actual, expected, eps)                               \
    do {                                                                   \
        double a_ = (double)(actual);                                      \
        double e_ = (double)(expected);                                    \
        double d_ = a_ - e_;                                               \
        utest_checks++;                                                    \
        if (d_ < 0.0) {                                                    \
            d_ = -d_;                                                      \
        }                                                                  \
        if (!(d_ <= (double)(eps))) {                                      \
            utest_failures++;                                              \
            printf("      [失败] %s:%d  期望 %.4f(±%.4f)，实际 %.4f\n",    \
                   __FILE__, __LINE__, e_, (double)(eps), a_);             \
        }                                                                  \
    } while (0)

/* 各测试文件的入口 */
void run_check_tests(void);
void run_fileio_tests(void);
void run_common_tests(void);

#endif /* UTEST_H */
