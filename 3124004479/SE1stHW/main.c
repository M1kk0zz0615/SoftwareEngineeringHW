/*
 * main.c —— 命令行入口
 *
 * 用法：
 *   main.exe <原文文件> <抄袭版论文文件> <答案文件>
 *
 * 本文件只负责「串流程」和「兜错误」，所有算法都在 check.c，
 * 所有文件操作都在 fileio.c。
 */
#include "check.h"
#include "fileio.h"

#include <stdio.h>
#include <stdlib.h>

/* 各错误场景对应的进程退出码，方便测试脚本区分失败原因 */
enum {
    EXIT_OK          = 0,
    EXIT_BAD_USAGE   = 2,
    EXIT_READ_ORIG   = 3,
    EXIT_READ_COPY   = 4,
    EXIT_OUT_OF_MEM  = 5,
    EXIT_COMPUTE     = 6,
    EXIT_WRITE_ANS   = 7
};

static void print_usage(const char *prog)
{
    fprintf(stderr,
            "用法：%s <原文文件> <抄袭版论文文件> <答案文件>\n"
            "示例：%s C:\\tests\\orig.txt C:\\tests\\orig_add.txt C:\\tests\\ans.txt\n",
            prog, prog);
}

int main(int argc, char *argv[])
{
    unsigned char *orig_raw = NULL;
    unsigned char *copy_raw = NULL;
    unsigned char *orig = NULL;
    unsigned char *copy = NULL;
    size_t orig_raw_len = 0;
    size_t copy_raw_len = 0;
    size_t orig_len = 0;
    size_t copy_len = 0;
    CheckResult result;
    CkStatus st = CK_OK;
    int code = EXIT_OK;

    /* 0. 参数个数：程序名 + 三个路径 */
    if (argc != 4) {
        print_usage(argv[0]);
        return EXIT_BAD_USAGE;
    }

    /* 1. 读原文 */
    st = fileio_read_all(argv[1], &orig_raw, &orig_raw_len);
    if (st != CK_OK) {
        code = EXIT_READ_ORIG;
        goto cleanup;
    }

    /* 2. 读抄袭版 */
    st = fileio_read_all(argv[2], &copy_raw, &copy_raw_len);
    if (st != CK_OK) {
        code = EXIT_READ_COPY;
        goto cleanup;
    }

    /* 3. 去空白。分配失败要单独报，别和算法失败混在一起 */
    orig = (unsigned char *)malloc(orig_raw_len + 1u);
    copy = (unsigned char *)malloc(copy_raw_len + 1u);
    if (orig == NULL || copy == NULL) {
        fprintf(stderr, "错误：内存分配失败\n");
        code = EXIT_OUT_OF_MEM;
        goto cleanup;
    }

    orig_len = check_strip_blank(orig_raw, orig_raw_len, orig);
    copy_len = check_strip_blank(copy_raw, copy_raw_len, copy);
    orig[orig_len] = '\0';
    copy[copy_len] = '\0';

    /* 4. 算重复率 */
    st = check_similarity(orig, orig_len, copy, copy_len, &result);
    if (st != CK_OK) {
        fprintf(stderr, "错误：%s\n", ck_status_text(st));
        code = EXIT_COMPUTE;
        goto cleanup;
    }

    /* 5. 写答案 */
    st = fileio_write_rate(argv[3], result.rate);
    if (st != CK_OK) {
        code = EXIT_WRITE_ANS;
        goto cleanup;
    }

cleanup:
    /* 所有出口都汇聚到这里，保证不泄漏 */
    free(orig_raw);
    free(copy_raw);
    free(orig);
    free(copy);
    return code;
}
