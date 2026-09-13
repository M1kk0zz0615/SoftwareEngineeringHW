# 发布说明

作业要求把编译好的程序发布到 GitHub 仓库的 **Releases** 里。

## 一、本次要发布的产物

| 文件 | 说明 |
| --- | --- |
| `3124004479/main.exe` | 评测用的可执行文件，64 位 Windows |

- 大小：98 827 字节
- SHA256：`fbaffaf3171cd0572bca5fe1592e6996bc8089806708d20fed46df40da583ca2`
- 构建方式：`gcc -std=c11 -O2 -static -o main.exe main.c check.c fileio.c common.c`

加了 `-static`，把运行库一起链进去，评测机上不需要额外装 MinGW 运行库。

## 二、怎么创建 Release

命令行（需要先装 GitHub CLI 并 `gh auth login`）：

```bash
cd 3124004479
gh release create v1.0.0 main.exe \
  --title "v1.0.0 首个可用版本" \
  --notes "论文查重：输入原文与抄袭版论文，输出重复率到答案文件（两位小数）。"
```

网页操作：

1. 打开仓库页面 → 右侧 **Releases** → **Create a new release**
2. Tag 填 `v1.0.0`，Target 选 `main` 分支
3. 标题填 `v1.0.0 首个可用版本`
4. 把 `3124004479/main.exe` 拖进附件区
5. 点 **Publish release**

## 三、评测机上怎么跑

```
main.exe <原文文件> <抄袭版论文文件> <答案文件>

例：
main.exe C:\tests\orig.txt C:\tests\orig_0.8_add.txt C:\tests\ans.txt
```

答案文件里写的是形如 `0.82` 的浮点数（带一个换行）。

## 四、从源码重新构建

评测方会验证「提交的可执行文件能否用源码编译出逻辑相同的程序」，所以：

```bash
cd 3124004479
gcc -std=c11 -O2 -static -o main.exe main.c check.c fileio.c common.c
```

或直接：

```bash
make
```

`make check` 会跑一遍严格编译警告和 cppcheck，两者都应该是零告警。
