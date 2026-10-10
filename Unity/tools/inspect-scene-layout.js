const fs = require('fs');
const file = process.argv[2];
const blocks = fs.readFileSync(file, 'utf8').split(/(?=^--- !u!)/m);
const objects = new Map();
for (const b of blocks) {
  const h = b.match(/^--- !u!(\d+) &(\d+)/); if (!h) continue;
  objects.set(h[2], {type:+h[1], id:h[2], b});
}
function field(o, name) {return o?.b.match(new RegExp('^  '+name+': (.*)$','m'))?.[1];}
function ref(o, name) {return field(o,name)?.match(/fileID: (\d+)/)?.[1];}
function name(o) {return (field(o,'m_Name') || '').replace(/^"|"$/g,'').replace(/\\u([a-f\d]{4})|\\x([a-f\d]{2})/ig, (_,u,x)=>String.fromCharCode(parseInt(u||x,16)));}
function path(o) {if (!o) return ''; const p=objects.get(ref(o,'m_Father'));return (p?path(p)+'/':'')+name(objects.get(ref(o,'m_GameObject')));}
for (const o of objects.values()) if(o.type===224 || o.type===4) {
  const p = path(o);
  if(process.argv[3] ? new RegExp(process.argv[3]).test(p) : /(?:0[1-8]_|Player Canvas|Panel|RaftRoot|木筏根)/.test(p) && p.split('/').length<=5)
    console.log(JSON.stringify({id:o.id,go:ref(o,'m_GameObject'),path:p,layer:field(objects.get(ref(o,'m_GameObject')),'m_Layer'),active:field(objects.get(ref(o,'m_GameObject')),'m_IsActive'),pos:field(o,'m_LocalPosition'),scale:field(o,'m_LocalScale'),anchorMin:field(o,'m_AnchorMin'),anchorMax:field(o,'m_AnchorMax'),pivot:field(o,'m_Pivot'),anchored:field(o,'m_AnchoredPosition'),size:field(o,'m_SizeDelta')}));
}
