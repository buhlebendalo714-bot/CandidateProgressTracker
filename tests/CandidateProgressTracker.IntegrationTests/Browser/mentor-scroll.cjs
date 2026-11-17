const fs=require('node:fs'),path=require('node:path'),http=require('node:http'),os=require('node:os'),{spawn}=require('node:child_process'),crypto=require('node:crypto');
(async()=>{
 const role=process.env.CPT_SCROLL_ROLE||'mentor';if(!['candidate','mentor','admin'].includes(role))throw Error('Unknown role');
 const output=path.resolve(process.env.CPT_SCROLL_ROLE?'TestResults/dashboard-light/'+role:'TestResults/mentor-scroll');fs.mkdirSync(output,{recursive:true});
 const source='TestResults/dashboard-branding/'+role+'/authenticated-snapshot.json',snapshot=JSON.parse(fs.readFileSync(source,'utf8'));
 const css=fs.readFileSync('src/CandidateProgressTracker.Web/wwwroot/css/site.css');
 let replaced=false;for(const [key,value] of Object.entries(snapshot.Pages)){if(/\/css\/site(?:\.[^/]+)?\.css$/.test(key)){value.Body=css.toString('base64');replaced=true;}}
 if(!replaced)throw Error('Shared stylesheet not found');
 const server=http.createServer((req,res)=>{if(req.method!=='GET'){res.writeHead(405);res.end();return;}const item=snapshot.Pages[req.url]||snapshot.Pages[req.url.split('?')[0]];if(!item){res.writeHead(404);res.end();return;}res.setHeader('Content-Type',item.Type);res.end(Buffer.from(item.Body,'base64'));});
 await new Promise(r=>server.listen(0,'127.0.0.1',r));
 const profile=fs.mkdtempSync(path.join(os.tmpdir(),'CptMentorScroll-'));
 const browser=spawn('C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',['--headless','--no-first-run','--no-default-browser-check','--remote-debugging-port=0','--user-data-dir='+profile,'about:blank'],{windowsHide:true,stdio:'ignore'});
 let ws,send;const watchdog=setTimeout(()=>{browser.kill();server.close();process.exitCode=1;},90000);
 try{
  let port;const until=Date.now()+25000;while(Date.now()<until){try{port=Number(fs.readFileSync(path.join(profile,'DevToolsActivePort'),'utf8').split('\n')[0]);if(port)break;}catch{}await new Promise(r=>setTimeout(r,100));}if(!port)throw Error('Owned browser unavailable');
  const target=await(await fetch(`http://127.0.0.1:${port}/json/new?about:blank`,{method:'PUT'})).json();
  ws=new WebSocket(target.webSocketDebuggerUrl);await new Promise((r,j)=>{ws.onopen=r;ws.onerror=j;});let id=0;const pending=new Map();let loaded;
  ws.onmessage=e=>{const m=JSON.parse(e.data);if(m.id){const p=pending.get(m.id);if(!p)return;pending.delete(m.id);m.error?p.reject(Error('CDP command failed')):p.resolve(m.result);}else if(m.method==='Page.loadEventFired'&&loaded)loaded();};
  send=(method,params={})=>new Promise((resolve,reject)=>{const next=++id;pending.set(next,{resolve,reject});ws.send(JSON.stringify({id:next,method,params}));});
  const evaluate=async expression=>{const r=await send('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(r.exceptionDetails)throw Error('Page evaluation failed');return r.result.value;};
  const identity=await send('Browser.getVersion');if(!identity.userAgent.includes('Edg/'))throw Error('Unexpected browser');await send('Page.enable');await send('Runtime.enable');
  const navigation=new Promise(r=>loaded=r);await send('Page.navigate',{url:`http://127.0.0.1:${server.address().port}`+snapshot.Path});await navigation;
  const checks=[];
  if(process.env.CPT_SCROLL_ROLE){const theme=await evaluate(`(()=>{const w=document.querySelector('.dashboard-workspace'),c=getComputedStyle(w),m=getComputedStyle(document.querySelector('.app-main'));return {background:c.backgroundColor,image:c.backgroundImage,watermark:getComputedStyle(w,'::before').content,text:m.color};})()`);if(theme.image!=='none'||!['none','normal'].includes(theme.watermark)||theme.text==='rgb(255, 255, 255)')throw Error('Light dashboard theme not applied');checks.push({theme});}
  for(const width of [1366,360]){
   await send('Emulation.setDeviceMetricsOverride',{width,height:800,deviceScaleFactor:1,mobile:false});
   for(const position of ['top','middle','bottom']){
    await evaluate(`new Promise(r=>{const max=document.documentElement.scrollHeight-innerHeight;window.scrollTo(0,${position==='top'?'0':position==='middle'?'max/2':'max'});requestAnimationFrame(()=>requestAnimationFrame(r));})`);
    const state=await evaluate(`(()=>{const visible=[...document.querySelectorAll('main h1,main h2,main h3,main a,main select,main table')].filter(e=>{const r=e.getBoundingClientRect();return r.bottom>0&&r.top<innerHeight&&r.width>0;});return {scrollY,scrollHeight:document.documentElement.scrollHeight,overflow:document.documentElement.scrollWidth>innerWidth+1,visibleElements:visible.length,headings:visible.filter(e=>/^H/.test(e.tagName)).map(e=>e.textContent.trim())};})()`);
    if(state.overflow||!state.visibleElements)throw Error('Viewport overflow or no visible dashboard content');
    const image=await send('Page.captureScreenshot',{format:'png',captureBeyondViewport:false});fs.writeFileSync(path.join(output,`${width}-${position}.png`),Buffer.from(image.data,'base64'));checks.push({width,position,...state});
   }
  }
  fs.writeFileSync(path.join(output,'checks.json'),JSON.stringify({browser:identity.product,source,mode:'GET-only replay of earlier authenticated fixture markup with current shared stylesheet; not fresh live authentication or Android',cssSha256:crypto.createHash('sha256').update(css).digest('hex'),checks},null,2));console.log('PASS: six scrolled viewport captures; inspect PNGs before visual acceptance');
 }finally{
  if(ws&&ws.readyState===WebSocket.OPEN){send('Browser.close').catch(()=>{});ws.close();}
  server.closeAllConnections();server.close();
  if(browser.exitCode===null){const killer=spawn('taskkill.exe',['/PID',String(browser.pid),'/T','/F'],{windowsHide:true,stdio:'ignore'});await new Promise(r=>killer.once('exit',r));}
  clearTimeout(watchdog);
  // This exact newly generated profile is owned by this invocation only.
  if(path.dirname(profile)===os.tmpdir()&&path.basename(profile).startsWith('CptMentorScroll-'))try{fs.rmSync(profile,{recursive:true,force:true,maxRetries:3,retryDelay:100});}catch{}
 }
})().catch(e=>{console.error(e.message);process.exitCode=1;});
