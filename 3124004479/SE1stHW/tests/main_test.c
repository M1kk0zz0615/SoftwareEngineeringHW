/* 单元测试入口：汇总各测试文件的断言结果 */
#include "utest.h"

int utest_checks = 0;
int utest_failures = 0;
const char *utest_case = "";

int main(void)
{
    printf("================ 单元测试 ================\n");

    printf("\n--- 计算模块 check.c ---\n");
    run_check_tests();

    printf("\n--- 文件模块 fileio.c ---\n");
    run_fileio_tests();

    printf("\n--- 错误码模块 common.c ---\n");
    run_common_tests();

    printf("\n=========================================\n");
    printf("断言总数: %d    失败: %d\n", utest_checks, utest_failures);
    if (utest_failures == 0) {
        printf("结果: 全部通过\n");
        return 0;
    }
    printf("结果: 存在失败用例\n");
    return 1;
}
