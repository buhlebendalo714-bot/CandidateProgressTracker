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
  const keyboardCheck=async label=>{
   const fields=await evaluate(`(()=>{const all=[...document.querySelectorAll('main input:not([type="hidden"]),main select,main textarea')];const ids=[...document.querySelectorAll('[id]')].map(e=>e.id);return {unlabelled:all.filter(e=>!e.labels?.length&&!e.getAttribute('aria-label')&&!e.getAttribute('aria-labelledby')).map(e=>e.name),duplicates:ids.filter((id,i)=>ids.indexOf(id)!==i),error:document.querySelector('.validation-summary-errors')?.textContent.trim(),fieldErrors:[...document.querySelectorAll('.field-validation-error')].map(e=>e.textContent.trim())};})()`);
   assert(fields.unlabelled.length===0&&fields.duplicates.length===0,'Unique, labelled fields: '+JSON.stringify(fields));
   assert(fields.error&&fields.error.length>5,'Readable server-side error summary');
   await evaluate(`document.querySelector('.skip-link').focus()`);
   await send('Input.dispatchKeyEvent',{type:'keyDown',key:'Enter',code:'Enter',windowsVirtualKeyCode:13});await send('Input.dispatchKeyEvent',{type:'keyUp',key:'Enter',code:'Enter',windowsVirtualKeyCode:13});
   const reached=[];let submit=false;
   for(let i=0;i<35;i++){
    await send('Input.dispatchKeyEvent',{type:'keyDown',key:'Tab',code:'Tab',windowsVirtualKeyCode:9});await send('Input.dispatchKeyEvent',{type:'keyUp',key:'Tab',code:'Tab',windowsVirtualKeyCode:9});
    const current=await evaluate(`(()=>{const e=document.activeElement,r=e.getBoundingClientRect(),c=getComputedStyle(e);return {tag:e.tagName,name:e.name||e.id||e.textContent.trim().slice(0,60),inside:!!e.closest('main'),outline:c.outlineWidth,top:r.top,bottom:r.bottom,submit:e.matches('main form button[type="submit"]')};})()`);
    if(current.inside){assert(Number.parseFloat(current.outline)>=2,'Visible keyboard focus: '+JSON.stringify(current));reached.push(current);}
    if(current.submit){submit=true;break;}
   }
   assert(submit,'Keyboard reaches the form action without a trap: '+JSON.stringify(reached));await shot(label+'-keyboard');
   await evaluate('window.scrollTo(0,0)');return {fields,reached,submit};
  };
  await send('Page.enable');await send('Runtime.enable');
  await send('Emulation.setDeviceMetricsOverride',{width:1366,height:900,deviceScaleFactor:1,mobile:false});await navigate(); console.log('Visual check: desktop loaded');
  const desktop=await evaluate(`(()=>{const sidebar=document.querySelector('.app-sidebar')?.getBoundingClientRect(),main=document.querySelector('main').getBoundingClientRect();return {title:document.title,sidebarWidth:sidebar?.width||0,sidebarPosition:sidebar?getComputedStyle(document.querySelector('.app-sidebar')).position:"guest",contentLeft:main.left,overflow:document.documentElement.scrollWidth>innerWidth,primaryActions:document.querySelectorAll('main form .btn-primary, main form button:not(.btn)').length,headings:[...document.querySelectorAll('main h2')].map(e=>e.textContent),reviewActions:document.querySelectorAll('.mentor-review').length,changes:document.querySelectorAll('.mentor-changes').length};})()`);
  assert(desktop.sidebarPosition==='guest'||desktop.sidebarPosition==='fixed'&&desktop.sidebarWidth>200&&desktop.contentLeft>=desktop.sidebarWidth,'Desktop shell alignment: '+JSON.stringify(desktop));
  assert(!desktop.overflow,'Desktop overflow: '+JSON.stringify(desktop));
  if(/\/(?:Submit|TeamSubmit|ReviewSubmission)\//.test(process.env.CPT_DASHBOARD_PATH)||process.env.CPT_DASHBOARD_PATH.startsWith('/Account/'))assert(desktop.primaryActions===1,'One primary form action: '+JSON.stringify(desktop));
  await shot('desktop',true);
  await send('Input.dispatchKeyEvent',{type:'keyDown',key:'Tab',code:'Tab',windowsVirtualKeyCode:9});await send('Input.dispatchKeyEvent',{type:'keyUp',key:'Tab',code:'Tab',windowsVirtualKeyCode:9});
  const focus=await evaluate(`({text:document.activeElement.textContent.trim(),outline:getComputedStyle(document.activeElement).outlineWidth,visible:document.activeElement.getBoundingClientRect().top>=0})`);assert(focus.text==='Skip to content'&&focus.outline==='3px'&&focus.visible,'Visible keyboard skip focus');
  const desktopKeyboard=await keyboardCheck('desktop');
  await send('Emulation.setDeviceMetricsOverride',{width:360,height:800,deviceScaleFactor:1,mobile:true});await navigate();
  const mobile=await evaluate(`({width:innerWidth,client:document.documentElement.clientWidth,scroll:document.documentElement.scrollWidth,overflowElements:[...document.querySelectorAll('body *')].filter(e=>{const r=e.getBoundingClientRect();return r.right>document.documentElement.clientWidth+1&&r.left>=0&&getComputedStyle(e).position!=='fixed';}).slice(0,18).map(e=>({tag:e.tagName,cls:e.className,right:e.getBoundingClientRect().right,width:e.getBoundingClientRect().width})),menuVisible:!!document.querySelector('.mobile-menu-toggle')&&getComputedStyle(document.querySelector('.mobile-menu-toggle')).display!=='none',sidebarHidden:!document.querySelector('.app-sidebar')||document.querySelector('.app-sidebar').getBoundingClientRect().right<=1})`);
  assert(mobile.width===360&&mobile.scroll===360&&(desktop.sidebarPosition==='guest'||mobile.menuVisible)&&mobile.sidebarHidden,'Mobile reflow and collapsed navigation: '+JSON.stringify(mobile));await shot('mobile',true);
  const mobileKeyboard=await keyboardCheck('mobile');
  let menu=null,restored=true;
  if(desktop.sidebarPosition!=='guest'){
  await evaluate(`document.querySelector('.mobile-menu-toggle').click()`);
  await evaluate(`new Promise(resolve=>{const panel=document.querySelector('#app-navigation');if(panel.classList.contains('show'))resolve();else panel.addEventListener('shown.bs.offcanvas',resolve,{once:true});})`);
  menu=await evaluate(`({open:document.querySelector('#app-navigation').classList.contains('show'),focusInside:document.querySelector('#app-navigation').contains(document.activeElement)})`);assert(menu.open&&menu.focusInside,'Accessible mobile navigation focus');await shot('mobile-menu');
  await send('Input.dispatchKeyEvent',{type:'keyDown',key:'Escape',code:'Escape',windowsVirtualKeyCode:27});await send('Input.dispatchKeyEvent',{type:'keyUp',key:'Escape',code:'Escape',windowsVirtualKeyCode:27});
  await evaluate(`new Promise(resolve=>{const panel=document.querySelector('#app-navigation');if(!panel.classList.contains('show')&&!panel.classList.contains('hiding'))resolve();else panel.addEventListener('hidden.bs.offcanvas',resolve,{once:true});})`);
  restored=await evaluate(`document.activeElement===document.querySelector('.mobile-menu-toggle')`);assert(restored,'Escape restores mobile trigger focus');}
  assert(errors.length===0,'Browser runtime errors: '+errors.join(','));
  const browser=await send('Browser.getVersion');
  assert(chromePath.toLowerCase().includes('msedge')?browser.userAgent.includes('Edg/'):!browser.userAgent.includes('Edg/'),'Connected browser matches selected executable: '+browser.userAgent);
  const result={browser,desktopKeyboard,mobileKeyboard,desktop,focus,mobile,menu,escapeRestoresFocus:restored,errors};fs.writeFileSync(path.join(root,'checks.json'),JSON.stringify(result,null,2));console.log(JSON.stringify(result));
 }finally{
  if(ws&&ws.readyState===WebSocket.OPEN){
   const closed=new Promise(resolve=>ws.addEventListener('close',resolve,{once:true}));
   if(closeBrowser)closeBrowser().catch(()=>{});else ws.close();
   await closed;
  }
  if(chrome.exitCode===null)chrome.kill();
 }
})().catch(error=>{fs.writeFileSync(path.join(process.env.CPT_DASHBOARD_OUTPUT,'browser-error.txt'),error.stack||String(error));console.error(error);process.exit(1);});
