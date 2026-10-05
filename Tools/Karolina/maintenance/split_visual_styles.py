"""One-time extraction of existing functional layout rules into component/page modules."""
from pathlib import Path
import re

web = Path(__file__).resolve().parents[1] / 'Karolina.Desktop/Web'
source = (web/'style.css').read_text(encoding='utf-8-sig')
if '@import' in source: raise RuntimeError('Styles already extracted')
modules = {k:[] for k in ['base','layout','navigation','chat','document','review','tools','markdown','dialog','input','badge','responsive']}
pattern = re.compile(r'[^{}]+\{')
cursor = 0
while cursor < len(source):
    m=pattern.match(source,cursor)
    if not m: break
    start=cursor; depth=1; cursor=m.end()
    while cursor<len(source) and depth:
        if source[cursor]=='{':depth+=1
        elif source[cursor]=='}':depth-=1
        cursor+=1
    rule=source[start:cursor].strip();selector=rule.split('{',1)[0].strip()
    if selector.startswith(':root') or selector=='body.dark':continue
    if 'button' in selector and selector in ['button','button:hover','button:disabled','button.primary']:continue
    if selector.startswith('@media'):key='responsive'
    elif any(k in selector for k in ['review','diff','approval','noteLocation']):key='review'
    elif any(k in selector for k in ['tools','toolInvocation','toolPlanRow','toolArguments','toolOutput','toolManual']):key='tools'
    elif any(k in selector for k in ['document','source-editor','reading-layout','toc','change-summary','metadata','meta-tabs','relation-card','history-entry']):key='document'
    elif 'markdown' in selector:key='markdown'
    elif any(k in selector for k in ['dialog','drawer','toast']):key='dialog'
    elif any(k in selector for k in ['sidebar','directory','workspace','rail','brand','group']):key='navigation'
    elif any(k in selector for k in ['composer','message','welcome','reference','execution-plan','workflow','tool-event']):key='chat'
    elif any(k in selector for k in ['input','textarea','select','search']):key='input'
    elif any(k in selector for k in ['badge','.dot']):key='badge'
    elif any(k in selector for k in ['main','topbar','top-actions','footer','.page','.empty','tool-spacer']):key='layout'
    else:key='base'
    modules[key].append(rule)
for name,rules in modules.items():
    target=web/'styles'/('pages' if name in ['chat','document','review','tools'] else 'primitives' if name in ['dialog','input','badge','markdown'] else 'foundation')/f'{name}.css'
    target.parent.mkdir(parents=True,exist_ok=True)
    target.write_text('/* Functional structure preserved from the original workbench. Theme atoms override appearance. */\n'+'\n'.join(rules)+'\n',encoding='utf-8')
(web/'style.css').write_text('\n'.join(f'@import url("./styles/{"pages" if name in ["chat","document","review","tools"] else "primitives" if name in ["dialog","input","badge","markdown"] else "foundation"}/{name}.css");' for name in modules)+'\n'+ '\n'.join(f'@import url("./styles/{file}");' for file in ['tokens.css','primitives/button.css','primitives/button-frame.css','primitives/button-label.css','primitives/button-node.css','primitives/button-indicator.css','primitives/icon.css','primitives/panel.css','primitives/field.css','primitives/list-item.css','primitives/segmented.css','primitives/focus.css','primitives/phone-face.css','composition.css','motion.css','appearance.css']),encoding='utf-8')
print('Extracted',sum(map(len,modules.values())),'functional rules into',len(modules),'modules')
