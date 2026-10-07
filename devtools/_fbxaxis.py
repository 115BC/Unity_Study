import struct, sys, zlib

sys.stdout.reconfigure(encoding='utf-8', errors='replace')


def verts(path):
    b = open(path, 'rb').read()
    i = b.find(b'Vertices')
    if i < 0:
        return None
    assert b[i + 8] == ord('d'), hex(b[i + 8])
    count, enc, blen = struct.unpack_from('<III', b, i + 9)
    raw = b[i + 21:i + 21 + blen]
    data = zlib.decompress(raw) if enc == 1 else raw
    vals = struct.unpack('<%dd' % count, data)
    xs, ys, zs = vals[0::3], vals[1::3], vals[2::3]
    ext = lambda a: max(a) - min(a)
    lo = lambda a: min(a)
    return count, ext(xs), ext(ys), ext(zs), lo(ys), lo(zs)


for f in ['PalmTree_2', 'PalmTree_4', 'Bush_Large', 'Grass_Large']:
    r = verts('Assets/Art/quaternius/stylized_nature/%s.fbx' % f)
    n, ex, ey, ez, y0, z0 = r
    up = 'Y' if ey == max(ex, ey, ez) else ('Z' if ez == max(ex, ey, ez) else 'X')
    print('%-11s verts=%5d extent x=%5.2f y=%5.2f z=%5.2f => authored up=%s (y-min %.2f, z-min %.2f)'
          % (f, n, ex, ey, ez, up, y0, z0))
