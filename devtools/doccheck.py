# -*- coding: utf-8 -*-
"""开发文档.md 的两项结构自检(每次改完文档随手跑一次)。

1) `**` 不成对的行 —— 少一个 `**` 会让后面一大片渲染成粗体,肉眼很容易漏。
2) 表格行的列数与同一张表的表头不一致 —— 用编辑工具往长行里塞字时最容易把收尾的 `|` 吃掉。
   · 伪代码单元格里合法的 `||`(逻辑或)先抹掉再数,避免误报;
   · 只数 **以 `|` 开头的行**;紧随其后、不以 `|` 开头的行是"断行的续行",单独报出来。

两条都有历史基线(见 BASE_*):工具 **只对"比基线多出来的"报警**,免得真信号被噪声淹掉。
用法:python devtools/doccheck.py     退出码 0=干净,1=有新增可疑项
"""
import io
import sys

try:                                    # Windows 控制台默认 GBK,✅/⚠ 会直接把 print 炸掉
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
except Exception:
    pass

PATH = '开发文档.md'
BASE_BOLD = 11          # 2026-09-30 实测:历史遗留的粗体失衡行
BASE_TABLE = 2          # §3.2 猴子 / 友善的猴子 那两行是**跨行折断**的历史遗留(见下)

lines = io.open(PATH, encoding='utf-8').read().split('\n')

# ---- 1) 粗体失衡 ----
unbalanced = [i + 1 for i, l in enumerate(lines) if l.count('**') % 2]
worse_bold = len(unbalanced) > BASE_BOLD
print('`**` 不成对的行:%d(基线 %d)%s' % (
    len(unbalanced), BASE_BOLD, '  ⚠ 比基线多 —— 检查本轮改动' if worse_bold else '  ok'))
for n in unbalanced[-3:]:
    print('   %5d  %s' % (n, lines[n - 1][:70]))

# ---- 2) 表格列数 ----
def cells(line):
    return line.replace('||', '  ').count('|')     # 抹掉伪代码里的逻辑或

runs, cur = [], []
for i, l in enumerate(lines):
    if l.startswith('|'):
        cur.append((i + 1, cells(l)))
    else:
        if len(cur) > 1:
            runs.append(cur)
        cur = []
if len(cur) > 1:
    runs.append(cur)

mismatch = []
for r in runs:
    head = r[0][1]
    mismatch += [(n, head, c) for n, c in r if c != head]

# 断行:表里某行之后紧跟一条不以 | 开头、又不是空行的普通文本 ⇒ 那行被折断了
wrapped = []
for i, l in enumerate(lines):
    if l.startswith('|') and l.rstrip().endswith('**') and i + 1 < len(lines):
        nxt = lines[i + 1]
        if nxt.strip() and not nxt.startswith('|') and not nxt.startswith('>') and not nxt.startswith('-'):
            wrapped.append(i + 2)

print('列数与表头不符的行:%d(基线 %d)%s' % (
    len(mismatch), BASE_TABLE, '  ⚠ 新增:' + str([m for m in mismatch][:8]) if len(mismatch) > BASE_TABLE else '  ok'))
print('疑似被折断的表格行(续行不以 | 开头):%d 处 → 行号 %s' % (len(wrapped), wrapped[:8]))
sys.exit(1 if (worse_bold or len(mismatch) > BASE_TABLE) else 0)
