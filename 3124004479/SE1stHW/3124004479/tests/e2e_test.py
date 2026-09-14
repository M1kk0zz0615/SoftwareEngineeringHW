#!/usr/bin/env python3
"""端到端测试：按评测方式调用命令行程序。

评测方会这样调用：
    main.exe <原文文件> <抄袭版文件> <答案文件>

本脚本把所有失败路径也跑一遍，确认程序不会崩溃、不会写多余文件、
并且能在 5 秒内给出答案（这是作业明确的扣分项）。

用法：
    python e2e_test.py [main.exe 路径] [样例目录]
"""
import os
import re
import subprocess
import sys
import tempfile
import time

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_EXE = os.path.join(HERE, "..", "main.exe")
# 样例目录：包含 orig.txt 与 orig_0.8_add.txt
DEFAULT_SAMPLE = r"E:\!作业\软件工程\测试文本"

RATE_PATTERN = re.compile(r"^\d\.\d{2}\n?$")

_checks = 0
_failures = 0


def check(cond: bool, msg: str) -> None:
    global _checks, _failures
    _checks += 1
    if not cond:
        _failures += 1
        print(f"      [失败] {msg}")


def case(name: str) -> None:
    print(f"  [用例] {name}")


def run(exe: str, args: list[str], timeout: float = 20.0):
    """跑一次程序，返回 (退出码, stdout, stderr, 耗时秒)。"""
    t0 = time.perf_counter()
    try:
        proc = subprocess.run(
            [exe, *args],
            capture_output=True,
            timeout=timeout,
            cwd=os.path.dirname(os.path.abspath(exe)),
        )
        return proc.returncode, proc.stdout, proc.stderr, time.perf_counter() - t0
    except subprocess.TimeoutExpired:
        return -1, b"", b"timeout", time.perf_counter() - t0


def read_rate(path: str):
    if not os.path.exists(path):
        return None
    with open(path, "rb") as fp:
        return fp.read()


def main() -> int:
    exe = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_EXE
    sample = sys.argv[2] if len(sys.argv) > 2 else DEFAULT_SAMPLE

    exe = os.path.abspath(exe)
    if not os.path.exists(exe):
        print(f"找不到可执行文件：{exe}")
        return 2

    orig = os.path.join(sample, "orig.txt")
    add = os.path.join(sample, "orig_0.8_add.txt")

    print("================ 端到端测试 ================")
    print(f"被测程序：{exe}")
    print(f"样例目录：{sample}\n")

    with tempfile.TemporaryDirectory() as tmp:
        ans = os.path.join(tmp, "ans.txt")

        # --- 用例 01：正常调用，输出格式必须是两位小数 ---
        case("端到端01 正常调用，输出形如 0.82")
        if os.path.exists(orig) and os.path.exists(add):
            code, _, _, _ = run(exe, [orig, add, ans])
            data = read_rate(ans)
            check(code == 0, f"退出码应为 0，实际 {code}")
            check(data is not None, "答案文件未生成")
            if data is not None:
                text = data.decode("utf-8", "replace")
                check(RATE_PATTERN.match(text) is not None,
                      f"输出格式不是两位小数：{text!r}")
        else:
            print(f"      [跳过] 样例文件不存在：{orig}")

        # --- 用例 02：参数个数不足 ---
        case("端到端02 只给两个参数 -> 退出码 2，且不生成答案文件")
        missing = os.path.join(tmp, "should_not_exist.txt")
        code, _, err, _ = run(exe, [orig, add])
        check(code == 2, f"退出码应为 2，实际 {code}")
        check(not os.path.exists(missing), "不应生成任何文件")
        check(len(err) > 0, "应向 stderr 输出用法说明")

        # --- 用例 03：参数给多了 ---
        case("端到端03 参数过多 -> 退出码 2")
        code, _, _, _ = run(exe, [orig, add, ans, "多余参数"])
        check(code == 2, f"退出码应为 2，实际 {code}")

        # --- 用例 04：原文文件不存在 ---
        case("端到端04 原文文件不存在 -> 退出码 3")
        code, _, err, _ = run(exe, [os.path.join(tmp, "无此文件.txt"), add, ans])
        check(code == 3, f"退出码应为 3，实际 {code}")
        check(len(err) > 0, "应给出可读的错误说明")

        # --- 用例 05：抄袭版文件不存在 ---
        case("端到端05 抄袭版文件不存在 -> 退出码 4")
        code, _, _, _ = run(exe, [orig, os.path.join(tmp, "无此文件.txt"), ans])
        check(code == 4, f"退出码应为 4，实际 {code}")

        # --- 用例 06：答案路径所在目录不存在 ---
        case("端到端06 答案路径不可写 -> 退出码 7")
        code, _, _, _ = run(exe, [orig, add, os.path.join(tmp, "无此目录", "a.txt")])
        check(code == 7, f"退出码应为 7，实际 {code}")

        # --- 用例 07：空原文 -> 0.00，且不能除零崩溃 ---
        case("端到端07 原文为空 -> 输出 0.00 且不崩溃")
        empty = os.path.join(tmp, "empty.txt")
        with open(empty, "wb") as fp:
            fp.write(b"")
        code, _, _, _ = run(exe, [empty, add, ans])
        check(code == 0, f"退出码应为 0，实际 {code}")
        check(read_rate(ans) == b"0.00\n", f"应为 b'0.00\\n'，实际 {read_rate(ans)!r}")

        # --- 用例 08：完全相同的两篇 -> 1.00 ---
        case("端到端08 两篇完全相同 -> 1.00")
        code, _, _, _ = run(exe, [orig, orig, ans])
        check(code == 0, f"退出码应为 0，实际 {code}")
        check(read_rate(ans) == b"1.00\n", f"应为 b'1.00\\n'，实际 {read_rate(ans)!r}")

        # --- 用例 09：性能红线，必须在 5 秒内出答案 ---
        case("端到端09 最大样例必须在 5 秒内完成")
        del_file = os.path.join(sample, "orig_0.8_del.txt")
        if os.path.exists(del_file):
            code, _, _, elapsed = run(exe, [orig, del_file, ans])
            check(code == 0, f"退出码应为 0，实际 {code}")
            check(elapsed < 5.0, f"耗时 {elapsed:.2f}s，超过 5 秒红线")
            print(f"      实际耗时 {elapsed:.2f}s，输出 {read_rate(ans)!r}")
        else:
            print(f"      [跳过] 找不到 {del_file}")

        # --- 用例 10：只允许写答案文件 ---
        case("端到端10 不产生多余的输出文件")
        before = set(os.listdir(tmp))
        run(exe, [orig, add, ans])
        after = set(os.listdir(tmp))
        check(after - before <= {"ans.txt"}, f"多出了文件：{after - before}")

    print("\n===========================================")
    print(f"断言总数: {_checks}    失败: {_failures}")
    print("结果: 全部通过" if _failures == 0 else "结果: 存在失败用例")
    return 0 if _failures == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
