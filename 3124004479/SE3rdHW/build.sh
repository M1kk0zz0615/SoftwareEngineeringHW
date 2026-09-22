#!/usr/bin/env bash
# ===================================================================
#  编译脚本（Git Bash / MSYS 环境）：生成 Myapp.exe
#  与 build.bat 等价，使用 Windows 自带的 C# 编译器
# ===================================================================
set -eu

CSC="/c/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe"
if [ ! -x "$CSC" ]; then
    CSC="/c/Windows/Microsoft.NET/Framework/v4.0.30319/csc.exe"
fi
if [ ! -x "$CSC" ]; then
    echo "[错误] 找不到 csc.exe，请安装 .NET SDK 后执行 dotnet build。" >&2
    exit 1
fi

# 必须在 src 目录里编译：csc.exe 对含非 ASCII 字符的 "src/xxx.cs" 这类相对路径处理不可靠
cd "$(dirname "$0")/src"
"$CSC" -nologo -codepage:65001 -target:exe -optimize+ -r:System.Numerics.dll -out:../Myapp.exe *.cs

echo "[完成] 已生成 Myapp.exe"
echo "       试试：./Myapp.exe -n 10 -r 10"
