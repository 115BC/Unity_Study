#!/usr/bin/env python3
# 读一个二进制 FBX 的网格重量:顶点数 / 多边形数 / 三角数。
# 为什么不用 Inspector:Unity 2022 的 Model 导入页面上没有这行统计(它只在预览窗口底部,
# 中文界面下位置还会变),而我们要的是"这件模型到底多少面"这种可比对的数。
# 数组头布局(实测,FBX SDK 2017.1 / 文件版本 7700):名字之后 **紧跟一个类型码字符**,
#   再跟 元素个数(4) / 是否压缩(4,1=zlib) / 数据字节长(4) / 数据。
#   ⇒ 偏移是 `len(名字)`,不是固定 8(我第一版按 'Vertices' 的 8 个字符写死,结果 18 字符的
#      'PolygonVertexIndex' 整个读歪 —— 顶点数照样像样,三角数静默变 0,这种"一半对"最骗人)。
# 两种索引写法都要认:常见 = `PolygonVertexIndex`(负数收尾一个多边形)。
import struct, sys, zlib

FMT = {'d': '<d', 'f': '<f', 'i': '<i', 'l': '<q'}


def arrays(data):
    out = {}
    for name in (b'Vertices', b'PolygonVertexIndex', b'PolygonIndices', b'PolygonVertexCount'):
        i = data.find(name)
        while i >= 0:
            h = i + len(name)                       # 名字后面紧跟类型码
            t = data[h:h + 1].decode('latin1')
            try:
                cnt, enc, blen = struct.unpack_from('<iii', data, h + 1)
            except struct.error:
                break
            if t in FMT and 0 < cnt < 20_000_000 and 0 < blen <= 64_000_000:
                raw = data[h + 13:h + 13 + blen]
                if enc == 1:
                    try:
                        raw = zlib.decompress(raw)
                    except zlib.error:
                        i = data.find(name, i + 1)
                        continue
                out[name.decode()] = (t, cnt, raw)
                break
            i = data.find(name, i + 1)
    return out


def ints(a, key):
    t, cnt, raw = a[key]
    f = struct.calcsize(FMT[t])
    return [int(struct.unpack_from(FMT[t], raw, n * f)[0]) for n in range(cnt)]


def main(path):
    try:
        data = open(path, 'rb').read()
    except OSError as e:
        print('%s: 读不到(%s)' % (path, e))
        return
    a = arrays(data)
    if 'Vertices' not in a:
        print('%s: 没读到 Vertices 数组(不是二进制 FBX?)' % path)
        return
    verts = a['Vertices'][1] // 3
    polys, tris = 0, 0
    if 'PolygonIndices' in a and 'PolygonVertexCount' in a:
        counts = ints(a, 'PolygonVertexCount')
        polys = len(counts)
        tris = sum(max(0, c - 2) for c in counts)
    elif 'PolygonVertexIndex' in a:
        idx, n = ints(a, 'PolygonVertexIndex'), 0
        for v in idx:
            n += 1
            if v < 0:
                polys += 1
                tris += max(0, n - 2)
                n = 0
    print('%-46s 顶点 %7s  多边形 %6s  三角 %7s  文件 %9s B' %
          (path.split('/')[-1], format(verts, ','), format(polys, ','), format(tris, ','), format(len(data), ',')))


for p in sys.argv[1:] or ['Assets/Resources/Models/net.fbx']:
    main(p)
