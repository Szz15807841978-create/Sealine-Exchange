'use strict';
const fs=require('fs'),path=require('path'),vm=require('vm'),cp=require('child_process'),assert=require('assert/strict');
const c={};vm.createContext(c);vm.runInContext(fs.readFileSync(path.join(__dirname,'../player.html'),'utf8').match(/\/\* ENGINE_BEGIN[\s\S]*?\*\/([\s\S]*?)\/\* ENGINE_END \*\//)[1]+';globalThis.E=Engine;',c);const E=c.E;
const run=cp.spawnSync(process.argv[2],[process.argv[3]],{encoding:'utf8',maxBuffer:8*1024*1024});assert.equal(run.status,0,run.stderr);
const states=new Map();let samples=0,ledgers=0,error=0;
for(const row of run.stdout.trim().split(/\r?\n/)){
 const [kind,seed,type,a,b]=row.trim().split(' '),key=seed+':'+type;
 if(!states.has(key)){const s=E.newState(Number(seed));E.debugSurvey(s);E.setShapeChoice(s,Object.keys(E.SHAPES)[Number(type)]);E.startSession(s);s.gaugeIndex=2;E.advance(s,91);states.set(key,s);}
 const s=states.get(key);if(kind==='CURVE'){const delta=Math.abs(s.series[Number(a)].p-Number(b));assert(delta<1e-7,key+' t='+a);error=Math.max(error,delta);samples++;}
 else if(kind==='CASH'){assert.equal(s.fish,Number(a));assert.equal(s.totalPL,Number(b));ledgers++;}else throw new Error(row);
}
assert.equal(samples,10920);assert.equal(ledgers,120);console.log(`PASS all 10 models: ${samples} JS/C# prices, ${ledgers} exact settlements; max error ${error}`);
