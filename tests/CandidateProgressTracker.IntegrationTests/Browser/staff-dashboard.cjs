// Opt-in authenticated visual check. Node built-ins + existing Chrome only.
const fs=require('node:fs'),path=require('node:path'),{spawn}=require('node:child_process');
(async()=>{
 const root=path.resolve(process.env.CPT_DASHBOARD_OUTPUT||'TestResults/staff-dashboard');fs.mkdirSync(root,{recursive:true});
 const chromePath=process.env.CHROME_PATH||'C:/Program Files/Google/Chrome/Application/chrome.exe';
 if(!fs.existsSync(chromePath))throw new Error('Existing Chrome required for opt-in visual verification.');
 const chrome=spawn(chromePath,['--headless','--disable-gpu','--no-first-run','--no-default-browser-check','--remote-debugging-port=9227','--user-data-dir='+path.join(root,'profile-'+Date.now()),'about:blank'],{windowsHide:true,stdio:'ignore'});
 let ws;
 try{
  let ready=false;const deadline=Date.now()+25000;
  while(Date.now()<deadline){try{await(await fetch('http://127.0.0.1:9227/json/version')).json();ready=true;break;}catch{await new Promise(r=>setTimeout(r,100));}}
  if(!ready)throw new Error('Chrome did not become ready.'); console.log('Visual check: Chrome ready');
  const target=await(await fetch('http://127.0.0.1:9227/json/new?about:blank',{method:'PUT'})).json();
  ws=new WebSocket(target.webSocketDebuggerUrl);await new Promise((resolve,reject)=>{ws.onopen=resolve;ws.onerror=reject;});
  let sequence=0;const pending=new Map(),events=new Map(),errors=[];
  ws.onmessage=event=>{const m=JSON.parse(event.data);if(m.id){const p=pending.get(m.id);pending.delete(m.id);m.error?p.reject(new Error(JSON.stringify(m.error))):p.resolve(m.result);}else{if(m.method==='Runtime.exceptionThrown')errors.push(m.params.exceptionDetails.text);if(events.has(m.method)){events.get(m.method)();events.delete(m.method);}}};
  const send=(method,params={})=>new Promise((resolve,reject)=>{const id=++sequence;pending.set(id,{resolve,reject});ws.send(JSON.stringify({id,method,params}));});
  const evaluate=async expression=>{const r=await send('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(r.exceptionDetails)throw new Error(JSON.stringify(r.exceptionDetails));return r.result.value;};
  const navigate=async()=>{const loaded=new Promise(r=>events.set('Page.loadEventFired',r));await send('Page.navigate',{url:'http://127.0.0.1:7259'+process.env.CPT_DASHBOARD_PATH});await loaded;};
  const shot=async(name,full=false)=>{const {cssContentSize:size}=await send('Page.getLayoutMetrics');const options={format:'png',captureBeyondViewport:full};if(full)options.clip={x:0,y:0,width:size.width,height:Math.min(size.height,3200),scale:1};const r=await send('Page.captureScreenshot',options);fs.writeFileSync(path.join(root,name+'.png'),Buffer.from(r.data,'base64'));};
  const assert=(value,message)=>{if(!value)throw new Error(message);};
  await send('Page.enable');await send('Runtime.enable');
  await send('Emulation.setDeviceMetricsOverride',{width:1440,height:1100,deviceScaleFactor:1,mobile:false});await navigate(); console.log('Visual check: desktop loaded');
  const desktop=await evaluate(`(()=>{const sidebar=document.querySelector('.app-sidebar').getBoundingClientRect(),main=document.querySelector('main').getBoundingClientRect();return {title:document.title,sidebarWidth:sidebar.width,sidebarPosition:getComputedStyle(document.querySelector('.app-sidebar')).position,contentLeft:main.left,overflow:document.documentElement.scrollWidth>innerWidth,primaryActions:document.querySelectorAll('.page-heading .btn-primary').length,headings:[...document.querySelectorAll('main h2')].map(e=>e.textContent),reviewActions:document.querySelectorAll('.mentor-review').length,changes:document.querySelectorAll('.mentor-changes').length};})()`);
  assert(desktop.sidebarPosition==='fixed'&&desktop.sidebarWidth>200&&desktop.contentLeft>=desktop.sidebarWidth,'Desktop shell alignment: '+JSON.stringify(desktop));
  assert(!desktop.overflow&&desktop.primaryActions===1,'Desktop overflow/primary action: '+JSON.stringify(desktop));
  if(process.env.CPT_DASHBOARD_PATH.startsWith('/Mentor'))assert(desktop.reviewActions===1&&desktop.changes===1&&desktop.headings.includes('Cohort progress'),'Scoped mentor content');
  else assert(desktop.headings.includes('Accounts and access')&&desktop.headings.includes('Courses and projects')&&desktop.headings.includes('History and accountability'),'Admin goal groups');
  await shot('desktop',true);
  await send('Input.dispatchKeyEvent',{type:'keyDown',key:'Tab',code:'Tab',windowsVirtualKeyCode:9});await send('Input.dispatchKeyEvent',{type:'keyUp',key:'Tab',code:'Tab',windowsVirtualKeyCode:9});
  const focus=await evaluate(`({text:document.activeElement.textContent.trim(),outline:getComputedStyle(document.activeElement).outlineWidth,visible:document.activeElement.getBoundingClientRect().top>=0})`);assert(focus.text==='Skip to content'&&focus.outline==='3px'&&focus.visible,'Visible keyboard skip focus');
  await send('Emulation.setDeviceMetricsOverride',{width:390,height:844,deviceScaleFactor:1,mobile:true});await navigate();
  const mobile=await evaluate(`({width:innerWidth,client:document.documentElement.clientWidth,scroll:document.documentElement.scrollWidth,overflowElements:[...document.querySelectorAll('body *')].filter(e=>{const r=e.getBoundingClientRect();return r.right>document.documentElement.clientWidth+1&&r.left>=0&&getComputedStyle(e).position!=='fixed';}).slice(0,18).map(e=>({tag:e.tagName,cls:e.className,right:e.getBoundingClientRect().right,width:e.getBoundingClientRect().width})),menuVisible:getComputedStyle(document.querySelector('.mobile-menu-toggle')).display!=='none',sidebarHidden:document.querySelector('.app-sidebar').getBoundingClientRect().right<=1})`);
  assert(mobile.width===390&&mobile.scroll===390&&mobile.menuVisible&&mobile.sidebarHidden,'Mobile reflow and collapsed navigation: '+JSON.stringify(mobile));await shot('mobile',true);
  await evaluate(`document.querySelector('.mobile-menu-toggle').click()`);
  await evaluate(`new Promise(resolve=>{const panel=document.querySelector('#app-navigation');if(panel.classList.contains('show'))resolve();else panel.addEventListener('shown.bs.offcanvas',resolve,{once:true});})`);
  const menu=await evaluate(`({open:document.querySelector('#app-navigation').classList.contains('show'),focusInside:document.querySelector('#app-navigation').contains(document.activeElement)})`);assert(menu.open&&menu.focusInside,'Accessible mobile navigation focus');await shot('mobile-menu');
  await send('Input.dispatchKeyEvent',{type:'keyDown',key:'Escape',code:'Escape',windowsVirtualKeyCode:27});await send('Input.dispatchKeyEvent',{type:'keyUp',key:'Escape',code:'Escape',windowsVirtualKeyCode:27});
  await evaluate(`new Promise(resolve=>{const panel=document.querySelector('#app-navigation');if(!panel.classList.contains('show')&&!panel.classList.contains('hiding'))resolve();else panel.addEventListener('hidden.bs.offcanvas',resolve,{once:true});})`);
  const restored=await evaluate(`document.activeElement===document.querySelector('.mobile-menu-toggle')`);assert(restored,'Escape restores mobile trigger focus');assert(errors.length===0,'Browser runtime errors: '+errors.join(','));
  const result={desktop,focus,mobile,menu,escapeRestoresFocus:restored,errors};fs.writeFileSync(path.join(root,'checks.json'),JSON.stringify(result,null,2));console.log(JSON.stringify(result));await send('Browser.close');ws.close();
 }finally{if(ws)ws.close();if(chrome.exitCode===null)chrome.kill();}
})().catch(error=>{fs.writeFileSync(path.resolve('TestResults/dashboard-visual/browser-error.txt'),error.stack||String(error));console.error(error);process.exit(1);});
