# 性能分析与改进

## 一、用什么工具

作业原文建议用 VS 2017 的性能分析工具。本机没有安装 Visual Studio，因此改用
**gprof**（GNU Profiler），它同样能给出「每个函数自身耗时占比」的 flat profile，
达到与 Studio Profiling Tools 相同的分析目的。

> 踩坑记录：MinGW 版的 gprof 在 Windows 上**采不到样**——`gmon.out` 里只有调用次数，
> PC 采样直方图是空的（`gprof` 输出里一个函数行都没有）。
> 因此性能数据是在 **WSL2 Ubuntu** 下用同一份源码重新编译采集的，
> gcc 15.2.0 + `-pg -O2`。源码完全一致，只是运行平台不同。

## 二、性能分析图

![优化前后对比](before_after.svg)

优化前后的 gprof flat profile 原文见
[`gprof_before.txt`](gprof_before.txt) 与 [`gprof_after.txt`](gprof_after.txt)。

### 优化前（经典动态规划）

```
Flat profile:

Each sample counts as 0.01 seconds.
  %   cumulative   self              self     total
 time   seconds   seconds    calls   s/call   s/call  name
100.00      2.61     2.61        1     2.61     2.61  check_lcs_dp
  0.00      2.61     0.00        2     0.00     0.00  check_strip_blank
  0.00      2.61     0.00        2     0.00     0.00  fileio_read_all
  0.00      2.61     0.00        1     0.00     2.61  check_similarity
  0.00      2.61     0.00        1     0.00     0.00  fileio_write_rate
```

**消耗最大的函数：`check_lcs_dp`——自身耗时 2.61 秒，占 100.00%。**

瓶颈非常干净：整个程序的时间几乎全部花在求最长公共子序列上，文件 I/O 和
空白过滤的耗时小到采样精度都测不出来。所以优化方向唯一且明确 —— **把 LCS 算快**。

### 优化后（位并行）

```
Flat profile:

Each sample counts as 0.01 seconds.
  %   cumulative   self              self     total
 time   seconds   seconds    calls  ms/call  ms/call  name
100.00      0.04     0.04        1    40.00    40.00  frame_dummy
  0.00      0.04     0.00        2     0.00     0.00  check_strip_blank
  0.00      0.04     0.00        2     0.00     0.00  fileio_read_all
  0.00      0.04     0.00        1     0.00    40.00  check_similarity
  0.00      0.04     0.00        1     0.00     0.00  fileio_write_rate
```

整个程序从 2.61 秒降到 0.04 秒，`check_lcs` 已经**低到采样精度以下**（不到 10 毫秒
一个采样点都采不到）。此时 `frame_dummy` 顶上来的 40 毫秒是 gprof 自身的启动开销，
不是业务代码。

### 两套算法在同一次采样里的对比

```
Flat profile:

Each sample counts as 0.01 seconds.
  %   cumulative   self              self     total
 time   seconds   seconds    calls   s/call   s/call  name
 98.32      2.93     2.93        1     2.93     2.93  check_lcs_dp
  1.68      2.98     0.05        1     0.05     0.05  frame_dummy
  0.00      2.98     0.00        2     0.00     0.00  check_strip_blank
  0.00      2.98     0.00        2     0.00     0.00  fileio_read_all
  0.00      2.98     0.00        1     0.00     0.00  check_lcs
```

同一个进程里跑两套算法：`check_lcs_dp` 占 98.32%，`check_lcs` 采不到样本。

## 三、改进思路

### 3.1 瓶颈的成因

原来的实现是教科书式的动态规划：

```
L[i][j] = L[i-1][j-1] + 1                    , a[i] == b[j]
        = max(L[i-1][j], L[i][j-1])          , 否则
```

时间复杂度 `O(m·n)`。样例里 `m·n ≈ 3.93 × 10⁹`，2.6~2.9 秒完全对得上。

### 3.2 第一刀：裁掉公共前后缀

真实的抄袭文本往往是大段照抄，开头和结尾常常一字不差。设公共前缀长 `p`、
公共后缀长 `s`，有：

```
LCS(A, B) = p + s + LCS(A[p : m-s], B[p : n-s])
```

这个等式是严格的，不是启发式。对「原文尾部追加内容」「原文中间删一段」这两类
典型抄袭，`p + s` 能直接吃掉绝大部分内容，剩下的中间部分规模骤减。

代价是 `O(m + n)`，收益却可能是把 `m·n` 砍掉几个数量级，非常划算。

### 3.3 第二刀（关键）：位并行 LCS

动态规划每一格都要做一次比较和一次取最大值，而「取最大值」在二进制里是可以用
**进位运算**模拟的。Crochemore 等人在 2001 年给出的位向量算法把整行 DP 压成
一个位向量，每处理一个字符只需要一次全字长的加法、减法和或运算：

```
X = S & mask[b[j]]          // 本轮能匹配上的位置
S = (S + X) | (S - X)       // 进位传播 + 借位传播，无分支
```

其中 `mask[c]` 的第 `i` 位表示「`a[i] == c`」。全部处理完后，
`S` 中 0 的个数就是 LCS 长度。

**复杂度从 `O(m·n)` 降到 `O(m·n / 64)`**——一次 64 位整数运算顶掉 64 次逐位比较。

实现上有两个细节值得说：

1. **位向量建在较短的串上**。时间不变（`m·n/64` 是对称的），但内存从
   `O(m/64)` 降下来，同时也让 `mask` 表更小——表的空间是 `256 × words`，
   `words = ⌈m/64⌉`，也就是说表大小约等于 `32 × m` 字节。
2. **进位/借位可以一次比较算出来**。加法 `sk + xk + carry` 的进位等于
   `(t < sk) + (ta < t)`（两次进位不会同时发生，所以用 `+` 合成一位是安全的），
   不用分支，编译器能生成无跳转的指令。

### 3.4 结果

| 算法 | 复杂度 | LCS 耗时 | 结果 |
| --- | --- | --- | --- |
| 经典动态规划 | `O(m·n)` | 2 930 ms | 23 397 |
| 位并行 | `O(m·n/64)` | 48 ms | 23 397 |

**加速比约 61 倍，两套算法结果完全一致。**

原来最坏的样例（162 KB 的 HTML 噪声文件）跑一次要 3.4 秒，已经贴着作业 5 秒的红线；
优化后同一个样例 **0.07 秒**完成，红线压力彻底解除。

## 四、复现方式

```bash
# Windows 下（用 MinGW，只能拿到调用次数，采不到样）
bash scripts/profile.sh

# 想要真正的采样数据，在 WSL Ubuntu 里：
wsl -d Ubuntu -u root
cd /root/se_prof && gcc -pg -O2 -std=c11 -o main_after.exe main.c check.c fileio.c common.c
./main_after.exe /mnt/e/.../orig.txt /mnt/e/.../orig_0.8_del.txt ans.txt
gprof -p -b main_after.exe gmon.out
```

`tests/bench.c` 是两套算法的绝对耗时对照工具，可以独立运行：

```bash
make bench
# 或
./bench.exe <原文> <抄袭版> 1
```
