/* Runs the actual HTML engine, not a replacement simulation. No browser or live saves. */
'use strict';
const fs=require('fs'),path=require('path'),vm=require('vm'),assert=require('assert/strict');
const root=path.resolve(__dirname,'..');
function engine(file){const c={};vm.createContext(c);vm.runInContext(fs.readFileSync(file,'utf8').match(/\/\* ENGINE_BEGIN[\s\S]*?\*\/([\s\S]*?)\/\* ENGINE_END \*\//)[1]+';globalThis.E=Engine;',c);return c.E;}
const E=engine(path.join(root,'player.html')),old=engine(path.join(__dirname,'fixtures/20261006-before-downside/player.html'));
const count=600,okay=x=>assert.equal(x,''),round=x=>+x.toFixed(2);
function run(api,seed,type='random',skill='normal',strategy='hold',clean=80){
 const s=api.newState(seed);s.clean=clean;s.stakeDraft=50;s.baitDraft=10;
 okay(api.debugSurvey(s));okay(api.setShapeChoice(s,type));okay(api.startSession(s));
 let stopped=false;
 for(let n=0;n<2400&&s.phase!=='closed';n++){
  const g=s.gauge;
  if(g&&!g.verdict&&skill!=='miss'){
   if(g.kind==='speed'&&g.count===0)for(let k=0;k<(skill==='best'?60:24);k++)okay(api.speedPress(s,k%2?'d':'a'));
   if(g.kind!=='speed'){const segment=g.layout.find(x=>x.kind===(skill==='best'?'best':'normal'));g.pos=(segment.from+segment.to)/2;okay(api.judgeGauge(s));}
  }
  if(strategy==='limits'&&!stopped&&s.position&&!api.challengeLocksTrades(s)&&s.elapsed>=1&&(s.price<=85||s.price>=120)){
   api.pause(s);okay(api.collectNet(s));okay(api.togglePause(s));stopped=true;
  }
  api.advance(s,.1);
 }
 assert.equal(s.phase,'closed');assert.equal(s.series.length,91);
 assert.equal(s.fish-s.feeDebt,100+s.totalPL);assert.equal(s.bait,50);assert.equal(s.position,null);
 assert(s.series.every(x=>Number.isFinite(x.p)&&x.p>0));
 let high=100,drawdown=0;for(const x of s.series){high=Math.max(high,x.p);drawdown=Math.max(drawdown,1-x.p/high);}
 return {seed,type:s.shape.type,pl:s.sessionPL,afterBait:s.sessionPL-5,end:s.price-100,drawdown:100*drawdown,series:s.series};
}
function stats(api,type,skill='normal',strategy='hold',clean=80){
 const rows=Array.from({length:count},(_,i)=>run(api,i+1,type,skill,strategy,clean));
 const q=(key,p)=>round(rows.map(x=>x[key]).sort((a,b)=>a-b)[Math.floor((count-1)*p)]);
 return{samples:count,meanPLkg:round(rows.reduce((a,x)=>a+x.pl,0)/count),medianPLkg:q('pl',.5),lossPercent:round(100*rows.filter(x=>x.pl<0).length/count),meanAfterBaitKg:round(rows.reduce((a,x)=>a+x.afterBait,0)/count),p10PLkg:q('pl',.1),p90PLkg:q('pl',.9),medianDrawdownPercent:q('drawdown',.5),endMedianPercent:q('end',.5)};
}
const report={method:'Actual player.html engine; 600 fixed seeds (1..600), water 0, cleanliness 80, stake 50 kg from 100 kg, 10 bait, natural events ON, both challenges played. Normal = neutral timing / 24 speed; best = best timing / 60 speed. Limits = collect all at -15% or +20%, only when trading unlocked. Bait equivalent: 10 units cost 50 currency = 5 kg at island sell price; not charged again by fishing engine.',before:{},after:{},models:{}};
for(const skill of ['normal','best','miss']){report.before[skill]=stats(old,'random',skill);report.after[skill]=stats(E,'random',skill);}
report.before.limits=stats(old,'random','normal','limits');report.after.limits=stats(E,'random','normal','limits');
report.after.dirty=stats(E,'random','normal','hold',20);report.after.clean=stats(E,'random','normal','hold',100);
for(const type of Object.keys(E.SHAPES))report.models[type]={name:E.SHAPES[type].name,...stats(E,type)};
report.examples=Object.keys(E.SHAPES).map(type=>({name:E.SHAPES[type].name,...run(E,37,type)}));
const out=path.join(__dirname,'reports');fs.mkdirSync(out,{recursive:true});fs.writeFileSync(path.join(out,'downside-balance.json'),JSON.stringify(report,null,2));
console.log(JSON.stringify({...report,examples:report.examples.map(({series,...x})=>x)},null,2));
module.exports={engine,run};
