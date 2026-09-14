/*
 * test_common.c —— 错误码文案单元测试
 *
 * ck_status_text 平时只在出错路径才会执行，靠正常流程跑不到，
 * 所以必须单独测一遍，否则覆盖率上会留下 0% 的空白。
 */
#include "utest.h"
#include "../common.h"

#include <string.h>

void run_common_tests(void)
{
    /* --- 用例 29：每个已定义错误码都要有非空中文说明 --- */
    UT_BEGIN("用例29 ck_status_text 覆盖全部错误码");
    {
        static const CkStatus all[] = {
            CK_OK, CK_ERR_ARG, CK_ERR_OPEN, CK_ERR_READ,
            CK_ERR_WRITE, CK_ERR_MEM, CK_ERR_TOO_BIG
        };
        size_t i = 0;

        for (i = 0; i < sizeof(all) / sizeof(all[0]); i++) {
            const char *text = ck_status_text(all[i]);

            UT_CHECK(text != NULL);
            UT_CHECK(text != NULL && text[0] != '\0');
        }

        /* 具体文案也钉住，防止以后被误改 */
        UT_CHECK(strcmp(ck_status_text(CK_ERR_OPEN), "无法打开文件") == 0);
        UT_CHECK(strcmp(ck_status_text(CK_ERR_MEM), "内存分配失败") == 0);
    }

    /* --- 用例 30：未定义的错误码走 default 分支 --- */
    UT_BEGIN("用例30 未定义错误码返回「未知错误」");
    {
        const char *text = ck_status_text((CkStatus)9999);

        UT_CHECK(text != NULL);
        UT_CHECK(strcmp(text, "未知错误") == 0);
    }

    /* --- 用例 31：CK_OK 的文案 --- */
    UT_BEGIN("用例31 CK_OK 文案为「成功」");
    UT_CHECK(strcmp(ck_status_text(CK_OK), "成功") == 0);
}
