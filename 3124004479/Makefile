# 论文查重 —— 构建脚本
#
# 用法：
#   make            构建 main.exe
#   make test       编译并运行单元测试
#   make e2e        编译 main.exe 并跑端到端测试
#   make bench      跑性能对照
#   make check      跑严格警告 + cppcheck
#   make clean      清理产物
#
# 注意：Windows 上的 `python` 常常是应用商店的占位壳，所以这里默认用 py -3。
#       在 Linux / macOS 上请用 make PYTHON=python3 ...

# make 内置了 CC = cc，而 Windows 上并没有 cc 这个命令，
# 所以要显式判断一下：只有用户没指定时才落到 gcc。
ifeq ($(origin CC),default)
CC := gcc
endif

PYTHON  ?= py -3

CFLAGS  ?= -std=c11 -O2 -Wall -Wextra -Wpedantic -Wshadow -Wconversion \
           -Wsign-conversion -Wcast-qual -Wstrict-prototypes \
           -Wmissing-prototypes -Wold-style-definition -Wwrite-strings \
           -Wundef -Wdouble-promotion -Wformat=2
LDFLAGS ?= -static

CORE     := check.c fileio.c common.c
HEADERS  := check.h fileio.h common.h
TEST_SRC := tests/main_test.c tests/test_check.c tests/test_fileio.c tests/test_common.c

SAMPLE   ?= ../测试文本

.PHONY: all test e2e bench check coverage profile clean

all: main.exe

main.exe: main.c $(CORE) $(HEADERS)
	$(CC) $(CFLAGS) $(LDFLAGS) -o $@ main.c $(CORE)

test_units.exe: $(TEST_SRC) $(CORE) $(HEADERS)
	$(CC) $(CFLAGS) -o $@ $(TEST_SRC) $(CORE)

bench.exe: tests/bench.c $(CORE) $(HEADERS)
	$(CC) $(CFLAGS) $(LDFLAGS) -o $@ tests/bench.c $(CORE)

test: test_units.exe
	./test_units.exe

e2e: main.exe
	$(PYTHON) tests/e2e_test.py

bench: bench.exe
	./bench.exe "$(SAMPLE)/orig.txt" "$(SAMPLE)/orig_0.8_add.txt" 1

# 严格警告 + 静态检查，两条都必须干净
check:
	$(CC) $(CFLAGS) -c $(CORE) main.c tests/*.c
	cppcheck --enable=all --inconclusive --check-level=exhaustive --inline-suppr \
	         --std=c11 --suppress=missingIncludeSystem --suppress=checkersReport \
	         $(CORE) main.c tests/*.c
	rm -f *.o

coverage:
	bash ../scripts/coverage.sh "$(SAMPLE)"

profile:
	bash ../scripts/profile.sh "$(SAMPLE)"

clean:
	rm -f *.exe *.o *.gcno *.gcda *.gcov gmon.out
	rm -f tests/*.o tests/*.gcno tests/*.gcda
