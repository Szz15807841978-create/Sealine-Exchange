'use strict';
const fs=require('fs'),path=require('path'),vm=require('vm'),assert=require('assert/strict');
function engine(file){const c={};vm.createContext(c);vm.runInContext(fs.readFileSync(file,'utf8').match(/\/\* ENGINE_BEGIN[\s\S]*?\*\/([\s\S]*?)\/\* ENGINE_END \*\//)[1]+';globalThis.E=Engine;',c);return c.E;}
const E=engine(path.join(__dirname,'../player.html')),O=engine(path.join(__dirname,'fixtures/20261006-before-downside/player.html'));
const okay=x=>assert.equal(x,''),near=(a,b)=>assert(Math.abs(a-b)<1e-10,`${a} != ${b}`);
function ready(api,seed){const s=api.newState(seed);okay(api.debugSurvey(s));okay(api.startSession(s));s.gaugeIndex=2;return s;}
for(let seed=1;seed<=60;seed++){
 const old=ready(O,seed);O.advance(old,23);O.pause(old);const restored=E.restore(O.serialize(old));assert(restored);assert.equal(restored.riskVersion,0);
 O.togglePause(old);E.togglePause(restored);O.advance(old,120);E.advance(restored,120);
 assert.equal(JSON.stringify(old.series),JSON.stringify(restored.series));assert.equal(old.fish,restored.fish);
 okay(E.nextSession(restored));assert.equal(restored.riskVersion,1);assert.equal(restored.lossAnchor,0);
 const s=ready(E,seed);E.advance(s,15);okay(E.triggerEvent(s,'shark'));E.advance(s,1);assert(s.lossAnchor<0);E.pause(s);
 const r=E.restore(E.serialize(s));assert(r);near(r.lossAnchor,s.lossAnchor);E.togglePause(r);E.togglePause(s);E.advance(s,120);E.advance(r,120);
 assert.equal(JSON.stringify(s.series),JSON.stringify(r.series));assert.equal(s.fish,r.fish);const closed=E.serialize(s);assert(E.finish(s));assert.equal(E.serialize(s),closed);
 okay(E.nextSession(s));assert.equal(s.lossAnchor,0);
}
// Suppress automatic events only in this paired fixture, so only the injected shark differs.
const a=ready(E,37),b=ready(E,37);a.eventCount=b.eventCount=3;E.advance(a,12);E.advance(b,12);b.eventCount=2;okay(E.triggerEvent(b,'shark'));const jump=b.pendingJump;E.advance(a,45);E.advance(b,45);
near(b.lossAnchor,.75*jump);assert(b.price/a.price<.9,'shark must not mean-revert away after 45 seconds');
const corrupt=JSON.parse(E.serialize(b));corrupt.lossAnchor='invalid';assert.equal(E.restore(JSON.stringify(corrupt)),null);
const seen=new Map();for(let seed=1;seed<=10000;seed++){const s=E.newState(seed);const k=s.maps[0].curve.type;seen.set(k,(seen.get(k)||0)+1);}
assert.equal(seen.size,10);assert(seen.get('surge')>1200&&seen.get('surge')<2000);assert(seen.get('bleed')>600);
console.log('PASS 60 legacy v4 continuations, 60 persisted-shark/reload/settlement sessions, persistent negative-event effect, next-session reset, corrupt anchor rejection, and all 10 weighted models across 10000 seeds');
