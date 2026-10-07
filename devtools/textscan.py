# 扫数据资产里 **玩家看得见** 的文本:夜晚选项的 resultHint、彩蛋的 desc、事件的 description。
# Unity 的 YAML 把中文写成 \uXXXX,直接 grep 读不出来,所以这里解码后再匹配。
# 用法:python devtools/textscan.py [正则,默认找概率写法]
import re, sys, glob, os

# 这台机器控制台默认 GBK,重定向到文件会变成乱码 ⇒ 显式走 UTF-8
try:
    sys.stdout.reconfigure(encoding='utf-8')
except Exception:
    pass

pat = re.compile(sys.argv[1] if len(sys.argv) > 1 else r'[0-9]+\s*%|概率|几率|掷')

def dec(s):
    return re.sub(r'\\u([0-9a-fA-F]{4})', lambda m: chr(int(m.group(1), 16)), s)

FIELDS = {'resultHint': '夜晚选项按钮括号那句(玩家可见)',
          'description': '事件描述(夜晚屏,玩家可见)',
          'desc': '物品/配方/行动说明(只有彩蛋 desc 上屏,其余是 Inspector 文档)'}

for f in sorted(glob.glob('Assets/Data/**/*.asset', recursive=True)):
    txt = open(f, encoding='utf-8', errors='replace').read()
    for k, note in FIELDS.items():
        # Unity 的 YAML 会把长字符串折行,所以取到"下一个顶格键"为止,而不是只取一行
        m = re.search(r'(?ms)^\s*%s:\s*(.*?)(?=\n(?: {0,2}|\t)?[A-Za-z_][A-Za-z0-9_]*:|\Z)' % k, txt)
        if not m:
            continue
        v = dec(m.group(1).replace('\n', ' ').strip())
        v = re.sub(r'\s+', ' ', v)
        if pat.search(v):
            print('%-11s %-34s | %s' % (k, os.path.relpath(f, 'Assets/Data'), v))
