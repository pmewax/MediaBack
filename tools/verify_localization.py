from pathlib import Path
import json
import re

root = Path(__file__).resolve().parent.parent / 'source'
ru = json.loads((root / 'Localization/Strings.ru.json').read_text(encoding='utf-8'))
en = json.loads((root / 'Localization/Strings.en.json').read_text(encoding='utf-8'))
assert ru.keys() == en.keys(), 'Catalog key sets differ'
assert all(value.strip() for value in ru.values()) and all(value.strip() for value in en.values())
refs = set()
for path in root.rglob('*'):
    if path.suffix not in ('.cs', '.xaml') or any(part in path.parts for part in ('obj', 'bin')):
        continue
    text = path.read_text(encoding='utf-8-sig')
    refs.update(re.findall(r'Loc\.([A-Za-z]+)', text))
    refs.update(re.findall(r'L\.(?:Get|Format)\("([^"]+)"', text))
assert not (refs - ru.keys()), f'Missing keys: {refs - ru.keys()}'
for key in ru:
    assert sorted(re.findall(r'\{\d+[^}]*\}', ru[key])) == sorted(re.findall(r'\{\d+[^}]*\}', en[key])), key
    if key != 'Language':
        assert not re.search('[А-Яа-яЁё]', en[key]), f'Russian text in English catalog: {key}'
print(f'{len(ru)} matched RU/EN keys; {len(refs)} references; all placeholders valid.')
