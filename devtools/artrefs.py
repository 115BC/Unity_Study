# 查 Assets/Art 里哪些文件真的被引用(场景 / 材质 / 数据资产),用来回答"素材源库要不要全进版本库"。
# 用法:python devtools/artrefs.py [被扫的根目录,默认 Assets/Art]
import os, re, sys, collections

root_dir = sys.argv[1] if len(sys.argv) > 1 else 'Assets/Art'
SEP = '/'

guid2path = {}
for root, _, files in os.walk(root_dir):
    for fn in files:
        if not fn.endswith('.meta'):
            continue
        p = os.path.join(root, fn)
        txt = open(p, encoding='utf-8', errors='replace').read()
        m = re.search(r'guid: ([0-9a-f]{32})', txt)
        if m:
            guid2path[m.group(1)] = p[:-5].replace(os.sep, SEP)

refs = collections.Counter()
refby = collections.defaultdict(set)
for root, _, files in os.walk('Assets'):
    if root.replace(os.sep, SEP).startswith(root_dir.replace(os.sep, SEP)):
        continue
    for fn in files:
        if not fn.endswith(('.unity', '.mat', '.asset', '.prefab', '.controller')):
            continue
        p = os.path.join(root, fn)
        txt = open(p, encoding='utf-8', errors='replace').read()
        for g in set(re.findall(r'guid: ([0-9a-f]{32})', txt)):
            if g in guid2path:
                target = guid2path[g]
                refs[target] += 1
                refby[target].add(p.replace(os.sep, SEP))

total = sum(os.path.getsize(v) for v in guid2path.values() if os.path.exists(v))
hit = sum(os.path.getsize(v) for v in refs if os.path.exists(v))
print('%s: %d 个资产 / %.0f MB,其中被引用 %d 个 / %.0f MB'
      % (root_dir, len(guid2path), total / 1e6, len(refs), hit / 1e6))
for p in sorted(refs):
    sz = os.path.getsize(p) / 1e6 if os.path.exists(p) else 0
    print('  %7.1f MB  %s   <= %s' % (sz, p, ', '.join(sorted(refby[p])[:3])))
