# 读 pptx 的每页文字(pptx 是 zip,文字在 ppt/slides/slideN.xml 的 <a:t> 里)。
# 不用装 python-pptx;课件里的图/视频会占体积,这里只取文本。
# 用法:python devtools/pptxdump.py "第5章 ….pptx" ["第6章 ….pptx" …]
import re, sys, zipfile
import xml.etree.ElementTree as ET

try:
    sys.stdout.reconfigure(encoding='utf-8')
except Exception:
    pass

NS = '{http://schemas.openxmlformats.org/drawingml/2006/main}'

def slides(path):
    z = zipfile.ZipFile(path)
    names = [n for n in z.namelist() if re.match(r'ppt/slides/slide\d+\.xml$', n)]
    names.sort(key=lambda n: int(re.search(r'\d+', n).group()))
    for n in names:
        root = ET.fromstring(z.read(n))
        texts = [(t.text or '').strip() for t in root.iter(NS + 't')]
        yield n, [t for t in texts if t]

for path in sys.argv[1:]:
    print('\n########## %s ##########' % path)
    for n, texts in slides(path):
        print('\n--- %s ---' % n)
        print(' | '.join(texts))
