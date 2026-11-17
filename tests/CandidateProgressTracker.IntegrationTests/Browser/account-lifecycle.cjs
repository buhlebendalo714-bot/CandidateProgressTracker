// Opt-in live browser acceptance. Credentials are memory-only; no DOM/HAR/screenshots.
const fs=require('node:fs'),path=require('node:path'),os=require('node:os'),https=require('node:https'),{spawn}=require('node:child_process');
let stage='startup';
(async()=>{
 const cfg=JSON.parse(process.env.CPT_LIFECYCLE_CONFIG);delete process.env.CPT_LIFECYCLE_CONFIG;
 const base='https://localhost:7264',output=path.resolve('TestResults/lifecycle-browser');fs.mkdirSync(output,{recursive:true});
 const profile=fs.mkdtempSync(path.join(os.tmpdir(),'CptLifecycleBrowser-'));
 const chrome=spawn('C:/Program Files/Google/Chrome/Application/chrome.exe',['--headless','--disable-gpu','--no-first-run','--no-default-browser-check','--disable-background-networking','--disable-background-timer-throttling','--disable-renderer-backgrounding','--disable-backgrounding-occluded-windows','--disable-sync','--ignore-certificate-errors','--remote-debugging-port=9232','--user-data-dir='+profile,'about:blank'],{windowsHide:true,stdio:'ignore'});
 const connections=[];let browser;
 const check=(ok,message)=>{if(!ok)throw Error(message);};
 const connect=async url=>{
  const ws=new WebSocket(url);await new Promise((resolve,reject)=>{ws.onopen=resolve;ws.onerror=()=>reject(Error('Browser connection failed'));});
  let seq=0,documentStatus;const pending=new Map(),events=new Map();
  ws.onmessage=e=>{const m=JSON.parse(e.data);if(m.id){const p=pending.get(m.id);if(!p)return;pending.delete(m.id);m.error?p.reject(Error('Browser command failed')):p.resolve(m.result);}else if(m.method==='Network.responseReceived'&&m.params.type==='Document'){documentStatus=m.params.response.status;}else if(events.has(m.method)){events.get(m.method)();events.delete(m.method);}};
  const send=(method,params={})=>new Promise((resolve,reject)=>{const id=++seq;pending.set(id,{resolve,reject});ws.send(JSON.stringify({id,method,params}));});
  const evaluate=async expression=>{const r=await send('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(r.exceptionDetails){if(r.exceptionDetails.exception?.description?.startsWith('Error: Invalid form'))throw Error('Form constraint validation failed');throw Error('Browser script failed');}return r.result.value;};
  const loaded=()=>new Promise((resolve,reject)=>{const timer=setTimeout(()=>{events.delete('Page.loadEventFired');reject(Error('Navigation timeout'));},45000);events.set('Page.loadEventFired',()=>{clearTimeout(timer);resolve();});});
  const go=async route=>{console.log('ACTION: GET '+route.split('?')[0]);const load=loaded();await send('Page.navigate',{url:base+route});await load;console.log('PAGE STATUS: '+documentStatus);check(documentStatus===200,'Unexpected page status');};
  const fill=async values=>evaluate(`(()=>{for(const [name,value] of Object.entries(${JSON.stringify(values)})){const input=document.getElementsByName(name)[0];if(!input)throw Error('Missing input');if(input.multiple){for(const option of input.options)option.selected=value.includes(option.value);}else input.value=value;input.dispatchEvent(new Event('input',{bubbles:true}));input.dispatchEvent(new Event('change',{bubbles:true}));}})()`);
  const submit=async(selector='main button[type="submit"]',bypass=false)=>{console.log('ACTION: submit page form');const load=loaded();await evaluate(`(()=>{const button=document.querySelector(${JSON.stringify(selector)});if(!button)throw Error('Missing button');${bypass?'button.form.noValidate=true;':''}if(!button.form.noValidate&&!button.form.checkValidity())throw Error('Invalid form');button.click();})()`);await load;console.log('PAGE STATUS: '+documentStatus);check(documentStatus===200,'Unexpected page status');};
  const text=()=>evaluate('document.body.innerText');
  const pathname=()=>evaluate('location.pathname');
  const api={send,evaluate,go,fill,submit,text,pathname,close:()=>ws.close()};connections.push(api);return api;
 };
 const control=action=>new Promise((resolve,reject)=>{const req=https.request(base+'/_acceptance/'+action,{method:'POST',rejectUnauthorized:false,headers:{'X-Acceptance-Control':cfg.control}},res=>{res.resume();res.on('end',()=>res.statusCode===204?resolve():reject(Error('Isolated fixture control failed')));});req.on('error',()=>reject(Error('Isolated fixture control unavailable')));req.end();});
 try{
  let version;const until=Date.now()+25000;while(Date.now()<until){try{version=await(await fetch('http://127.0.0.1:9232/json/version')).json();break;}catch{await new Promise(r=>setTimeout(r,100));}}
  check(version,'Chrome startup failed');browser=await connect(version.webSocketDebuggerUrl);
  const tab=async()=>{const {browserContextId}=await browser.send('Target.createBrowserContext');const {targetId}=await browser.send('Target.createTarget',{url:'about:blank',browserContextId});const targets=await(await fetch('http://127.0.0.1:9232/json/list')).json();const page=await connect(targets.find(t=>t.id===targetId).webSocketDebuggerUrl);await page.send('Page.enable');await page.send('Runtime.enable');await page.send('Network.enable');return page;};
  const admin=await tab(),guest=await tab(),old=await tab(),second=await tab(),fresh=await tab();
  const login=async(page,user,password,expected)=>{await page.go('/Account/Login');await page.fill({'Input.UserName':user,'Input.Password':password});await page.submit();check((await page.pathname())===expected,'Login destination mismatch');if(expected==='/Account/Login')check((await page.text()).includes('Unable to log in. Check your details or contact your administrator.'),'Expected login denial missing');};
  const receipt=async()=>{const value=await admin.evaluate(`document.querySelector('[role="status"] code')?.textContent.trim()`);check(value&&value.length>=40,'Credential receipt missing');return value;};
  const redeem=async(token,password,success)=>{await guest.go('/Account/CompleteCredential');check(await guest.evaluate(`['Credential','Password','ConfirmPassword'].every(n=>document.getElementsByName(n)[0].type==='password')`),'Secret input is not masked');await guest.fill({UserName:cfg.candidateName,Credential:token,Password:password,ConfirmPassword:password});await guest.submit();const body=await guest.text();check(success?body.includes('Password saved.'):body.includes('The credential is invalid, expired or already used.'),'Credential result mismatch');if(!success)check(await guest.evaluate(`['Credential','Password','ConfirmPassword'].every(n=>document.getElementsByName(n)[0].value==='')`),'Failed credential response redisplays secrets');};
  const manage=async()=>{await admin.go('/Admin/Users');const href=await admin.evaluate(`[...document.querySelectorAll('td a')].find(a=>a.textContent===${JSON.stringify(cfg.candidateName)})?.getAttribute('href')`);check(href,'Created account navigation missing');await admin.go(href);};
  const operation=async(kind,reason='Disposable lifecycle acceptance',roles)=>{await manage();const fields={Reason:reason};if(roles)fields.Roles=roles;await admin.fill(fields);await admin.submit(`button[name="Operation"][value="${kind}"]`,reason==='');};
  const revoked=async page=>{await page.go('/Candidate');check(await page.pathname()==='/Account/Login','Existing session still authorised');};
  const passed=[];
  stage='activation expiry, supersession and single use';
  await login(admin,cfg.adminName,cfg.password,'/Admin');await admin.go('/Admin/Users/Create');await admin.fill({'Input.UserName':cfg.candidateName,'Input.Email':cfg.candidateName+'@example.invalid','Input.Role':'Candidate'});await admin.submit();const expiredActivation=await receipt();
  await login(old,cfg.candidateName,cfg.password,'/Account/Login');
  await control('expire');await redeem(expiredActivation,cfg.password,false);
  await operation('Activation');const superseded=await receipt();await operation('Activation');const activation=await receipt();await redeem(superseded,cfg.password,false);await redeem(activation,cfg.password,true);await redeem(activation,cfg.password,false);await control('membership');
  await login(old,cfg.candidateName,cfg.password,'/Candidate');await login(second,cfg.candidateName,cfg.password,'/Candidate');passed.push(stage);console.log('PASS: '+stage);
  stage='recovery expiry, single use and session invalidation';
  await operation('Recovery');const expiredRecovery=await receipt();await revoked(old);await control('expire');await redeem(expiredRecovery,cfg.nextPassword,false);
  await operation('Recovery');const recovery=await receipt();await login(second,cfg.candidateName,cfg.password,'/Candidate');await redeem(recovery,cfg.nextPassword,true);await revoked(second);await redeem(recovery,cfg.nextPassword,false);await login(old,cfg.candidateName,cfg.password,'/Account/Login');await login(fresh,cfg.candidateName,cfg.nextPassword,'/Candidate');passed.push(stage);console.log('PASS: '+stage);
  stage='deactivation blocks login and existing sessions';
  await control('end-membership');await operation('Deactivate');check((await admin.text()).includes('Inactive'),'Deactivation status missing');await revoked(fresh);await login(old,cfg.candidateName,cfg.nextPassword,'/Account/Login');passed.push(stage);console.log('PASS: '+stage);
  stage='reasoned reactivation requires fresh activation';
  await operation('Activation','');check(!(await admin.evaluate(`!!document.querySelector('[role="status"] code')`)),'Blank reason issued activation');check((await admin.text()).includes('Inactive'),'Blank reason enabled account');
  await operation('Activation');const reactivation=await receipt();await login(old,cfg.candidateName,cfg.nextPassword,'/Account/Login');await redeem(activation,cfg.nextPassword,false);await redeem(reactivation,cfg.nextPassword,true);await login(old,cfg.candidateName,cfg.nextPassword,'/Candidate');passed.push(stage);console.log('PASS: '+stage);
  stage='role changes revoke stale candidate access';
  await operation('Update','Disposable role correction',['Mentor']);await revoked(old);await login(fresh,cfg.candidateName,cfg.nextPassword,'/Mentor');passed.push(stage);console.log('PASS: '+stage);
  stage='last active administrator cannot deactivate or remove own administrator role';
  await admin.go('/Admin/Users');const own=await admin.evaluate(`[...document.querySelectorAll('td a')].find(a=>a.textContent===${JSON.stringify(cfg.adminName)})?.getAttribute('href')`);check(own,'Administrator navigation missing');
  await admin.go(own);await admin.fill({Reason:'Acceptance last-administrator protection'});await admin.submit('button[value="Deactivate"]');check((await admin.text()).includes('The last active Administrator cannot lose access.'),'Last administrator deactivation accepted');
  await admin.go(own);await admin.fill({Reason:'Acceptance last-administrator role protection',Roles:['Candidate']});await admin.submit('button[value="Update"]');check((await admin.text()).includes('The last active Administrator cannot lose access.'),'Last administrator role removal accepted');await admin.go('/Admin');check(await admin.pathname()==='/Admin','Last administrator lost session');passed.push(stage);console.log('PASS: '+stage);
  fs.writeFileSync(path.join(output,'acceptance.json'),JSON.stringify({passed,transport:'Live HTTPS Kestrel application with actual Razor/Identity endpoints and isolated SQL Express',simulations:['Activation and recovery expiry: only current disposable credential timestamps adjusted','Membership ended via existing service; DB rowversion/history verified by C# after browser exit'],credentialsPersisted:false,browserChecksCompleted:true,assertionRevision:'http-status-and-denial-v1'},null,2));
 }finally{
  if(browser){try{await browser.send('Browser.close');}catch{}}
  connections.forEach(c=>c.close());if(chrome.exitCode===null)chrome.kill();
  // Remove only this invocation's exact, newly created temporary browser profile.
  if(path.dirname(profile)===os.tmpdir()&&path.basename(profile).startsWith('CptLifecycleBrowser-')){try{fs.rmSync(profile,{recursive:true,force:true,maxRetries:10,retryDelay:100});}catch{}}
 }
})().catch(error=>{const safe=new Set(['Navigation timeout','Credential receipt missing','Login destination mismatch','Created account navigation missing','Secret input is not masked','Credential result mismatch','Failed credential response redisplays secrets','Isolated fixture control failed','Isolated fixture control unavailable','Browser script failed','Browser command failed','Unexpected page status','Expected login denial missing','Form constraint validation failed']);console.error('FAIL: lifecycle browser stage: '+stage+'; '+(safe.has(error.message)?error.message:'Acceptance assertion failed')+'. No secret diagnostics retained.');process.exit(1);});
