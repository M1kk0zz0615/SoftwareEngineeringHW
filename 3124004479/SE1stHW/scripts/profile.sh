#!/usr/bin/env bash
#
# 用 gprof 做性能分析（对应作业里「Studio Profiling Tools」那一节）。
#
# 说明：本机没装 Visual Studio 2017，用 MinGW 自带的 gprof 达到同样目的，
# 它同样能给出「每个函数自身耗时占比」的 flat profile。
#
# 为了回答「瓶颈在哪、改进了多少」，这里生成两份 profile：
#   ① 优化前：把 check_similarity 里的 check_lcs 换成经典动态规划 check_lcs_dp，
#      复现第一版程序的算法，看出瓶颈。
#   ② 优化后：当前仓库里的真实代码。
#
# 用法： bash scripts/profile.sh [样例目录]
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
SRC_DIR="${REPO_ROOT}/3124004479"
OUT_DIR="${REPO_ROOT}/docs/profiling"
SAMPLE_DIR="${1:-E:/!作业/软件工程/测试文本}"

WORK="$(cygpath -m "${TMPDIR:-/tmp}")/se_prof_work"
rm -rf "${WORK}"
mkdir -p "${WORK}/tests" "${WORK}/before" "${OUT_DIR}"

cp "${SRC_DIR}"/*.c "${SRC_DIR}"/*.h "${WORK}/"
cp "${SRC_DIR}"/tests/bench.c "${WORK}/tests/"

cd "${WORK}"

ORIG="${SAMPLE_DIR}/orig.txt"
COPY="${SAMPLE_DIR}/orig_0.8_del.txt"

if [ ! -f "${ORIG}" ] || [ ! -f "${COPY}" ]; then
    echo "找不到样例文件，请把样例目录作为第一个参数传进来" >&2
    exit 1
fi

# ---------------------------------------------------------------- 优化前
echo "==> 1/4 构造「优化前」的版本（LCS 改用经典动态规划）"
cp ./*.c ./*.h before/
# 把 check_similarity 里对 check_lcs 的调用换成 check_lcs_dp，
# 这样这份代码的算法与第一版完全一致。
sed -i 's/size_t mid = check_lcs(pa, ma, pb, nb);/size_t mid = check_lcs_dp(pa, ma, pb, nb);/' before/check.c
grep -q "check_lcs_dp(pa, ma, pb, nb)" before/check.c || {
    echo "替换失败，源码结构可能已改动" >&2; exit 1; }

cd before
gcc -pg -O2 -std=c11 -o main_before.exe main.c check.c fileio.c common.c
rm -f gmon.out
./main_before.exe "${ORIG}" "${COPY}" ans_before.txt >/dev/null
echo "    优化前答案: $(cat ans_before.txt)"
gprof -b main_before.exe gmon.out > "${OUT_DIR}/flat_profile_before.txt" 2>&1
cd ..

# ---------------------------------------------------------------- 优化后
echo "==> 2/4 采集「优化后」的 profile"
gcc -pg -O2 -std=c11 -o main_after.exe main.c check.c fileio.c common.c
rm -f gmon.out
./main_after.exe "${ORIG}" "${COPY}" ans_after.txt >/dev/null
echo "    优化后答案: $(cat ans_after.txt)"
gprof -b main_after.exe gmon.out > "${OUT_DIR}/flat_profile_after.txt" 2>&1

echo "==> 3/4 对照两套算法的绝对耗时"
gcc -pg -O2 -std=c11 -o bench_prof.exe tests/bench.c check.c fileio.c common.c
rm -f gmon.out
./bench_prof.exe "${ORIG}" "${COPY}" 1 > bench_output.txt
gprof -b bench_prof.exe gmon.out > "${OUT_DIR}/flat_profile_dp_vs_bitset.txt" 2>&1

echo "==> 4/4 汇总"
{
    echo "############ 优化前（经典动态规划 LCS）flat profile ############"
    cat "${OUT_DIR}/flat_profile_before.txt"
    echo
    echo "############ 优化后（位并行 LCS）flat profile ############"
    cat "${OUT_DIR}/flat_profile_after.txt"
    echo
    echo "############ 两套算法绝对耗时对照 ############"
    cat bench_output.txt
} > "${OUT_DIR}/run_log.txt"

echo
echo "报告已写入 ${OUT_DIR}"
echo "----- 优化前 [前 8 行] -----"
sed -n '6,14p' "${OUT_DIR}/flat_profile_before.txt"
echo "----- 优化后 [前 8 行] -----"
sed -n '6,14p' "${OUT_DIR}/flat_profile_after.txt"
