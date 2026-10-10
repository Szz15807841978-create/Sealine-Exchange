/* zstd 多帧解压：DSH 的会话文件是多个 zstd 帧顺序拼接的 */
const fs = require('fs');
const zlib = require('zlib');

function findAll(buf, magic){
  const out = [];
  for (let i = 0; i + magic.length <= buf.length; i++){
    let ok = true;
    for (let k = 0; k < magic.length; k++) if (buf[i + k] !== magic[k]) { ok = false; break; }
    if (ok) out.push(i);
  }
  return out;
}

function decompressAll(file){
  const buf = fs.readFileSync(file);
  const magic = [0x28, 0xB5, 0x2F, 0xFD];
  const skip  = [0x50, 0x2A, 0x4D, 0x18];
  const starts = [...new Set([...findAll(buf, magic), ...findAll(buf, skip)])].sort((a, b) => a - b);
  if (!starts.length || starts[0] !== 0) starts.unshift(0);
  const parts = [];
  let okFrames = 0, badFrames = 0;
  for (let i = 0; i < starts.length; i++){
    const from = starts[i];
    const to = i + 1 < starts.length ? starts[i + 1] : buf.length;
    if (to <= from) continue;
    try {
      parts.push(zlib.zstdDecompressSync(buf.subarray(from, to)));
      okFrames++;
    } catch (e) {
      // 可能是数据里恰好出现的 magic 字节，合并到下一帧一起试
      let merged = null;
      for (let j = i + 2; j <= starts.length; j++){
        const end = j < starts.length ? starts[j] : buf.length;
        try { merged = zlib.zstdDecompressSync(buf.subarray(from, end)); okFrames++; i = j - 1; break; } catch (e2) {}
      }
      if (merged) parts.push(merged); else badFrames++;
    }
  }
  return { text: Buffer.concat(parts).toString('utf8'), okFrames, badFrames };
}

const file = process.argv[2];
const out = process.argv[3] || 'session-dump.jsonl';
const r = decompressAll(file);
fs.writeFileSync(out, r.text, 'utf8');
console.log(`帧：成功 ${r.okFrames} · 失败 ${r.badFrames}`);
console.log(`解压字符数 ${r.text.length} · 行数 ${r.text.split('\n').filter(l => l.trim()).length}`);
