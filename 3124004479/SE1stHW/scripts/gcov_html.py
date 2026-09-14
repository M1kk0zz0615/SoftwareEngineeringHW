#!/usr/bin/env python3
"""把 gcov 生成的 .gcov 文本报告汇总成一个可在浏览器里看的 HTML 页面。

gcov 的 .gcov 文件每行格式为：
    <执行次数>:<行号>:<源码>          正常行
    <执行次数>:<行号>:<源码>          ##### 表示该行从未执行
    -:<行号>:<源码>                   非可执行行
    branch <N> taken <M>              分支信息
"""
import html
import os
import re
import sys

CSS = """
body { font-family: "Segoe UI", "Microsoft YaHei", sans-serif; margin: 0; padding: 24px;
       background: #f6f8fa; color: #24292f; }
h1 { font-size: 20px; }
h2 { font-size: 16px; margin-top: 28px; }
table { border-collapse: collapse; width: 100%; background: #fff; margin-bottom: 8px; }
th, td { border: 1px solid #d0d7de; padding: 6px 10px; text-align: left; font-size: 13px; }
th { background: #eaeef2; }
pre { background: #fff; border: 1px solid #d0d7de; border-radius: 6px;
      padding: 12px; overflow-x: auto; font-size: 12px; line-height: 1.5;
      font-family: Consolas, "Cascadia Mono", monospace; }
.z { color: #b71c1c; font-weight: 600; }   /* 未执行 */
.c { color: #1b5e20; }                     /* 已执行 */
.n { color: #8b949e; }                     /* 非可执行 */
.sum { background: #fff; border: 1px solid #d0d7de; border-radius: 6px; padding: 12px; }
"""

LINE = re.compile(r"^\s*([^:]*):(\s*\d+):(.*)$")


def render_gcov(path: str) -> str:
    rows = []
    with open(path, encoding="utf-8", errors="replace") as fp:
        for raw in fp:
            raw = raw.rstrip("\n")
            if raw.startswith(("branch ", "call ", "function ")):
                rows.append(f'<span class="n">{html.escape(raw)}</span>')
                continue
            m = LINE.match(raw)
            if not m:
                rows.append(html.escape(raw))
                continue
            count, lineno, src = m.group(1).strip(), m.group(2).strip(), m.group(3)
            body = html.escape(src)
            if count == "-":
                rows.append(f'<span class="n">{lineno:>5} {body}</span>')
            elif count in ("#####", "=====") or count.startswith("#####"):
                rows.append(f'<span class="z">{lineno:>5} {body}   // 未执行</span>')
            else:
                rows.append(f'<span class="c">{lineno:>5} {body}</span>')
    name = html.escape(os.path.basename(path).replace(".gcov", ""))
    return f"<h2>{name}</h2>\n<pre>{chr(10).join(rows)}</pre>\n"


def parse_summary(path: str):
    if not os.path.exists(path):
        return []
    out, cur = [], None
    with open(path, encoding="utf-8", errors="replace") as fp:
        for line in fp:
            line = line.rstrip("\n")
            if line.startswith("File '"):
                cur = {"file": line[6:-1]}
                out.append(cur)
            elif cur is not None and line.startswith("Lines executed:"):
                cur["lines"] = line.split(":", 1)[1].strip()
            elif cur is not None and line.startswith("Branches executed:"):
                cur["branches"] = line.split(":", 1)[1].strip()
            elif cur is not None and line.startswith("Taken at least once:"):
                cur["taken"] = line.split(":", 1)[1].strip()
    return out


def main() -> None:
    out_dir = sys.argv[1] if len(sys.argv) > 1 else "."
    parts = ['<!DOCTYPE html><html lang="zh-CN"><head><meta charset="utf-8">',
             "<title>覆盖率报告</title>", f"<style>{CSS}</style></head><body>",
             "<h1>单元测试覆盖率报告（gcov）</h1>"]

    rows = parse_summary(os.path.join(out_dir, "summary.txt"))
    if rows:
        parts.append('<div class="sum"><table>')
        parts.append("<tr><th>文件</th><th>行覆盖</th><th>分支覆盖</th><th>分支至少执行一次</th></tr>")
        for r in rows:
            parts.append(
                f"<tr><td>{html.escape(r['file'])}</td>"
                f"<td>{html.escape(r.get('lines', '-'))}</td>"
                f"<td>{html.escape(r.get('branches', '-'))}</td>"
                f"<td>{html.escape(r.get('taken', '-'))}</td></tr>"
            )
        parts.append("</table></div>")

    for name in sorted(os.listdir(out_dir)):
        if name.endswith(".gcov"):
            parts.append(render_gcov(os.path.join(out_dir, name)))

    parts.append("</body></html>")
    with open(os.path.join(out_dir, "index.html"), "w", encoding="utf-8") as fp:
        fp.write("\n".join(parts))
    print(f"  HTML 报告: {os.path.join(out_dir, 'index.html')}")


if __name__ == "__main__":
    main()
