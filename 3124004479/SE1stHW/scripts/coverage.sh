#!/usr/bin/env bash
#
# 生成单元测试覆盖率报告（gcov）。
#
# 为什么要在临时目录里编译：
#   MinGW 的 gcov 运行时无法正确处理含非 ASCII（中文）字符的路径，
#   .gcda 数据文件根本写不出来。所以先把源码复制到纯 ASCII 路径下再跑。
#
# 为什么用 cygpath 转成 Windows 风格路径：
#   脚本里跑的 e2e_test.py 是 Windows 版 Python，它不认识 /tmp 这种 MSYS 路径。
#
# 用法： bash scripts/coverage.sh [样例目录]
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
SRC_DIR="${REPO_ROOT}/3124004479"
OUT_DIR="${REPO_ROOT}/docs/coverage"
SAMPLE_DIR="${1:-E:/!作业/软件工程/测试文本}"

# 纯 ASCII 的临时工作目录
WORK="$(cygpath -m "${TMPDIR:-/tmp}")/se_cov_work"

# 找一个真正能用的 Python。
# 注意：Windows 上 `python` 常常是应用商店的占位壳，命令存在但执行就报错，
# 所以这里必须真的跑一次 --version 来验证，不能只看 command -v。
PYTHON=""
for cand in "py -3" "python3" "python"; do
    if ${cand} --version >/dev/null 2>&1; then
        PYTHON="${cand}"
        break
    fi
done
if [ -z "${PYTHON}" ]; then
    echo "找不到可用的 Python，将跳过端到端测试" >&2
else
    echo "使用 Python 解释器：${PYTHON}"
fi

rm -rf "${WORK}"
mkdir -p "${WORK}/tests" "${OUT_DIR}"

cp "${SRC_DIR}"/*.c "${SRC_DIR}"/*.h "${WORK}/"
cp "${SRC_DIR}"/tests/*.h "${WORK}/tests/"
cp "${SRC_DIR}"/tests/main_test.c "${SRC_DIR}"/tests/test_check.c \
   "${SRC_DIR}"/tests/test_fileio.c "${SRC_DIR}"/tests/test_common.c \
   "${SRC_DIR}"/tests/e2e_test.py "${WORK}/tests/"

cd "${WORK}"

echo "==> 1/4 编译带覆盖率插桩的版本（工作目录 ${WORK}）"
gcc --coverage -O0 -std=c11 -c check.c fileio.c common.c main.c
gcc --coverage -O0 -std=c11 -o cov_test.exe \
    tests/main_test.c tests/test_check.c tests/test_fileio.c tests/test_common.c \
    check.o fileio.o common.o
gcc --coverage -O0 -std=c11 -o cov_main.exe main.o check.o fileio.o common.o

echo "==> 2/4 跑单元测试"
./cov_test.exe > unittest_output.txt 2>&1 || true
tail -3 unittest_output.txt

echo "==> 3/4 跑端到端测试（用于覆盖 main.c）"
if [ -n "${PYTHON}" ] && [ -d "${SAMPLE_DIR}" ]; then
    PYTHONIOENCODING=utf-8 ${PYTHON} tests/e2e_test.py "${WORK}/cov_main.exe" \
        "${SAMPLE_DIR}" > e2e_output.txt 2>&1 || true
    tail -3 e2e_output.txt
else
    echo "    [跳过] 缺少 Python 或样例目录不存在：${SAMPLE_DIR}"
    : > e2e_output.txt
fi

echo "==> 4/4 生成 gcov 报告"
gcov -b -c check.c fileio.c common.c main.c > "${OUT_DIR}/summary.txt" 2>&1
cp ./*.gcov "${OUT_DIR}/"
${PYTHON} "${SCRIPT_DIR}/gcov_html.py" "${OUT_DIR}" 2>/dev/null || echo "  (跳过 HTML 生成)"

{
    echo "覆盖率的编译与运行均在临时目录 ${WORK} 中完成，"
    echo "源码原样复制自 ${SRC_DIR}，因此行号与本仓库源码一一对应。"
    echo
    echo "---- 单元测试输出 ----"
    cat unittest_output.txt
    echo
    echo "---- 端到端测试输出 ----"
    cat e2e_output.txt
} > "${OUT_DIR}/run_log.txt"

echo
echo "报告已写入 ${OUT_DIR}"
grep -E "^File '|^Lines executed" "${OUT_DIR}/summary.txt"
