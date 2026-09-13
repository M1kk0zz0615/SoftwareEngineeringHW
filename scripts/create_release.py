#!/usr/bin/env python3
"""用 GitHub API 创建 Release 并上传 main.exe。

凭据从 Git Credential Manager 取（和 git push 用的是同一份），
token 只存在于内存里，不打印、不落盘。

用法：
    python create_release.py [tag] [标题]
"""
import json
import os
import subprocess
import sys
import urllib.error
import urllib.request

REPO = "M1kk0zz0615/3124004479"
HERE = os.path.dirname(os.path.abspath(__file__))
EXE = os.path.join(HERE, "..", "3124004479", "main.exe")

BODY = """论文查重：输入原文与抄袭版论文，输出重复率到答案文件（两位小数）。

- 重复率口径：最长公共子序列 ÷ 原文长度（比较前去掉所有空白字符）
- 位并行 LCS，O(m*n/64)，最坏样例 0.07 秒
- 详见仓库 README 与 docs/profiling/"""


def get_token() -> str:
    proc = subprocess.run(
        ["git", "credential", "fill"],
        input="protocol=https\nhost=github.com\n\n",
        capture_output=True,
        text=True,
    )
    for line in proc.stdout.splitlines():
        if line.startswith("password="):
            return line[len("password="):]
    return ""


def api(token: str, url: str, data=None, ctype="application/json"):
    req = urllib.request.Request(url, data=data, method="POST" if data is not None else "GET")
    req.add_header("Authorization", f"token {token}")
    req.add_header("Accept", "application/vnd.github+json")
    req.add_header("User-Agent", "se-homework-release")
    if data is not None:
        req.add_header("Content-Type", ctype)
    try:
        with urllib.request.urlopen(req, timeout=120) as resp:
            return json.loads(resp.read().decode("utf-8"))
    except urllib.error.HTTPError as exc:
        print(f"HTTP {exc.code}: {exc.read().decode('utf-8', 'replace')[:600]}", file=sys.stderr)
        return None


def main() -> int:
    tag = sys.argv[1] if len(sys.argv) > 1 else "v1.0.0"
    title = sys.argv[2] if len(sys.argv) > 2 else "v1.0.0 首个可用版本"

    token = get_token()
    if not token:
        print("取不到 GitHub 凭据，请手动创建 Release（见 docs/RELEASE.md）", file=sys.stderr)
        return 1

    with open(EXE, "rb") as fp:
        blob = fp.read()
    print(f"==> 待上传 {os.path.basename(EXE)}（{len(blob)} 字节）")

    payload = json.dumps({
        "tag_name": tag,
        "name": title,
        "body": BODY,
        "draft": False,
        "prerelease": False,
    }).encode("utf-8")

    print(f"==> 创建 Release {tag}")
    rel = api(token, f"https://api.github.com/repos/{REPO}/releases", payload)
    if rel is None:
        return 1
    upload_url = rel["upload_url"].split("{")[0]
    print(f"    创建成功: {rel['html_url']}")

    print("==> 上传可执行文件")
    asset = api(token, f"{upload_url}?name=main.exe", blob,
                ctype="application/octet-stream")
    if asset is None:
        return 1
    print(f"    下载地址: {asset['browser_download_url']}")
    print(f"\n完成: https://github.com/{REPO}/releases")
    return 0


if __name__ == "__main__":
    sys.exit(main())
