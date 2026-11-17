// Opt-in authenticated visual check. Node built-ins + existing Chrome only.
const fs=require('node:fs'),path=require('node:path'),{spawn}=require('node:child_process');
(async()=>{
 const root=path.resolve(process.env.CPT_DASHBOARD_OUTPUT||'TestResults/staff-dashboard');fs.mkdirSync(root,{recursive:true});
 const chromePath=process.env.CHROME_PATH||'C:/Program Files/Google/Chrome/Application/chrome.exe';
 if(!fs.existsSync(chromePath))throw new Error('Existing Chrome required for opt-in visual verification.');
 const profile=path.join(root,'profile-'+Date.now());
 const chrome=spawn(chromePath,['--headless','--disable-gpu','--no-first-run','--no-default-browser-check','--remote-debugging-port=0','--user-data-dir='+profile,'about:blank'],{windowsHide:true,stdio:'ignore'});
 let ws,closeBrowser;
 try{
  let port;const deadline=Date.now()+25000;
  while(Date.now()<deadline){try{port=Number(fs.readFileSync(path.join(profile,'DevToolsActivePort'),'utf8').split('\n')[0]);if(port>0)break;}catch{}await new Promise(r=>setTimeout(r,100));}
  if(!port)throw new Error('Owned browser did not publish its debugging endpoint.');console.log('Visual check: owned browser ready');
  const target=await(await fetch('http://127.0.0.1:'+port+'/json/new?about:blank',{method:'PUT'})).json();
  ws=new WebSocket(target.webSocketDebuggerUrl);await new Promise((resolve,reject)=>{ws.onopen=resolve;ws.onerror=reject;});
  let sequence=0;const pending=new Map(),events=new Map(),errors=[];
  ws.onmessage=event=>{const m=JSON.parse(event.data);if(m.id){const p=pending.get(m.id);pending.delete(m.id);m.error?p.reject(new Error(JSON.stringify(m.error))):p.resolve(m.result);}else{if(m.method==='Runtime.exceptionThrown')errors.push(m.params.exceptionDetails.text);if(events.has(m.method)){events.get(m.method)();events.delete(m.method);}}};
  const send=(method,params={})=>new Promise((resolve,reject)=>{const id=++sequence;pending.set(id,{resolve,reject});ws.send(JSON.stringify({id,method,params}));});
  closeBrowser=()=>send("Browser.close");
  const evaluate=async expression=>{const r=await send('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(r.exceptionDetails)throw new Error(JSON.stringify(r.exceptionDetails));return r.result.value;};
  const navigate=async()=>{const loaded=new Promise(r=>events.set('Page.loadEventFired',r));await send('Page.navigate',{url:'http://127.0.0.1:7259'+process.env.CPT_DASHBOARD_PATH});await loaded;};
  const shot=async(name,full=false)=>{const {cssContentSize:size}=await send('Page.getLayoutMetrics');const options={format:'png',captureBeyondViewport:full};if(full)options.clip={x:0,y:0,width:size.width,height:Math.min(size.height,3200),scale:1};const r=await send('Page.captureScreenshot',options);fs.writeFileSync(path.join(root,name+'.png'),Buffer.from(r.data,'base64'));};
  const assert=(value,message)=>{if(!value)throw new Error(message);};
  await send('Page.enable');await send('Runtime.enable');
  const checks=[];
  for(const width of [1366,360]){
   await send('Emulation.setDeviceMetricsOverride',{width,height:900,deviceScaleFactor:1,mobile:false});
   await navigate();
   const state=await evaluate(`(()=>{const main=document.querySelector('main');return {heading:main.querySelector('h1')?.textContent.trim(),hasContent:main.innerText.length>30,overflow:document.documentElement.scrollWidth>innerWidth+1,developerCopy:/future interval|authoritative roster|not yet implemented|Row 0/.test(main.innerText),secretRedacted:!main.querySelector('code')||[...main.querySelectorAll('code')].every(e=>e.textContent==='[code removed before browser review]')};})()`);
   assert(state.hasContent&&!state.overflow&&!state.developerCopy&&state.secretRedacted,'Communication/reflow check failed');
   await shot('screen-'+width,true);checks.push({width,...state});
  }
  fs.writeFileSync(path.join(root,'checks.json'),JSON.stringify({checks,transport:'Authenticated Razor response replay; code removed before bridge',credentialsCaptured:false,errors},null,2));
 }finally{
  if(ws&&ws.readyState===WebSocket.OPEN){
   const closed=new Promise(resolve=>ws.addEventListener('close',resolve,{once:true}));
   if(closeBrowser)closeBrowser().catch(()=>{});else ws.close();
   await closed;
  }
  if(chrome.exitCode===null)chrome.kill();
 }
})().catch(error=>{fs.writeFileSync(path.join(process.env.CPT_DASHBOARD_OUTPUT,'browser-error.txt'),error.stack||String(error));console.error(error);process.exit(1);});
