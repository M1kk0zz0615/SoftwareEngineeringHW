#include "common.h"

const char *ck_status_text(CkStatus st)
{
    switch (st) {
    case CK_OK:          return "成功";
    case CK_ERR_ARG:     return "命令行参数个数不正确";
    case CK_ERR_OPEN:    return "无法打开文件";
    case CK_ERR_READ:    return "文件读取失败";
    case CK_ERR_WRITE:   return "答案文件写入失败";
    case CK_ERR_MEM:     return "内存分配失败";
    case CK_ERR_TOO_BIG: return "文件过大，超出程序处理上限";
    default:             return "未知错误";
    }
}
