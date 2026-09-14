#include "fileio.h"
#include "check.h"

#include <stdio.h>
#include <stdlib.h>

CkStatus fileio_read_all(const char *path, unsigned char **out, size_t *out_len)
{
    FILE *fp = NULL;
    long size = 0;
    unsigned char *buf = NULL;
    size_t got = 0;

    fp = fopen(path, "rb");
    if (fp == NULL) {
        fprintf(stderr, "错误：无法打开文件 %s\n", path);
        return CK_ERR_OPEN;
    }

    /* 先跳到末尾问一下文件多大 */
    if (fseek(fp, 0, SEEK_END) != 0) {
        fprintf(stderr, "错误：无法定位文件末尾 %s\n", path);
        fclose(fp);
        return CK_ERR_READ;
    }

    size = ftell(fp);
    if (size < 0) {
        fprintf(stderr, "错误：无法获取文件大小 %s\n", path);
        fclose(fp);
        return CK_ERR_READ;
    }

    if ((unsigned long)size > (unsigned long)CK_MAX_FILE_BYTES) {
        fprintf(stderr, "错误：文件 %s 过大（%ld 字节，上限 %lu 字节）\n",
                path, size, (unsigned long)CK_MAX_FILE_BYTES);
        fclose(fp);
        return CK_ERR_TOO_BIG;
    }

    if (fseek(fp, 0, SEEK_SET) != 0) {
        fprintf(stderr, "错误：无法回到文件开头 %s\n", path);
        fclose(fp);
        return CK_ERR_READ;
    }

    /* 多开 1 字节放 '\0'，空文件也能得到一个合法缓冲区 */
    buf = (unsigned char *)malloc((size_t)size + 1u);
    if (buf == NULL) {
        fprintf(stderr, "错误：内存分配失败（需要 %ld 字节）\n", size + 1);
        fclose(fp);
        return CK_ERR_MEM;
    }

    got = fread(buf, 1, (size_t)size, fp);
    if (got != (size_t)size) {
        fprintf(stderr, "错误：读取文件 %s 失败（预期 %ld 字节，实际 %lu 字节）\n",
                path, size, (unsigned long)got);
        free(buf);
        fclose(fp);
        return CK_ERR_READ;
    }

    buf[got] = '\0';
    fclose(fp);

    *out = buf;
    *out_len = got;
    return CK_OK;
}

CkStatus fileio_write_rate(const char *path, double rate)
{
    FILE *fp = NULL;

    /*
     * 用二进制模式写：文本模式在 Windows 上会把 '\n' 变成 "\r\n"，
     * 答案文件就会多出一个字节，这里要求输出严格是 "0.82\n"。
     */
    fp = fopen(path, "wb");
    if (fp == NULL) {
        fprintf(stderr, "错误：无法写入答案文件 %s\n", path);
        return CK_ERR_WRITE;
    }

    /* 小学算术：%.2f 会四舍五入到小数点后两位 */
    if (fprintf(fp, "%.2f\n", rate) < 0) {
        fprintf(stderr, "错误：写入答案文件 %s 失败\n", path);
        fclose(fp);
        return CK_ERR_WRITE;
    }

    /* fclose 才会真正把缓冲区刷盘，它的返回值不能忽略 */
    if (fclose(fp) != 0) {
        fprintf(stderr, "错误：关闭答案文件 %s 失败，数据可能未写入\n", path);
        return CK_ERR_WRITE;
    }

    return CK_OK;
}
