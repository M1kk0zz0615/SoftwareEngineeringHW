@echo off
rem ===================================================================
rem  编译脚本：使用 Windows 自带的 C# 编译器 csc.exe 生成 Myapp.exe
rem  不需要安装 Visual Studio 或 .NET SDK
rem ===================================================================
setlocal

set "CSC=%SystemRoot%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [错误] 找不到 C# 编译器 csc.exe。
    echo        请改用 Visual Studio 打开 Myapp.csproj，或安装 .NET SDK 后执行 dotnet build。
    exit /b 1
)

pushd "%~dp0src"
"%CSC%" -nologo -codepage:65001 -target:exe -optimize+ -r:System.Numerics.dll -out:..\Myapp.exe *.cs
set "RESULT=%ERRORLEVEL%"
popd

if not "%RESULT%"=="0" (
    echo [错误] 编译失败。
    exit /b 1
)

echo [完成] 已生成 Myapp.exe
echo        试试：Myapp.exe -n 10 -r 10
endlocal
