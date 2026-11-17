// Opt-in live browser acceptance. Credentials are memory-only; no DOM/HAR/screenshots.
const fs=require('node:fs'),path=require('node:path'),os=require('node:os'),https=require('node:https'),{spawn}=require('node:child_process');
let stage='startup';
(async()=>{
 const cfg=JSON.parse(process.env.CPT_COURSE_CONFIG);delete process.env.CPT_COURSE_CONFIG;
 const base='https://localhost:7265',output=path.resolve(cfg.journey==='onboarding'?'TestResults/onboarding-browser':cfg.journey==='archive'?'TestResults/archive-browser':'TestResults/course-browser');fs.mkdirSync(output,{recursive:true});
 const profile=fs.mkdtempSync(path.join(os.tmpdir(),'CptCourseBrowser-'));
 const browserPath=process.env.CPT_NATIVE_BROWSER==='edge'?'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe':'C:/Program Files/Google/Chrome/Application/chrome.exe';
 const chrome=spawn(browserPath,['--headless','--disable-gpu','--no-first-run','--no-default-browser-check','--disable-background-networking','--disable-background-timer-throttling','--disable-renderer-backgrounding','--disable-backgrounding-occluded-windows','--disable-sync','--ignore-certificate-errors','--remote-debugging-port=0','--user-data-dir='+profile,'about:blank'],{windowsHide:true,stdio:'ignore'});
 const connections=[];let browser,port;
 const check=(ok,message)=>{if(!ok)throw Error(message);};
 const connect=async url=>{
  const ws=new WebSocket(url);await new Promise((resolve,reject)=>{ws.onopen=resolve;ws.onerror=()=>reject(Error('Browser connection failed'));});
  let seq=0,documentStatus,lastLoader,mainFrame,navigation;const pending=new Map(),readyLoaders=new Set(),resources=new Map();
  const completeNavigation=()=>{
   if(!navigation?.loader||!readyLoaders.has(navigation.loader))return;
   const current=navigation;navigation=undefined;clearTimeout(current.timer);
   console.log('READY: main document DOMContentLoaded; HTTP '+current.status);
   current.resolve();
  };
  ws.onmessage=e=>{
   const m=JSON.parse(e.data);
   if(m.id){const p=pending.get(m.id);if(!p)return;pending.delete(m.id);m.error?p.reject(Error('Browser command failed')):p.resolve(m.result);return;}
   if(m.method==='Network.requestWillBeSent'){const r=m.params;try{resources.set(r.requestId,{type:r.type,path:new URL(r.request.url).pathname});}catch{}}
   if(m.method==='Network.loadingFinished'||m.method==='Network.loadingFailed'){
    if(m.method==='Network.loadingFailed'&&resources.has(m.params.requestId))console.log('RESOURCE FAILED: '+JSON.stringify(resources.get(m.params.requestId)));
    resources.delete(m.params.requestId);
   }
   if(m.method==='Page.lifecycleEvent'&&navigation)console.log('LIFECYCLE: '+m.params.name+'; mainFrame='+(!mainFrame||m.params.frameId===mainFrame)+'; currentLoader='+(m.params.loaderId===navigation.loader));
   if(m.method==='Page.frameNavigated'&&!m.params.frame.parentId)mainFrame=m.params.frame.id;
   if(m.method==='Network.responseReceived'&&m.params.type==='Document'&&(!mainFrame||m.params.frameId===mainFrame)){
    const {loaderId,response}=m.params;documentStatus=response.status;lastLoader=loaderId;
    console.log('DOCUMENT: '+documentStatus+' '+new URL(response.url).pathname);
    if(navigation&&loaderId!==navigation.previousLoader){navigation.loader=loaderId;navigation.status=documentStatus;completeNavigation();}
   }
   if(m.method==='Page.lifecycleEvent'&&m.params.name==='DOMContentLoaded'&&(!mainFrame||m.params.frameId===mainFrame)){
    readyLoaders.add(m.params.loaderId);completeNavigation();
   }
  };
  const send=(method,params={})=>new Promise((resolve,reject)=>{const id=++seq;pending.set(id,{resolve,reject});ws.send(JSON.stringify({id,method,params}));});
  const evaluate=async expression=>{const r=await send('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(r.exceptionDetails){if(r.exceptionDetails.exception?.description?.startsWith('Error: Invalid form'))throw Error('Form constraint validation failed');throw Error('Browser script failed');}return r.result.value;};
  // Correlate readiness to the NEW main-document loader. Global load events can
  // wait on unrelated assets or belong to the previous page. Keep the same 45s
  // deadline, actual HTTP checks and real browser form/antiforgery behavior.
  const loaded=()=>new Promise((resolve,reject)=>{
   const current={resolve,reject,previousLoader:lastLoader};navigation=current;
   current.timer=setTimeout(()=>{if(navigation===current)navigation=undefined;console.log('PENDING RESOURCE PATHS: '+JSON.stringify([...resources.values()]));console.log('TIMEOUT document received: '+!!current.loader+'; DOM ready: '+readyLoaders.has(current.loader)+'; HTTP: '+current.status);reject(Error('Navigation timeout'));},45000);
  });
  const go=async route=>{console.log('ACTION: GET '+route.split('?')[0]);const load=loaded();await send('Page.navigate',{url:base+route});await load;console.log('PAGE STATUS: '+documentStatus);check(documentStatus===200,'Unexpected page status');};
  const fill=async values=>evaluate(`(()=>{for(const [name,value] of Object.entries(${JSON.stringify(values)})){const input=document.getElementsByName(name)[0];if(!input)throw Error('Missing input');if(input.multiple){for(const option of input.options)option.selected=value.includes(option.value);}else input.value=value;input.dispatchEvent(new Event('input',{bubbles:true}));input.dispatchEvent(new Event('change',{bubbles:true}));}})()`);
  const submit=async(selector='main button[type="submit"]',bypass=false)=>{console.log('ACTION: submit page form');const load=loaded();await evaluate(`(()=>{const button=document.querySelector(${JSON.stringify(selector)});if(!button)throw Error('Missing button');${bypass?'button.form.noValidate=true;':''}if(!button.form.noValidate&&!button.form.checkValidity())throw Error('Invalid form');button.click();})()`);await load;console.log('PAGE STATUS: '+documentStatus);check(documentStatus===200,'Unexpected page status');};
  const text=()=>evaluate('document.body.innerText');
  const pathname=()=>evaluate('location.pathname');
  const api={send,evaluate,go,fill,submit,text,pathname,close:()=>ws.close(),closed:()=>new Promise(r=>ws.readyState===WebSocket.CLOSED?r():ws.addEventListener('close',r,{once:true}))};connections.push(api);return api;
 };
 const control=action=>new Promise((resolve,reject)=>{const req=https.request(base+'/_acceptance/'+action,{method:'POST',rejectUnauthorized:false,headers:{'X-Acceptance-Control':cfg.control}},res=>{res.resume();res.on('end',()=>res.statusCode===204?resolve():reject(Error('Isolated fixture control failed')));});req.on('error',()=>reject(Error('Isolated fixture control unavailable')));req.end();});
 try{
  let version;const until=Date.now()+25000;while(Date.now()<until){try{port=Number(fs.readFileSync(path.join(profile,'DevToolsActivePort'),'utf8').split('\n')[0]);if(port>0){version=await(await fetch('http://127.0.0.1:'+port+'/json/version')).json();break;}}catch{}await new Promise(r=>setTimeout(r,100));}
  check(version,'Owned browser startup failed');browser=await connect(version.webSocketDebuggerUrl);
  const identity=await browser.send('Browser.getVersion');check(process.env.CPT_NATIVE_BROWSER==='edge'?identity.userAgent.includes('Edg/'):!identity.userAgent.includes('Edg/'),'Browser identity mismatch');
  console.log('BROWSER: '+identity.product+'; Edge: '+identity.userAgent.includes('Edg/'));
  const tab=async()=>{const {browserContextId}=await browser.send('Target.createBrowserContext');const {targetId}=await browser.send('Target.createTarget',{url:'about:blank',browserContextId});const targets=await(await fetch('http://127.0.0.1:'+port+'/json/list')).json();const page=await connect(targets.find(t=>t.id===targetId).webSocketDebuggerUrl);await page.send('Page.enable');await page.send('Page.setLifecycleEventsEnabled',{enabled:true});await page.send('Runtime.enable');await page.send('Network.enable');page.browserContextId=browserContextId;return page;};
  if(cfg.journey==='onboarding'){
   await require('./onboarding-journey.cjs')({tab,check,cfg,output,setStage:value=>{stage=value;}});return;
  }
  if(cfg.journey==='archive'){
   await require('./archive-audit-journey.cjs')({tab,check,cfg,output,setStage:value=>{stage=value;}});return;
  }
  const admin=await tab(),owner=await tab(),mentor=await tab(),outsider=await tab();
  const ui=[];
  const preview=async(page,label)=>{if(process.env.CPT_NATIVE_BROWSER!=='edge')return;for(const width of [1366,360]){
   await page.send('Emulation.setDeviceMetricsOverride',{width,height:800,deviceScaleFactor:1,mobile:false});
   const state=await page.evaluate(`(()=>{const logo=document.querySelector('img[alt="CAPACITI"]');return {overflow:document.documentElement.scrollWidth>innerWidth+1,logoLoaded:!!logo&&logo.complete&&logo.naturalWidth>0,scrollHeight:document.documentElement.scrollHeight};})()`);
   check(!state.overflow&&state.logoLoaded,'Current UI overflow or missing logo');
   const shot=await page.send('Page.captureScreenshot',{format:'png',captureBeyondViewport:true});fs.writeFileSync(path.join(output,label+'-'+width+'.png'),Buffer.from(shot.data,'base64'));ui.push({page:label,width,...state});
  }await page.send('Emulation.clearDeviceMetricsOverride');};

  const login=async(page,user,expected)=>{await page.go('/Account/Login');await page.fill({'Input.UserName':user,'Input.Password':cfg.password});await page.submit();check(await page.pathname()===expected,'Login destination mismatch');};
  const passed=[];const pass=()=>{passed.push(stage);console.log('PASS: '+stage);};
  stage='administrator creates catalogue, offering and individual course assignment';
  await login(admin,cfg.adminName,'/Admin');await preview(admin,'admin-dashboard');const route='/Admin/Courses/'+cfg.cycleId;
  await admin.go(route);await admin.fill({'Input.Title':'Browser certificate course','Input.Platform':'Acceptance provider','Input.Url':'https://example.com/course'});await admin.submit('input[value="create"] + label ~ button[type="submit"]');
  await admin.submit('form:has(input[value="offer"]) button');
  const deadline=new Date(Date.now()+2*86400000).toISOString().slice(0,16);
  await admin.fill({'Input.CandidateId':cfg.candidateId,'Input.DueLocal':deadline,'Input.Instructions':'Complete acceptance course','Input.Acceptance':'Provide a certificate','Input.Evidence':'Actual certificate file required'});await admin.evaluate(`document.getElementsByName('Input.Required')[0].checked=true`);await admin.submit('form:has(input[value="assign"]) button');
  check((await admin.text()).includes('Browser certificate course'),'Course assignment missing');pass();
  stage='course owner navigation and links-only rejection';
  await login(owner,cfg.candidateName,'/Candidate');await preview(owner,'candidate-dashboard');await owner.go('/Candidate/Courses');const submission=await owner.evaluate(`document.querySelector('main a[href*="/Candidate/Submit/"]')?.getAttribute('href')`);check(submission,'Course submission navigation missing');const assignment=submission.split('/').pop();
  await owner.go(submission);await owner.fill({'Input.Explanation':'Certificate completion','Input.Evidence[0].Label':'Course link','Input.Evidence[0].Url':'https://example.com/certificate'});await owner.submit();check(await owner.pathname()===submission,'Links-only course was accepted');check((await owner.text()).includes('Courses require an actual certificate file.'),'Certificate validation missing');pass();
  stage='actual PDF upload stays Processing until real Defender finalizes it';
  await owner.go(submission);await preview(owner,'certificate-form');await owner.fill({'Input.Explanation':'Browser certificate completion'});
  const document=await owner.send('DOM.getDocument');const input=await owner.send('DOM.querySelector',{nodeId:document.root.nodeId,selector:'#Files'});await owner.send('DOM.setFileInputFiles',{nodeId:input.nodeId,files:[path.resolve('TestResults/operational-acceptance/samples/certificate.pdf')]});
  await owner.submit();const processing=await owner.pathname();check(processing.startsWith('/Candidate/Processing/'),'Processing route missing');check((await owner.text()).includes('Checking uploaded files'),'Processing status missing');
  await control('process');await owner.go(processing);check((await owner.text()).includes('Submission saved.'),'Real scanning did not commit');check((await owner.text()).includes('Receipt:'),'Committed receipt missing');pass();
  stage='history protected download preserves bytes and blocks another candidate';
  const history='/Candidate/SubmissionHistory/'+assignment;await owner.go(history);check((await owner.text()).includes('Attempt 1'),'History attempt missing');const download=await owner.evaluate(`document.querySelector('main a[href*="/Evidence/Download/"]')?.getAttribute('href')`);check(download,'Certificate download link missing');
  const downloaded=await owner.evaluate(`(async()=>{const r=await fetch(${JSON.stringify(download)});return {status:r.status,attachment:r.headers.get('content-disposition'),cache:r.headers.get('cache-control'),bytes:[...new Uint8Array(await r.arrayBuffer())]};})()`);check(downloaded.status===200&&downloaded.attachment.includes('attachment')&&downloaded.cache.includes('no-store'),'Unsafe certificate response');check(Buffer.from(downloaded.bytes).equals(fs.readFileSync('TestResults/operational-acceptance/samples/certificate.pdf')),'Certificate bytes changed');
  if(process.env.CPT_NATIVE_BROWSER==='edge'){
   const downloadRoot=path.join(profile,'downloads');fs.mkdirSync(downloadRoot);
   await browser.send('Browser.setDownloadBehavior',{behavior:'allow',downloadPath:downloadRoot,browserContextId:owner.browserContextId});
   await owner.evaluate(`document.querySelector('main a[href*="/Evidence/Download/"]').click()`);
   let files=[];const end=Date.now()+45000;while(Date.now()<end){files=fs.readdirSync(downloadRoot).filter(n=>!n.endsWith('.crdownload')&&!n.endsWith('.tmp'));if(files.length)break;await new Promise(r=>setTimeout(r,100));}
   fs.writeFileSync(path.join(output,'native-download.json'),JSON.stringify({completedFiles:files.length,partialFiles:fs.readdirSync(downloadRoot).filter(n=>n.endsWith('.crdownload')).length,browserContextScoped:true},null,2));
   check(files.length===1&&fs.readFileSync(path.join(downloadRoot,files[0])).equals(fs.readFileSync('TestResults/operational-acceptance/samples/certificate.pdf')),'Browser-managed download bytes differ');console.log('PASS: native browser-managed attachment download exact bytes');
  }
  await preview(owner,'certificate-history');

  await login(outsider,cfg.outsiderName,'/Candidate');check(await outsider.evaluate(`fetch(${JSON.stringify(download)}).then(r=>r.status)`)===404,'Another candidate downloaded certificate');check(await outsider.evaluate(`fetch(${JSON.stringify(history)}).then(r=>r.status)`)===404,'Another candidate read history');pass();
  stage='scoped mentor queue approval and candidate feedback/progress';
  await login(mentor,cfg.mentorName,'/Mentor');await preview(mentor,'mentor-dashboard');await mentor.go('/Mentor/ReviewQueue');const review='/Mentor/ReviewSubmission/'+assignment;check(await mentor.evaluate(`!!document.querySelector('main a[href="${review}"]')`),'Course absent from review queue');await mentor.go(review);await preview(mentor,'mentor-review');
  check(await mentor.evaluate(`fetch(${JSON.stringify(download)}).then(r=>r.status)`)===200,'Scoped mentor download denied');await mentor.fill({'Input.Decision':'1','Input.Feedback':'Certificate verified in browser acceptance'});await mentor.submit();check((await mentor.text()).includes('Approved'),'Mentor decision missing');
  await owner.go(history);check((await owner.text()).includes('Certificate verified in browser acceptance'),'Candidate feedback missing');
  check(await owner.evaluate(`[...document.querySelectorAll('main h4')].some(h=>/^Review \\d+: Approved$/.test(h.textContent.trim()))`),'Candidate approval missing');
  await owner.go('/Candidate/Progress?cycleId='+cfg.cycleId);
  // Read Theory specifically, retaining exact numeric expectations while accepting
  // the decimal separator used by the server's culture-sensitive Razor formatting.
  // Persist numeric diagnostics only, never the page's account/evidence content.
  const theoryProgress=await owner.evaluate(`(()=>{const heading=[...document.querySelectorAll('main h2')].find(h=>h.textContent.trim()==='Theory');const summary=heading?.nextElementSibling;const match=summary?.tagName==='P'&&summary.textContent.trim().match(/^(\\d+)\\s*\\/\\s*(\\d+) approved \\((\\d+[.,]\\d)%\\)$/);return match?{approved:Number(match[1]),required:Number(match[2]),percentage:Number(match[3].replace(',','.')),displayedPercentage:match[3]}:{summaryFormatValid:false};})()`);
  fs.writeFileSync(path.join(output,'progress.json'),JSON.stringify(theoryProgress,null,2));
  check(theoryProgress.approved===1&&theoryProgress.required===1&&theoryProgress.percentage===100,'Theory progress missing');pass();
  if(process.env.CPT_NATIVE_BROWSER==='edge')fs.writeFileSync(path.join(output,'native-ui.json'),JSON.stringify({browser:identity.product,userAgent:identity.userAgent,ui,nativeBrowserDownload:true,fileSelection:'CDP real file input; OS picker not tested',androidDevice:false},null,2));
  fs.writeFileSync(path.join(output,'acceptance.json'),JSON.stringify({passed,theoryProgress,transport:'Live HTTPS Kestrel, real Chrome forms and Defender, isolated SQL and private storage',credentialsPersisted:false,browserChecksCompleted:true,processing:'Test-only authenticated trigger drives actual durable processor; background scheduling not rechecked'},null,2));
 }finally{
  if(browser){const closed=browser.closed();browser.send('Browser.close').catch(()=>{});await closed;}
  connections.forEach(c=>c.close());if(chrome.exitCode===null)chrome.kill();
  // Remove only this invocation's exact, newly created temporary browser profile.
  if(path.dirname(profile)===os.tmpdir()&&path.basename(profile).startsWith('CptCourseBrowser-')){try{fs.rmSync(profile,{recursive:true,force:true,maxRetries:10,retryDelay:100});}catch{}}
 }
})().catch(error=>{const safe=new Set(['Navigation timeout','Credential receipt missing','Login destination mismatch','Created account navigation missing','Secret input is not masked','Credential result mismatch','Failed credential response redisplays secrets','Isolated fixture control failed','Isolated fixture control unavailable','Browser script failed','Browser command failed','Unexpected page status','Expected login denial missing','Form constraint validation failed']);console.error('FAIL: course browser stage: '+stage+'; '+(safe.has(error.message)?error.message:'Acceptance assertion failed')+'. No secret diagnostics retained.');process.exit(1);});



