/*
 * common.h —— 全局错误码定义
 *
 * 本程序的所有失败路径都通过 CkStatus 统一上报，避免各处散落魔法数字。
 * 每个错误码对应博客「异常处理」一节中的一类场景。
 */
#ifndef COMMON_H
#define COMMON_H

typedef enum {
    CK_OK = 0,       /* 成功 */
    CK_ERR_ARG,      /* 命令行参数个数不正确 */
    CK_ERR_OPEN,     /* 文件不存在 / 无权限，fopen 失败 */
    CK_ERR_READ,     /* 打开成功但读取失败 */
    CK_ERR_WRITE,    /* 答案文件无法写入 */
    CK_ERR_MEM,      /* 内存分配失败 */
    CK_ERR_TOO_BIG   /* 文件超过程序可处理的上限 */
} CkStatus;

/* 把错误码翻译成给用户看的中文说明 */
const char *ck_status_text(CkStatus st);

#endif /* COMMON_H */
