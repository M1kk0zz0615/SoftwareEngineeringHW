# 论文查重（软件工程 个人项目）

> 学号：3124004479　姓名：刘俊宁
> 课程：[计科24级78班 - 软件工程](https://edu.cnblogs.com/campus/gdgy/Class78-Grade2024-CS/)

给定一份论文原文和一份在原文基础上增删改过的抄袭版论文，计算并输出两者的**重复率**，
结果精确到小数点后两位。

## 一、怎么用

```
main.exe <原文文件> <抄袭版论文文件> <答案文件>
```

例：

```
main.exe C:\tests\orig.txt C:\tests\orig_0.8_add.txt C:\tests\ans.txt
```

程序读取前两个文件，把重复率写进第三个文件（内容是形如 `0.82` 的浮点数）。

- 输入输出全部走文件，不读也不写命令行指定的这三个文件之外的任何文件
- 全程不访问网络
- 路径中不含空格

## 二、目录结构

```
3124004479/                 学号目录，即本项目的全部源码
├── main.c                  命令行入口：串流程、兜错误
├── check.h / check.c       计算模块：文本预处理 + 最长公共子序列 + 重复率
├── fileio.h / fileio.c     文件模块：全程序唯一碰文件系统的地方
├── common.h / common.c     错误码定义与文案
├── Makefile                构建脚本
├── main.exe                编译产物（同时发布到 Releases）
└── tests/
    ├── utest.h             极简单元测试框架（零第三方依赖）
    ├── main_test.c         单元测试入口
    ├── test_check.c        计算模块测试（20 个用例）
    ├── test_fileio.c       文件模块与异常处理测试（12 个用例）
    ├── test_common.c       错误码文案测试（3 个用例）
    ├── e2e_test.py         端到端测试（10 个用例，按评测方式调用命令行）
    └── bench.c             性能对照工具（动态规划 vs 位并行）

scripts/
├── coverage.sh             生成 gcov 覆盖率报告
├── profile.sh              生成 gprof 性能分析报告
└── gcov_html.py            把 .gcov 汇总成 HTML

docs/
├── PSP.md                  PSP 预估 / 实际耗时表
├── coverage/               覆盖率报告（index.html 可直接浏览）
└── profiling/              性能分析：gprof flat profile + 前后对比图
```

博客（含设计说明、性能改进、单元测试与异常处理）：见仓库根目录 [`blog.md`](../blog.md)。

## 三、构建与测试

需要 gcc（MinGW-w64 或 Linux 均可）。

```bash
make            # 构建 main.exe
make test       # 编译并运行单元测试（101 条断言）
make e2e        # 端到端测试（18 条断言，按评测方式调用命令行）
make bench      # 两套 LCS 算法的耗时对照
make check      # 严格编译警告 + cppcheck，两者都必须干净
make clean      # 清理产物
```

## 四、当前状态

| 项目 | 结果 |
| --- | --- |
| 编译警告（`-Wall -Wextra -Wpedantic -Wconversion` 等 16 项） | 0 |
| cppcheck（`--enable=all --inconclusive`） | 0 条告警 |
| 单元测试 | 35 个用例 / 101 条断言，全部通过 |
| 端到端测试 | 10 个用例 / 18 条断言，全部通过 |
| 行覆盖率 | 85.94%（`check.c` 93.98%，`main.c` 88.00%） |
| 最坏样例耗时 | 0.07 s（优化前 3.4 s） |

## 五、算法要点

重复率 = **最长公共子序列长度 ÷ 原文长度**（比较前先去掉所有空白字符）。

两层优化：

1. **裁剪公共前后缀**——把抄袭版与原文首尾相同的部分直接计为公共内容，
   典型抄袭场景下能大幅缩小待计算规模；
2. **位并行 LCS**——把动态规划的一整行压进位向量，用进位/借位运算代替逐格比较，
   复杂度从 `O(m·n)` 降到 `O(m·n/64)`，实测**加速约 61 倍**。

详见 [`docs/profiling/`](docs/profiling/) 与博客正文。
