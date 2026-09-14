/*
 * fileio.h —— 文件读写模块
 *
 * 全程序只在这里碰文件系统，且只碰命令行传来的那三个路径，
 * 不读取任何其他文件，也不访问网络。
 */
#ifndef FILEIO_H
#define FILEIO_H

#include <stddef.h>
#include "common.h"

/*
 * 一次性读入整个文件。
 * 成功时 *out 指向堆缓冲区（以 '\0' 结尾，调用方负责 free），*out_len 为字节数。
 * 失败时返回错误码，并已向 stderr 打印可读的原因。
 */
CkStatus fileio_read_all(const char *path, unsigned char **out, size_t *out_len);

/* 按 "%.2f\n" 的格式把重复率写入答案文件 */
CkStatus fileio_write_rate(const char *path, double rate);

#endif /* FILEIO_H */
