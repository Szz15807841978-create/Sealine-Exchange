/* node tools/player-check.js — player-only engine, hidden-information and browser regressions. */
'use strict';
const fs=require('fs');
const path=require('path');
const vm=require('vm');
const http=require('http');
const assert=require('assert/strict');
const {pathToFileURL}=require('url');
const root=path.resolve(__dirname,'..');
const html=fs.readFileSync(path.join(root,'player.html'),'utf8');
const original=fs.readFileSync(path.join(root,'index.html'),'utf8');
const near=(a,b)=>assert(Math.abs(a-b)<1e-8,`${a} != ${b}`);
const okay=value=>assert.equal(value,'');
function engine(source){const context={};vm.createContext(context);vm.runInContext(source.match(/\/\* ENGINE_BEGIN[\s\S]*?\*\/([\s\S]*?)\/\* ENGINE_END \*\//)[1]+';globalThis.E=Engine;',context);return context.E;}
const E=engine(html),O=engine(original);
// Reuse every existing accounting/model regression, changing only the two player difficulty constants.
let regression=fs.readFileSync(path.join(__dirname,'smoke-test.js'),'utf8');
const sourcePath="path.join(__dirname,'..','index.html')";
assert(regression.includes(sourcePath));
regression=regression.replace(sourcePath,"path.join(__dirname,'..','player.html')");
const oldSpeed="near(E.speedGain(0),-.12);near(E.speedGain(6),-.06);near(E.speedGain(12),0);near(E.speedGain(24),.09);near(E.speedGain(36),.18);near(E.speedGain(100),.18);";
assert(regression.includes(oldSpeed));
regression=regression.replace(oldSpeed,"near(E.speedGain(0),-.12);near(E.speedGain(12),-.06);near(E.speedGain(24),0);near(E.speedGain(42),.09);near(E.speedGain(60),.18);near(E.speedGain(100),.18);");
vm.runInNewContext(regression,{require,__dirname,console},{filename:'player-engine-regression.js'});
assert.equal(E.CFG.SPEED_WINDOW,6);assert.equal(E.CFG.SPEED_BREAK_EVEN,24);assert.equal(E.CFG.SPEED_MAX_COUNT,60);
assert.equal(O.CFG.SPEED_BREAK_EVEN,12);assert.equal(O.CFG.SPEED_MAX_COUNT,36);
for(const key of Object.keys(O.CFG))if(!['SPEED_BREAK_EVEN','SPEED_MAX_COUNT'].includes(key))assert.equal(E.CFG[key],O.CFG[key],key);
for(let seed=1;seed<=30;seed++){
 const a=E.newState(seed),b=O.newState(seed);
 for(const [eng,s] of [[E,a],[O,b]]){okay(eng.debugSurvey(s));okay(eng.startSession(s));s.gaugeIndex=2;eng.advance(s,91);}
 assert.equal(JSON.stringify(a.series),JSON.stringify(b.series));assert.equal(a.fish,b.fish);assert.equal(a.totalPL,b.totalPL);
}
console.log('PASS player difficulty increased; all other model constants and 30 seeded trajectories unchanged');
if(process.argv.includes('--engine-only'))process.exit(0);
const {chromium}=require('playwright');

let passed=0;const errors=[],shots=path.join(__dirname,'shots','player');
fs.mkdirSync(shots,{recursive:true});
const pass=name=>{passed++;console.log('PASS',name);};
const server=http.createServer((req,res)=>{if(req.url==='/favicon.ico'){res.writeHead(204);res.end();return;}const file=req.url==='/index.html'?'index.html':'player.html';res.writeHead(200,{'Content-Type':'text/html; charset=utf-8'});res.end(fs.readFileSync(path.join(root,file)));});
const forbidden=/最适|最合适|最佳诱饵|适宜度|增益|调节基准|σ|GARCH|噪声|形态|行为反馈|次保本|√秒|公式|调试/;
async function privateUI(page){
 assert.doesNotMatch(await page.locator('body').innerText(),forbidden);
 const attrs=await page.locator('[title],[aria-label]').evaluateAll(els=>els.filter(el=>!el.closest('#debugDialog')).map(el=>(el.title||'')+' '+(el.getAttribute('aria-label')||'')).join('\n'));
 assert.doesNotMatch(attrs,forbidden);
}
async function openDebug(page){await page.keyboard.press('Control+Shift+KeyD');assert(await page.locator('#debugDialog').isVisible());}
async function closeDebug(page){await page.locator('#debugClose').click();}
async function speedFixture(page){
 await page.evaluate(()=>{const {Engine:E,state:s}=FishingDemo;if(s.phase==='closed')E.nextSession(s);if(s.phase==='preopen'){E.debugSurvey(s);E.startSession(s);}s.gauge=null;s.gaugeIndex=1;E.openGauge(s);E.pause(s);FishingDemo.refresh();});
 await page.locator('#pauseBtn').click();
}
(async()=>{
 let browser;
 try{
  await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
  const url='http://127.0.0.1:'+server.address().port;
  browser=await chromium.launch({channel:'msedge',headless:true});
  const context=await browser.newContext({viewport:{width:1440,height:980},deviceScaleFactor:1});
  // Freeze only the animation scheduler for reproducible input/elapsed-time fixtures.
  await context.addInitScript(()=>{window.requestAnimationFrame=()=>0;});
  const page=await context.newPage();page.on('pageerror',e=>errors.push(String(e)));
  await page.goto(url+'/player.html');await page.waitForFunction(()=>!!window.FishingDemo);
  assert.equal(await page.locator('#debugBtn').count(),0);assert.equal(await page.locator('#baitCurve').count(),0);assert.equal(await page.locator('#baitOptimum').count(),0);assert.equal(await page.locator('#volatilityText').count(),0);assert.equal(await page.locator('#npcQuality').count(),0);
  assert.equal(await page.locator('#debugDialog').isVisible(),false);await privateUI(page);pass('no player-facing debugging, optimum, coefficient or model widgets');
  await page.keyboard.press('d');assert.equal(await page.locator('#debugDialog').isVisible(),false);pass('plain D no longer exposes debugging');
  await page.locator('#helpBtn').click();await privateUI(page);assert.match(await page.locator('#helpDialog').innerText(),/0.5%/);assert.match(await page.locator('#helpDialog').innerText(),/次数太少会有损失/);await page.locator('#helpClose').click();pass('help explains actions and costs without revealing optimal conditions or reward numbers');
  for(let i=0;i<3;i++){
   await page.locator(`[data-map="${i}"]`).click();await page.locator('#surveyBtn').click();await page.locator('#diveDown').click();for(let n=0;n<=i;n++)await page.locator('#diveScan').click();await page.locator('#diveReturn').click();
  }
  assert.equal(await page.locator('.map-fish').filter({hasText:'估计鱼量'}).count(),3);assert.match(await page.locator('#mapList').innerText(),/相对鱼量/);assert.doesNotMatch(await page.locator('#mapList').innerText(),/精度|%|基准/);pass('three-water surveys retain comparable fish estimates without precision/engine factors');
  await page.locator('#observeBtn').click();await page.locator('#npcBtn').click();await page.locator('#cultivateBtn').click();await page.waitForFunction(()=>!document.getElementById('toast').classList.contains('on'),null,{polling:100});
  assert.match(await page.locator('#tempValue').innerText(),/\d/);assert.match(await page.locator('#windValue').innerText(),/\d/);assert.match(await page.locator('#npcText').innerText(),/“/);await privateUI(page);pass('real observations and NPC rumors remain, cultivation has no quantified benefit');
  await page.screenshot({path:path.join(shots,'01-player-preopen.png'),fullPage:true});
  await openDebug(page);await page.locator('#debugFish').fill('900');await page.locator('#giveFish').click();await page.locator('#debugBait').fill('100');await page.locator('#giveBait').click();assert.equal(await page.evaluate(()=>FishingDemo.state.fish),1000);assert.equal(await page.evaluate(()=>FishingDemo.state.bait),155);
  await page.locator('#debugReveal').check();assert.match(await page.locator('#debugSecrets').innerText(),/24 次零增益，60 次满增益/);await closeDebug(page);await page.waitForFunction(()=>!document.getElementById('toast').classList.contains('on'),null,{polling:100});await privateUI(page);pass('secret shortcut exposes working resource controls and optional hidden parameters, not the main screen');
  await page.evaluate(()=>FishingDemo.save());await page.reload();assert.equal(await page.locator('#debugDialog').isVisible(),false);await privateUI(page);assert.equal(await page.evaluate(()=>FishingDemo.state.fish),1000);pass('debug reveal never appears outside its modal, including after reload');
  const old=await context.newPage();await old.goto(url+'/index.html');assert.equal(await old.evaluate(()=>FishingDemo.state.fish),100);assert.equal(await old.evaluate(()=>FishingDemo.Engine.CFG.SPEED_MAX_COUNT),36);const originalSave=await old.evaluate(()=>localStorage.getItem('fishing-weight-market-v4'));await old.close();pass('same-origin original version keeps its own balances, save and easier difficulty');
  await page.locator('#maxStake').click();await page.locator('#startBtn').click();await page.locator('#pauseBtn').click();assert.equal(await page.evaluate(()=>FishingDemo.state.fish),0);
  await page.locator('[data-percent="50"]').click();await page.locator('#collectBtn').click();await page.locator('#stakeInput').fill('500');assert.equal(await page.locator('#addWeightBtn').isDisabled(),false);await page.locator('#addWeightBtn').click();assert.equal(await page.evaluate(()=>FishingDemo.state.fish),0);assert.equal(await page.locator('#positionStake').innerText(),'1,000 kg');assert(await page.locator('#collectBtn').isDisabled());pass('T+0 partial harvest immediately reinvests all proceeds while preserving harvest cooldown');
  await page.evaluate(()=>{const {Engine:E,state:s}=FishingDemo;s.gaugeIndex=2;E.togglePause(s);E.advance(s,6);E.pause(s);FishingDemo.refresh();});await page.locator('[data-percent="100"]').click();await page.locator('#collectBtn').click();await page.locator('#maxStake').click();await page.locator('#castBtn').click();assert(await page.evaluate(()=>!!FishingDemo.state.position));assert(await page.evaluate(()=>FishingDemo.state.cd>0));assert(await page.locator('#collectBtn').isDisabled());pass('T+0 full harvest enables immediate recast, principal only and deferred fees intact');
  await page.evaluate(()=>{const {Engine:E,state:s}=FishingDemo;s.gaugeIndex=0;E.openGauge(s);E.togglePause(s);s.gauge.pos=50;E.judgeGauge(s);E.pause(s);FishingDemo.refresh();});assert.match(await page.locator('#gaugeText').innerText(),/停针结果/);assert.doesNotMatch(await page.locator('#gauge').innerText(),/%/);await privateUI(page);pass('timing-gauge result and radio provide qualitative feedback only');
  await speedFixture(page);await page.locator('#stakeInput').focus();await page.keyboard.press('a');await page.keyboard.press('a');await page.keyboard.down('d');await page.keyboard.down('d');await page.keyboard.up('d');assert.equal(await page.evaluate(()=>FishingDemo.state.gauge.count),2);assert.equal(await page.locator('#debugDialog').isVisible(),false);pass('A/D alternation still rejects repeated keys and long press without accidental trades/debug');
  for(let i=2;i<24;i++)await page.keyboard.press(i%2?'d':'a');assert.equal(await page.locator('#speedEffect').innerText(),'稳住了');assert.equal(await page.evaluate(()=>FishingDemo.Engine.speedGain(FishingDemo.state.gauge.count)),0);await privateUI(page);pass('24 valid alternating presses are now the neutral threshold, no numeric reward shown');
  await page.evaluate(()=>{FishingDemo.Engine.advance(FishingDemo.state,2);FishingDemo.Engine.pause(FishingDemo.state);FishingDemo.refresh();FishingDemo.save();});await page.reload();assert.equal(await page.evaluate(()=>FishingDemo.state.gauge.count),24);assert.equal(await page.evaluate(()=>FishingDemo.state.gauge.u),2);assert.equal(await page.evaluate(()=>FishingDemo.state.gauge.lastKey),'d');await page.keyboard.press('a');assert.equal(await page.evaluate(()=>FishingDemo.state.gauge.count),24);await page.locator('#pauseBtn').click();pass('speed progress survives reload; pause blocks counting');
  for(let i=24;i<60;i++)await page.keyboard.press(i%2?'d':'a');assert.equal(await page.locator('#speedEffect').innerText(),'势不可挡');assert.equal(await page.locator('#speedCount').innerText(),'60');await page.evaluate(()=>{FishingDemo.Engine.advance(FishingDemo.state,4);FishingDemo.Engine.pause(FishingDemo.state);FishingDemo.refresh();});near(await page.evaluate(()=>FishingDemo.state.gauge.gain),.18);assert(await page.locator('#endBtn').isDisabled());await privateUI(page);assert.doesNotMatch(await page.locator('#gauge').innerText(),/%/);await page.screenshot({path:path.join(shots,'02-player-speed.png'),fullPage:true});pass('60 alternating presses give the capped reward, with only qualitative result visible');
  await page.evaluate(()=>{const {Engine:E,state:s}=FishingDemo;E.togglePause(s);E.advance(s,2);E.pause(s);s.eventCount=0;E.triggerEvent(s,'shark');E.triggerEvent(s,'weather');FishingDemo.refresh();});assert.match(await page.locator('#eventList').innerText(),/鲨鱼/);assert.doesNotMatch(await page.locator('#eventList').innerText(),/跳跃|负向|增益|后续行情|形态|行为反馈/);await privateUI(page);pass('natural events and radio do not leak jump magnitude or hidden behavior causes');
  await page.locator('#endBtn').click();const ledger=await page.evaluate(()=>{const s=FishingDemo.state;return{left:s.fish-s.feeDebt,right:100+s.giftFish+s.totalPL,fees:s.result.fees,expected:Math.floor((s.sessionBuyWeight+s.sessionSellWeight)/200)};});assert.equal(ledger.left,ledger.right);assert.equal(ledger.fees,ledger.expected);await privateUI(page);pass('integer weight ledger and close-only 0.5% fees still balance');
  await page.locator('#nextBtn').click();await page.setViewportSize({width:390,height:844});assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),false);await page.screenshot({path:path.join(shots,'03-player-mobile.png'),fullPage:true});
  for(let n=0;n<5;n++)await page.locator('#brandMark').click();assert(await page.locator('#debugDialog').isVisible());await page.screenshot({path:path.join(shots,'04-hidden-debug-mobile.png'),fullPage:true});await closeDebug(page);pass('390px mobile fits and secret five-tap logo entry reaches debug controls');
  await speedFixture(page);await page.locator('#speedA').click();await page.locator('#speedA').click();await page.locator('#speedD').click();assert.equal(await page.evaluate(()=>FishingDemo.state.gauge.count),2);await page.evaluate(()=>{FishingDemo.Engine.advance(FishingDemo.state,6);FishingDemo.Engine.pause(FishingDemo.state);FishingDemo.refresh();});assert(await page.evaluate(()=>FishingDemo.state.gauge.gain<0));assert.equal(await page.locator('#speedEffect').innerText(),'力道不足');assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth+1),false);await privateUI(page);await page.locator('#gauge').screenshot({path:path.join(shots,'05-mobile-speed.png')});pass('mobile tapping obeys the same harder threshold and shows qualitative penalty');
  await openDebug(page);page.once('dialog',d=>d.accept());await page.locator('#resetBtn').click();assert.equal(await page.evaluate(()=>FishingDemo.state.fish),100);assert.equal(await page.evaluate(()=>localStorage.getItem('fishing-weight-market-v4')),originalSave);pass('player reset cannot overwrite or remove original-version save');
  const offline=await context.newPage();offline.on('pageerror',e=>errors.push(String(e)));await offline.goto(pathToFileURL(path.join(root,'player.html')).href);assert.equal(await offline.locator('.map-card').count(),3);await privateUI(offline);await openDebug(offline);await offline.locator('#giveFish').click();assert.equal(await offline.evaluate(()=>FishingDemo.state.fish),1100);pass('standalone file:// player works without server or external dependencies');
  // A separate normal animation-loop smoke verifies the real timer is not disabled by the player build.
  const live=await browser.newPage();live.on('pageerror',e=>errors.push(String(e)));await live.goto(url+'/player.html');await live.evaluate(()=>{const {Engine:E,state:s}=FishingDemo;E.debugSurvey(s);E.startSession(s);s.gaugeIndex=1;E.openGauge(s);FishingDemo.refresh();});await live.waitForFunction(()=>FishingDemo.state.gauge?.kind==='speed'&&FishingDemo.state.gauge.u>.2);assert(await live.evaluate(()=>FishingDemo.state.elapsed===0));await live.close();pass('real animation loop advances the challenge timer while freezing market time');
  assert.deepEqual(errors,[]);pass('no uncaught browser JavaScript errors');
  console.log(`\n${passed} player browser checks passed. Screenshots: ${shots}`);
 }finally{if(browser)await browser.close();await new Promise(resolve=>server.close(resolve));}
})().catch(e=>{console.error(e);process.exitCode=1;});
