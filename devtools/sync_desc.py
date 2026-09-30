# -*- coding: utf-8 -*-
"""把 **烘焙器里的那条文案字面量** 原样写进对应资产的字段。

为什么需要它:本项目里同一句文案存在两份 —— `DemoAssetBaker` 里的字面量 与 盘上的 `.asset`
(烘焙器"已有资产不覆盖",所以改了代码不会改资产,反之也一样)。
两边一旦漂,谁跑一次 `③ 强制重建` 文案就变 ⇒ 资产不再是"Inspector 里看到的那份是真源"。
v0.48 起定的口径:**要改文案就改烘焙器,然后跑这个脚本回写资产**,不要手打第二遍。

用法:
    python devtools/sync_desc.py                  # 对齐下面 PAIRS 里列的全部
    python devtools/sync_desc.py Recipe=wall Tool=flint
只在 "烘焙器里能按 调用名+key 定位、且块内第三个字符串字面量就是那条 desc" 时可靠 ——
对 Recipe/Tool/Item/Act 这四种 helper 成立;`Ch(...)`(label+hint 两条)请单独处理。
"""
import io, os, re, sys

# 控制台是 GBK 时,文案里的 ✅/⚠ 会让 print 崩 —— 统一按 UTF-8 输出,编码不了的字符直接替换掉
try:
    sys.stdout.reconfigure(encoding='utf-8', errors='replace')
except Exception:
    pass

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
BAKER = os.path.join(ROOT, 'Assets/Scripts/Editor/DemoAssetBaker.cs')
Q, BS = chr(34), chr(92)

# 调用名 → (字段名在资产里叫什么, 资产目录)
FAM = {
    'Recipe': ('desc', 'Assets/Data/Recipes'),
    'Tool':   ('desc', 'Assets/Data/Tools'),
    'Item':   ('desc', 'Assets/Data/Items'),
    'Act':    ('desc', 'Assets/Data/Activities'),
    'Ev':     ('description', 'Assets/Data/Events'),
}
# 默认对齐这些(v0.48 那几处我们两边都动过文案的)
DEFAULT = ['Recipe=campfire', 'Recipe=signalfire', 'Recipe=wall', 'Tool=flint']

BAK = io.open(BAKER, encoding='utf-8').read()


def unesc(s):
    out, i = [], 0
    while i < len(s):
        if s[i] == BS and i + 1 < len(s):
            n = s[i + 1]
            if n == 'u' and i + 5 < len(s):
                out.append(chr(int(s[i + 2:i + 6], 16))); i += 6; continue
            m = {'n': '\n', 'r': '\r', 't': '\t', Q: Q, BS: BS}.get(n, n)
            out.append(m); i += 2; continue
        out.append(s[i]); i += 1
    return ''.join(out)


def literals(block):
    res, i = [], 0
    while i < len(block):
        if block[i] == Q:
            j, buf = i + 1, []
            while j < len(block):
                if block[j] == BS:
                    buf.append(block[j]); buf.append(block[j + 1]); j += 2; continue
                if block[j] == Q:
                    break
                buf.append(block[j]); j += 1
            res.append(unesc(''.join(buf))); i = j + 1; continue
        i += 1
    return res


def call_block(head):
    i = BAK.index(head)
    d, j = 0, i
    while j < len(BAK):
        if BAK[j] == '(':
            d += 1
        elif BAK[j] == ')':
            d -= 1
            if d == 0:
                return BAK[i:j + 1]
        j += 1
    raise RuntimeError('括号没闭合:' + head)


def esc(v):
    return Q + ''.join(c if ord(c) < 128 else BS + 'u%04X' % ord(c) for c in v) + Q


def write_field(path, field, value):
    raw = io.open(path, encoding='utf-8', newline='').read()
    nl = '\r\n' if '\r' in raw else '\n'
    L = raw.split(nl)
    i = next((k for k, x in enumerate(L) if x.startswith('  ' + field + ':')), -1)
    if i < 0:
        return False
    j = i + 1
    while j < len(L) and (L[j].startswith('    ') or L[j].startswith('   ')):   # 吃掉折行续行
        j += 1
    L[i:j] = ['  ' + field + ': ' + value]
    io.open(path, 'w', encoding='utf-8', newline='').write(nl.join(L))
    return True


targets = sys.argv[1:] or DEFAULT
report = []
for spec in targets:
    fam, key = spec.split('=', 1)
    if fam not in FAM:
        report.append('跳过未知家族:' + spec); continue
    field, folder = FAM[fam]
    ls = literals(call_block(fam + '(' + Q + key + Q))
    if len(ls) < 3:
        report.append('字面量不足:' + spec); continue
    p = os.path.join(ROOT, folder, key + '.asset')
    ok = write_field(p, field, esc(ls[2]))
    report.append(('OK   ' if ok else 'MISS ') + spec + ' → ' + ls[2][:34] + ('…' if len(ls[2]) > 34 else ''))
print('\n'.join(report))
