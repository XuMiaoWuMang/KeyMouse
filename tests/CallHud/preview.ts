import { hudRows, statusText } from '../../src/KeyMouse.EditorUI/src/lib/hud';
import '../../src/KeyMouse.EditorUI/src/hud.css';
import raw from './nested-call.fixture?raw';
const root = document.querySelector('#hud')!;
const panel = document.createElement('div'); panel.className = 'hud';
panel.innerHTML = '<div class="head"><span class="pulse"></span><span class="title">运行日志</span><span class="tally">3</span></div>';
const rows = document.createElement('div'); rows.className = 'rows';
function append(row: ReturnType<typeof hudRows>[number], parent: HTMLElement) {
  const group = document.createElement('div'); group.className = 'group';
  const el = document.createElement('div'); el.className = `row ${row.status}${row.status === 'current' ? ' current' : ''}`;
  ['counter','name','colon','detail','status'].forEach((cls, i) => {
    const span = document.createElement('span'); span.className = cls;
    span.textContent = [`[${row.index}]`, row.name, ':', row.detail, statusText(row.status)][i]; el.append(span);
  }); group.append(el);
  if (row.children?.length) { const children = document.createElement('div'); children.className = 'children'; row.children.forEach(r => append(r, children)); group.append(children); }
  parent.append(group);
}
hudRows(raw.split('\n')).forEach(r => append(r, rows));
panel.append(rows); root.append(panel);
