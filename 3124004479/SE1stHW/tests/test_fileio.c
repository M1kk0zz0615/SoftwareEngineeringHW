/*
 * test_fileio.c —— 文件读写模块 + 异常处理单元测试
 *
 * 每个用例对应博客「异常处理说明」里的一个场景。
 */
#include "utest.h"
#include "../fileio.h"
#include "../check.h"   /* 只为拿到 CK_MAX_FILE_BYTES */

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#define TMP_IN   "utest_tmp_in.txt"
#define TMP_OUT  "utest_tmp_out.txt"

/* 往指定路径写一段内容，供测试造数据 */
static int make_file(const char *path, const char *content, size_t len)
{
    FILE *fp = fopen(path, "wb");
    size_t n = 0;

    if (fp == NULL) {
        return 0;
    }
    n = fwrite(content, 1, len, fp);
    fclose(fp);
    return (n == len) ? 1 : 0;
}

/* 读一个文本文件的全部内容，供断言 */
static long read_text(const char *path, char *buf, size_t cap)
{
    FILE *fp = fopen(path, "rb");
    size_t n = 0;

    if (fp == NULL) {
        return -1;
    }
    n = fread(buf, 1, cap - 1u, fp);
    fclose(fp);
    buf[n] = '\0';
    return (long)n;
}

void run_fileio_tests(void)
{
    remove(TMP_IN);
    remove(TMP_OUT);

    /* --- 用例 21：正常读取 --- */
    UT_BEGIN("用例21 正常读取文件内容与长度");
    {
        unsigned char *buf = NULL;
        size_t len = 0;
        CkStatus st = CK_OK;

        UT_CHECK(make_file(TMP_IN, "hello 查重", 12) != 0);
        st = fileio_read_all(TMP_IN, &buf, &len);

        UT_CHECK(st == CK_OK);
        UT_CHECK_SIZE(len, 12u);
        UT_CHECK(buf != NULL && memcmp(buf, "hello 查重", 12) == 0);
        UT_CHECK(buf != NULL && buf[len] == '\0');   /* 末尾补 '\0' */
        free(buf);
    }

    /* --- 用例 22：文件不存在 --- */
    UT_BEGIN("用例22 输入文件不存在 -> CK_ERR_OPEN");
    {
        unsigned char *buf = NULL;
        size_t len = 0;
        CkStatus st = fileio_read_all("这个文件根本不存在_98765.txt", &buf, &len);

        UT_CHECK(st == CK_ERR_OPEN);
        UT_CHECK(buf == NULL);
    }

    /* --- 用例 23：空文件不算错误 --- */
    UT_BEGIN("用例23 空文件 -> 读取成功且长度为 0");
    {
        unsigned char *buf = NULL;
        size_t len = 123u;
        CkStatus st = CK_OK;

        UT_CHECK(make_file(TMP_IN, "", 0) != 0);
        st = fileio_read_all(TMP_IN, &buf, &len);

        UT_CHECK(st == CK_OK);
        UT_CHECK_SIZE(len, 0u);
        UT_CHECK(buf != NULL && buf[0] == '\0');
        free(buf);
    }

    /* --- 用例 24：写入重复率，格式精确到两位小数 --- */
    UT_BEGIN("用例24 写出 0.82 的答案文件内容为 \"0.82\\n\"");
    {
        char text[64];
        CkStatus st = fileio_write_rate(TMP_OUT, 0.8181);

        UT_CHECK(st == CK_OK);
        UT_CHECK(read_text(TMP_OUT, text, sizeof(text)) == 5);
        UT_CHECK(strcmp(text, "0.82\n") == 0);
    }

    /* --- 用例 25：边界值 0.00 与 1.00 --- */
    UT_BEGIN("用例25 边界值 0.00 / 1.00 的格式化");
    {
        char text[64];

        UT_CHECK(fileio_write_rate(TMP_OUT, 0.0) == CK_OK);
        UT_CHECK(read_text(TMP_OUT, text, sizeof(text)) > 0);
        UT_CHECK(strcmp(text, "0.00\n") == 0);

        UT_CHECK(fileio_write_rate(TMP_OUT, 1.0) == CK_OK);
        UT_CHECK(read_text(TMP_OUT, text, sizeof(text)) > 0);
        UT_CHECK(strcmp(text, "1.00\n") == 0);

        /* 0.999 应四舍五入到 1.00 而不是 0.99 */
        UT_CHECK(fileio_write_rate(TMP_OUT, 0.999) == CK_OK);
        UT_CHECK(read_text(TMP_OUT, text, sizeof(text)) > 0);
        UT_CHECK(strcmp(text, "1.00\n") == 0);
    }

    /* --- 用例 26：答案文件路径所在目录不存在 --- */
    UT_BEGIN("用例26 答案文件路径非法 -> CK_ERR_WRITE");
    {
        CkStatus st = fileio_write_rate("不存在的目录_zzz/ans.txt", 0.5);

        UT_CHECK(st == CK_ERR_WRITE);
    }

    /* --- 用例 27：把目录当成文件读 --- */
    UT_BEGIN("用例27 输入路径是目录 -> 读取失败");
    {
        unsigned char *buf = NULL;
        size_t len = 0;
        CkStatus st = fileio_read_all(".", &buf, &len);

        UT_CHECK(st != CK_OK);      /* Windows 上 fopen 目录会失败 */
    }

    /* --- 用例 28：读取含 0x00 的二进制内容不截断 --- */
    UT_BEGIN("用例28 文件中间含 \\0 也能按字节数完整读入");
    {
        const char raw[] = {'a', '\0', 'b', '\0', 'c'};
        unsigned char *buf = NULL;
        size_t len = 0;
        CkStatus st = CK_OK;

        UT_CHECK(make_file(TMP_IN, raw, sizeof(raw)) != 0);
        st = fileio_read_all(TMP_IN, &buf, &len);

        UT_CHECK(st == CK_OK);
        UT_CHECK_SIZE(len, 5u);
        UT_CHECK(buf != NULL && memcmp(buf, raw, 5) == 0);
        free(buf);
    }

    /* --- 用例 32：超过大小上限的文件被拒绝 --- */
    UT_BEGIN("用例32 超过 16MB 上限的文件 -> CK_ERR_TOO_BIG");
    {
        unsigned char *buf = NULL;
        size_t len = 0;
        CkStatus st = CK_OK;
        FILE *fp = fopen(TMP_IN, "wb");

        UT_CHECK(fp != NULL);
        if (fp != NULL) {
            /*
             * 用「定位到上限之后写一个字节」造一个大文件，
             * 避免真的往磁盘上刷 16MB 数据，测试跑得快。
             */
            UT_CHECK(fseek(fp, (long)CK_MAX_FILE_BYTES, SEEK_SET) == 0);
            UT_CHECK(fputc('\0', fp) != EOF);
            UT_CHECK(fclose(fp) == 0);

            st = fileio_read_all(TMP_IN, &buf, &len);
            UT_CHECK(st == CK_ERR_TOO_BIG);
            UT_CHECK(buf == NULL);
        }
    }

    remove(TMP_IN);
    remove(TMP_OUT);
}
