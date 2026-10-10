'use strict';
const fs=require('fs'),path=require('path'),vm=require('vm'),cp=require('child_process'),assert=require('assert/strict');
const html=fs.readFileSync(path.join(__dirname,'../../player.html'),'utf8'),context={};vm.createContext(context);vm.runInContext(html.match(/\/\* ENGINE_BEGIN[\s\S]*?\*\/([\s\S]*?)\/\* ENGINE_END \*\//)[1]+';globalThis.E=Engine;',context);
const E=context.E,run=cp.spawnSync(process.argv[2],[process.argv[3]],{encoding:'utf8'});if(run.status!==0){process.stdout.write(run.stdout||'');process.stderr.write(run.stderr||'');process.exit(run.status||1);}
const states=new Map();let samples=0,maxError=0;
for(const line of run.stdout.split(/\r?\n/)){
 const [kind,seed,a,b]=line.replace(/\uFEFF/g,'').split(' ');if(kind==='PASS'){console.log(line);continue;}if(!['CURVE','CASH'].includes(kind))continue;
 if(!states.has(seed)){const s=E.newState(Number(seed));assert.equal(E.debugSurvey(s),'');s.stakeDraft=50;assert.equal(E.startSession(s),'');s.gaugeIndex=2;E.advance(s,91);states.set(seed,s);}
 const s=states.get(seed);if(kind==='CURVE'){const p=s.series[Number(a)].p,error=Math.abs(Number(b)-p);maxError=Math.max(maxError,error);assert(error<1e-7,`curve mismatch seed=${seed}, t=${a}: C#=${b}, JS=${p}`);samples++;}else{assert.equal(Number(a),s.fish);assert.equal(Number(b),s.totalPL);}
}
assert.equal(samples,455);console.log(`PASS ${samples} JS/C# price samples and 5 exact settlement ledgers; max error ${maxError}`);
