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
  const desktop=await evaluate(`(()=>{const sidebar=document.querySelector('.app-sidebar')?.getBoundingClientRect(),main=document.querySelector('main').getBoundingClientRect();return {title:document.title,sidebarWidth:sidebar?.width||0,sidebarPosition:sidebar?getComputedStyle(document.querySelector('.app-sidebar')).position:"guest",contentLeft:main.left,overflow:document.documentElement.scrollWidth>innerWidth,primaryActions:document.querySelectorAll('main form .btn-primary, main form button:not(.btn)').length,headings:[...document.querySelectorAll('main h2')].map(e=>e.textContent),reviewActions:document.querySelectorAll('.mentor-review').length,changes:document.querySelectorAll('.mentor-changes').length};})()`);
  assert(desktop.sidebarPosition==='guest'||desktop.sidebarPosition==='fixed'&&desktop.sidebarWidth>200&&desktop.contentLeft>=desktop.sidebarWidth,'Desktop shell alignment: '+JSON.stringify(desktop));
  assert(!desktop.overflow,'Desktop overflow: '+JSON.stringify(desktop));
  if(/\/(?:Submit|TeamSubmit|ReviewSubmission)\//.test(process.env.CPT_DASHBOARD_PATH)||process.env.CPT_DASHBOARD_PATH.startsWith('/Account/'))assert(desktop.primaryActions===1,'One primary form action: '+JSON.stringify(desktop));
  const brand=await evaluate(`(()=>{
   const logo=document.querySelector('.sidebar-brand img'),box=logo.getBoundingClientRect(),frame=document.querySelector('.dashboard-workspace'),main=document.querySelector('main');
   const rgb=s=>s.match(/[0-9.]+/g).slice(0,3).map(Number);
   const luminance=c=>c.map(v=>{v/=255;return v<=.04045?v/12.92:((v+.055)/1.055)**2.4}).reduce((a,v,i)=>a+v*[.2126,.7152,.0722][i],0);
   const contrast=(a,b)=>{a=luminance(a);b=luminance(b);return (Math.max(a,b)+.05)/(Math.min(a,b)+.05)};
   const mix=(a,b,t)=>a.map((v,i)=>v*t+b[i]*(1-t));
   const purple=[64,56,95],pale=[239,235,251],coral=[242,82,81];
   const surfaces=[purple,mix(pale,purple,.12),mix(coral,purple,.22),mix(coral,mix(pale,purple,.07),.22),mix(coral,mix(pale,purple,.12),.28)];
   const text=[...document.querySelectorAll('.page-heading h1,.page-heading .page-description,.app-breadcrumb a,.app-breadcrumb li')].map(e=>({text:e.textContent.trim(),ratio:Math.min(...surfaces.map(b=>contrast(rgb(getComputedStyle(e).color),b)))}));
   return {loaded:logo.complete&&logo.naturalWidth>0,alt:logo.alt,aspect:box.width/box.height,naturalAspect:logo.naturalWidth/logo.naturalHeight,width:box.width,link:logo.closest('a').getAttribute('href'),duplicate:logo.closest('a').textContent.trim(),brandSurface:getComputedStyle(document.querySelector('.sidebar-brand')).backgroundColor,background:getComputedStyle(frame).backgroundImage,coversMain:frame.getBoundingClientRect().bottom>=main.getBoundingClientRect().bottom,title:document.title,text,cards:[...document.querySelectorAll('.work-panel,.progress-panel,.history-panel,.feedback-panel')].map(e=>getComputedStyle(e).backgroundColor)};
  })()`);
  assert(brand.loaded&&brand.alt==='CAPACITI'&&brand.width>=180&&Math.abs(brand.aspect-brand.naturalAspect)<.01,'Logo loads without distortion: '+JSON.stringify(brand));
  assert(brand.duplicate===''&&brand.brandSurface==='rgb(255, 255, 255)','Single wordmark on white brand area');
  assert(brand.link.split('?')[0].replace(/\/Index$/,'').toLowerCase()===process.env.CPT_DASHBOARD_PATH.split('?')[0].toLowerCase(),'Role dashboard logo destination');
  assert(brand.title.endsWith(' - CAPACITI')&&brand.background.includes('linear-gradient')&&brand.coversMain,'Brand title and full workspace background');
  assert(brand.text.every(e=>e.ratio>=4.5),'Readable exterior text: '+JSON.stringify(brand));
  const watermarkInfo=()=>evaluate(`(()=>{const s=getComputedStyle(document.querySelector('.dashboard-workspace'),'::before');return {position:s.position,left:s.left,top:s.top,width:s.width,height:s.height,size:s.backgroundSize,center:s.backgroundPosition,opacity:Number(s.opacity),pointer:s.pointerEvents,transform:s.transform,animation:s.animationName,image:s.backgroundImage};})()`);
  const watermark=await watermarkInfo();
  assert(watermark.position==='fixed'&&watermark.opacity>=.05&&watermark.opacity<=.08&&watermark.pointer==='none'&&watermark.transform==='none'&&watermark.animation==='none'&&watermark.size==='80% auto'&&watermark.center==='50% 50%'&&Number.parseFloat(watermark.left)===desktop.sidebarWidth,'Fixed decorative workspace watermark: '+JSON.stringify(watermark));
  const watermarkImage=await evaluate(`new Promise((resolve,reject)=>{const i=new Image();i.onload=()=>resolve({width:i.naturalWidth,height:i.naturalHeight});i.onerror=()=>reject(new Error('Watermark image unavailable'));i.src=getComputedStyle(document.querySelector('.dashboard-workspace'),'::before').backgroundImage.slice(5,-2);})`);
  assert(Math.abs(watermarkImage.width/watermarkImage.height-brand.naturalAspect)<.01,'Original watermark aspect ratio');
  const frost=await evaluate(`(()=>{
   const rgba=s=>s.match(/[0-9.]+/g).map(Number);
   const luminance=c=>c.map(v=>{v/=255;return v<=.04045?v/12.92:((v+.055)/1.055)**2.4}).reduce((a,v,i)=>a+v*[.2126,.7152,.0722][i],0);
   const ratio=(a,b)=>(Math.max(luminance(a),luminance(b))+.05)/(Math.min(luminance(a),luminance(b))+.05);
   const surface=e=>{const c=getComputedStyle(e),bg=rgba(c.backgroundColor),a=bg[3]??1;return {bg:c.backgroundColor,alpha:a,blur:c.backdropFilter,radius:c.borderTopLeftRadius,opacity:c.opacity,contrast:ratio(rgba(c.color).slice(0,3),bg.slice(0,3).map((v,i)=>v*a+[29,41,81][i]*(1-a)))};};
   return {supported:CSS.supports('backdrop-filter','blur(14px)'),panels:[...document.querySelectorAll('.work-panel,.progress-panel,.history-panel,.feedback-panel')].map(surface),bar:surface(document.querySelector('.app-topbar')),controls:[...document.querySelectorAll('main select,main input,main form.workspace-panel')].map(surface),tables:[...document.querySelectorAll('main table,main th,main td,.history-panel .table-responsive')].map(e=>({alpha:rgba(getComputedStyle(e).backgroundColor)[3]??1})),opacity:[...document.querySelectorAll('.work-panel,.progress-panel,.history-panel')].map(e=>getComputedStyle(e).opacity)};
  })()`);
  assert(frost.panels.every(p=>p.alpha>=(frost.supported ? .88 : 1)&&p.alpha<=(frost.supported ? .92 : 1)&&p.radius==='12px'&&p.opacity==='1'&&p.contrast>=4.5&&(!frost.supported||p.blur==='blur(14px)')),'Crisp frosted panels: '+JSON.stringify(frost));
  assert(frost.bar.alpha>=.88&&frost.bar.contrast>=4.5&&frost.controls.every(c=>c.alpha>=.98&&c.contrast>=4.5),'Readable top bar and controls: '+JSON.stringify(frost));
  assert(frost.tables.every(t=>t.alpha===0),'Tables reveal parent glass: '+JSON.stringify(frost.tables));
  await evaluate('window.scrollTo(0,document.documentElement.scrollHeight)');await shot('desktop-bottom');assert(JSON.stringify(await watermarkInfo())===JSON.stringify(watermark),'Watermark stays fixed while scrolling');await evaluate('window.scrollTo(0,0)');
  await shot('desktop',true);
  await send('Input.dispatchKeyEvent',{type:'keyDown',key:'Tab',code:'Tab',windowsVirtualKeyCode:9});await send('Input.dispatchKeyEvent',{type:'keyUp',key:'Tab',code:'Tab',windowsVirtualKeyCode:9});
  const focus=await evaluate(`({text:document.activeElement.textContent.trim(),outline:getComputedStyle(document.activeElement).outlineWidth,visible:document.activeElement.getBoundingClientRect().top>=0})`);assert(focus.text==='Skip to content'&&focus.outline==='3px'&&focus.visible,'Visible keyboard skip focus');
  const selectState=await evaluate(`(()=>{const e=document.querySelector('main select');if(!e)return null;e.focus();return {count:e.options.length,index:e.selectedIndex};})()`);
  if(selectState){await send('Input.dispatchKeyEvent',{type:'keyDown',key:'ArrowDown',code:'ArrowDown',windowsVirtualKeyCode:40});await send('Input.dispatchKeyEvent',{type:'keyUp',key:'ArrowDown',code:'ArrowDown',windowsVirtualKeyCode:40});assert(await evaluate(`document.querySelector('main select').selectedIndex>=0`),'Native filter dropdown remains operable');}
  await evaluate(`document.querySelector('.account-menu').open=true`);await shot('account-dropdown');await evaluate(`document.querySelector('.account-menu').open=false`);
  await send('Emulation.setDeviceMetricsOverride',{width:390,height:844,deviceScaleFactor:1,mobile:true});await navigate();
  const mobile=await evaluate(`({width:innerWidth,client:document.documentElement.clientWidth,scroll:document.documentElement.scrollWidth,overflowElements:[...document.querySelectorAll('body *')].filter(e=>{const r=e.getBoundingClientRect();return r.right>document.documentElement.clientWidth+1&&r.left>=0&&getComputedStyle(e).position!=='fixed';}).slice(0,18).map(e=>({tag:e.tagName,cls:e.className,right:e.getBoundingClientRect().right,width:e.getBoundingClientRect().width})),menuVisible:!!document.querySelector('.mobile-menu-toggle')&&getComputedStyle(document.querySelector('.mobile-menu-toggle')).display!=='none',sidebarHidden:!document.querySelector('.app-sidebar')||document.querySelector('.app-sidebar').getBoundingClientRect().right<=1})`);
  assert(mobile.width===390&&mobile.scroll===390&&(desktop.sidebarPosition==='guest'||mobile.menuVisible)&&mobile.sidebarHidden,'Mobile reflow and collapsed navigation: '+JSON.stringify(mobile));await shot('mobile',true);await evaluate('window.scrollTo(0,document.documentElement.scrollHeight)');await shot('mobile-bottom');await evaluate('window.scrollTo(0,0)');
  const mobileWatermark=await watermarkInfo();assert(mobileWatermark.left==='0px'&&Number.parseFloat(mobileWatermark.width)===390&&mobileWatermark.size==='80% auto','Responsive mobile watermark without overflow');
  const tableScroll=await evaluate(`(()=>{const e=[...document.querySelectorAll('.table-responsive')].find(e=>e.scrollWidth>e.clientWidth);if(!e)return {needed:false};e.scrollLeft=100;const moved=e.scrollLeft>0;e.scrollLeft=0;return {needed:true,moved};})()`);assert(!tableScroll.needed||tableScroll.moved,'Mobile tables scroll inside their panel');
  let menu=null,restored=true;
  if(desktop.sidebarPosition!=='guest'){
  await evaluate(`document.querySelector('.mobile-menu-toggle').click()`);
  await evaluate(`new Promise(resolve=>{const panel=document.querySelector('#app-navigation');if(panel.classList.contains('show'))resolve();else panel.addEventListener('shown.bs.offcanvas',resolve,{once:true});})`);
  menu=await evaluate(`({open:document.querySelector('#app-navigation').classList.contains('show'),focusInside:document.querySelector('#app-navigation').contains(document.activeElement)})`);assert(menu.open&&menu.focusInside,'Accessible mobile navigation focus');const mobileLogo=await evaluate(`(()=>{const e=document.querySelector('.sidebar-brand img'),r=e.getBoundingClientRect();return {loaded:e.complete&&e.naturalWidth>0,width:r.width,left:r.left,right:r.right,aspect:r.width/r.height};})()`);assert(mobileLogo.loaded&&mobileLogo.width>=180&&mobileLogo.left>=0&&mobileLogo.right<=390&&Math.abs(mobileLogo.aspect-brand.naturalAspect)<.01,'Mobile logo visibility and aspect ratio');await shot('mobile-menu');
  await send('Input.dispatchKeyEvent',{type:'keyDown',key:'Escape',code:'Escape',windowsVirtualKeyCode:27});await send('Input.dispatchKeyEvent',{type:'keyUp',key:'Escape',code:'Escape',windowsVirtualKeyCode:27});
  await evaluate(`new Promise(resolve=>{const panel=document.querySelector('#app-navigation');if(!panel.classList.contains('show')&&!panel.classList.contains('hiding'))resolve();else panel.addEventListener('hidden.bs.offcanvas',resolve,{once:true});})`);
  restored=await evaluate(`document.activeElement===document.querySelector('.mobile-menu-toggle')`);assert(restored,'Escape restores mobile trigger focus');}
  assert(errors.length===0,'Browser runtime errors: '+errors.join(','));
  const fallback=await evaluate(`(()=>{for(const sheet of document.styleSheets){for(let i=sheet.cssRules.length-1;i>=0;i--){const rule=sheet.cssRules[i];if(rule instanceof CSSSupportsRule&&rule.conditionText.includes('backdrop-filter'))sheet.deleteRule(i);}}return [...document.querySelectorAll('.work-panel,.progress-panel,.history-panel,.app-topbar')].map(e=>({bg:getComputedStyle(e).backgroundColor,blur:getComputedStyle(e).backdropFilter}));})()`);
  assert(fallback.every(p=>p.bg==='rgb(255, 255, 255)'&&p.blur==='none'),'Opaque no-blur fallback: '+JSON.stringify(fallback));await shot('mobile-fallback',true);
  const result={watermark,mobileWatermark,watermarkImage,frost,tableScroll,fallback,brand,desktop,focus,mobile,menu,escapeRestoresFocus:restored,errors};fs.writeFileSync(path.join(root,'checks.json'),JSON.stringify(result,null,2));console.log(JSON.stringify(result));ws.close();
 }finally{if(ws)ws.close();if(chrome.exitCode===null)chrome.kill();}
})().catch(error=>{fs.writeFileSync(path.join(process.env.CPT_DASHBOARD_OUTPUT,'browser-error.txt'),error.stack||String(error));console.error(error);process.exit(1);});
