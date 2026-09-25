import zipfile, xml.etree.ElementTree as ET, json
from pathlib import Path
NS={'s':'http://schemas.openxmlformats.org/spreadsheetml/2006/main'}
out=[]
for path in Path('docs').glob('*.xlsx'):
 with zipfile.ZipFile(path) as z:
  strings=[]
  if 'xl/sharedStrings.xml' in z.namelist():
   strings=[''.join(n.itertext()) for n in ET.fromstring(z.read('xl/sharedStrings.xml'))]
  wb=ET.fromstring(z.read('xl/workbook.xml'))
  names=[s.attrib['name'] for s in wb.find('s:sheets',NS)]
  out.append('\nFILE '+path.name)
  for idx,name in enumerate(names,1):
   root=ET.fromstring(z.read(f'xl/worksheets/sheet{idx}.xml'))
   merges=[m.attrib['ref'] for m in root.findall('s:mergeCells/s:mergeCell',NS)]
   out.append(f'SHEET {name} merges={len(merges)}')
   for row in root.findall('s:sheetData/s:row',NS):
    vals=[]
    for c in row:
     v=c.find('s:v',NS); value=v.text if v is not None else ''
     if c.attrib.get('t')=='s': value=strings[int(value)] if value else ''
     if c.attrib.get('t')=='inlineStr': value=''.join(c.find('s:is',NS).itertext())
     if value: vals.append(c.attrib['r']+'='+value.replace('\n',' / '))
    if vals: out.append(' | '.join(vals))
   out.append('MERGES '+','.join(merges))
Path('docs/source-inspection.txt').write_text('\n'.join(out),encoding='utf-8')
