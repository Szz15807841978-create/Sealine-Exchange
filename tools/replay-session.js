/* 从会话记录回放 write/edit，重建被编码破坏的文件 */
const fs = require('fs');
const path = require('path');

const dump = process.argv[2];
const target = process.argv[3] || 'index.html';
const outName = process.argv[4] || ('rebuilt-' + target);

const recs = [];
for (const line of fs.readFileSync(dump, 'utf8').split('\n')){
  if (!line.trim()) continue;
  try { recs.push(JSON.parse(line)); } catch (e) {}
}
recs.sort((a, b) => (a.seq || 0) - (b.seq || 0));

let content = null, applied = 0, failed = 0;
const log = [];

for (const r of recs){
  const c = r && r.data && r.data.message && r.data.message.content;
  if (!Array.isArray(c)) continue;
  for (const item of c){
    if (!item || item.type !== 'tool-call') continue;
    let args = item.arguments;
    if (typeof args === 'string'){ try { args = JSON.parse(args); } catch (e) { continue; } }
    if (!args) continue;
    const fp = String(args.file_path || args.path || '');
    if (!fp.endsWith(target)) continue;

    if (item.name === 'write'){
      content = args.content;
      applied++;
      log.push(`[write] ${fp} → ${content.split('\n').length} 行 / ${Buffer.byteLength(content)} 字节`);
    } else if (item.name === 'edit'){
      if (content === null){ log.push('[edit] 跳过：基线缺失'); failed++; continue; }
      const idx = content.indexOf(args.old_string);
      if (idx < 0){
        failed++;
        log.push('❌ [edit] 找不到片段: ' + JSON.stringify(String(args.old_string).slice(0, 70)));
        continue;
      }
      if (args.replace_all) content = content.split(args.old_string).join(args.new_string);
      else content = content.slice(0, idx) + args.new_string + content.slice(idx + args.old_string.length);
      applied++;
    }
  }
}

console.log(`应用 ${applied} 次 · 失败 ${failed}`);
const tail = log.slice(-6);
tail.forEach(l => console.log('  ' + l));
if (failed) console.log('失败明细:', log.filter(l => l.startsWith('❌')).slice(0, 10).join('\n  '));
if (content === null){ console.error('没有内容可写'); process.exit(1); }
fs.writeFileSync(path.join(__dirname, '..', outName), content, 'utf8');
console.log(`已写出 ${outName}：${content.split('\n').length} 行 / ${Buffer.byteLength(content)} 字节`);
