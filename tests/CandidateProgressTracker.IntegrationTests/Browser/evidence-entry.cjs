// Run EvidenceEntryPageTests first, copy its TestResults/evidence-entry/*.html to
// repository TestResults/evidence-entry, then run this script from the repo root.
// Uses actual HTTP-rendered editor snapshots, Node built-ins and existing Chrome.
const fs=require('node:fs'),path=require('node:path'),{spawn}=require('node:child_process');
(async()=>{
 const root=path.resolve('TestResults/evidence-entry');fs.mkdirSync(root,{recursive:true});
 const chromePath=process.env.CHROME_PATH||'C:/Program Files/Google/Chrome/Application/chrome.exe';
 if(!fs.existsSync(chromePath))throw new Error('Existing Chrome required for opt-in visual verification.');
 const chrome=spawn(chromePath,['--headless','--disable-gpu','--no-first-run','--no-default-browser-check','--remote-debugging-port=9228','--user-data-dir='+path.join(root,'profile-'+Date.now()),'about:blank'],{windowsHide:true,stdio:'ignore'});
 let ws;
 try{
  let ready=false;const deadline=Date.now()+25000;
  while(Date.now()<deadline){try{await(await fetch('http://127.0.0.1:9228/json/version')).json();ready=true;break;}catch{await new Promise(r=>setTimeout(r,100));}}
  if(!ready)throw new Error('Chrome did not become ready.'); console.log('Visual check: Chrome ready');
  const target=await(await fetch('http://127.0.0.1:9228/json/new?about:blank',{method:'PUT'})).json();
  ws=new WebSocket(target.webSocketDebuggerUrl);await new Promise((resolve,reject)=>{ws.onopen=resolve;ws.onerror=reject;});
  let sequence=0;const pending=new Map(),events=new Map(),errors=[];
  ws.onmessage=event=>{const m=JSON.parse(event.data);if(m.id){const p=pending.get(m.id);pending.delete(m.id);m.error?p.reject(new Error(JSON.stringify(m.error))):p.resolve(m.result);}else{if(m.method==='Runtime.exceptionThrown')errors.push(m.params.exceptionDetails.text);if(events.has(m.method)){events.get(m.method)();events.delete(m.method);}}};
  const send=(method,params={})=>new Promise((resolve,reject)=>{const id=++sequence;pending.set(id,{resolve,reject});ws.send(JSON.stringify({id,method,params}));});
  const evaluate=async expression=>{const r=await send('Runtime.evaluate',{expression,returnByValue:true,awaitPromise:true});if(r.exceptionDetails)throw new Error(JSON.stringify(r.exceptionDetails));return r.result.value;};
  await send('Page.enable'); await send('Runtime.enable');
  const script=fs.readFileSync(path.resolve('src/CandidateProgressTracker.Web/wwwroot/js/evidence-links.js'),'utf8');
  for(const kind of ['individual','team']) {
    const html=fs.readFileSync(path.join(root,kind+'.html'),'utf8');
    await send('Page.navigate',{url:'about:blank'});
    await send('Page.setDocumentContent',{frameId:(await send('Page.getFrameTree')).frameTree.frame.id,html:'<!doctype html><html><body>'+html+'</body></html>'});
    await evaluate(script);
    const result=await evaluate(`(()=>{
      const editor=document.querySelector('[data-evidence-editor]'),rows=editor.querySelector('[data-link-rows]'),add=editor.querySelector('[data-add-link]');
      const check=(ok,message)=>{if(!ok)throw Error(message)};
      check(rows.children.length===1,'Initial row');
      rows.children[0].querySelector('input').value='First';
      add.click(); check(document.activeElement===rows.children[1].querySelector('input'),'Add focus');
      rows.children[1].querySelector('input').value='Middle';
      add.click(); rows.children[2].querySelector('input').value='Last';
      rows.children[1].querySelector('[data-remove-link]').click();
      check(rows.children[0].querySelector('input').value==='First'&&rows.children[1].querySelector('input').value==='Last','Remove preserves values');
      check(document.activeElement===rows.children[1].querySelector('input'),'Remove focus');
      check(rows.children[1].querySelector('input').name==='Input.Evidence[1].Label','Contiguous binding');
      while(rows.children.length<10)add.click();
      check(add.disabled,'Ten row maximum');
      rows.lastElementChild.querySelector('[data-remove-link]').click();check(!add.disabled,'Add reenabled');
      check([...rows.querySelectorAll('label')].every(label=>document.getElementById(label.htmlFor)),'Label associations');
      check(new Set([...rows.querySelectorAll('input')].map(e=>e.id)).size===18,'Unique IDs');
      add.focus(); return {rows:rows.children.length,valuesPreserved:true,focus:true,bindings:true};
    })()`);
    // Use real keyboard activation for the final add (rather than a synthetic click).
    await send('Input.dispatchKeyEvent',{type:'keyDown',key:'Enter',code:'Enter',windowsVirtualKeyCode:13,text:'\r'});
    await send('Input.dispatchKeyEvent',{type:'keyUp',key:'Enter',code:'Enter',windowsVirtualKeyCode:13});
    if(!await evaluate(`document.querySelector('[data-link-rows]').children.length===10 && document.activeElement.name==='Input.Evidence[9].Label'`))throw Error('Keyboard add failed: '+JSON.stringify(await evaluate(`({count:document.querySelector('[data-link-rows]').children.length,focus:document.activeElement.outerHTML})`)));
    console.log(kind,JSON.stringify(result));
  }
  if(errors.length)throw Error(errors.join(','));
  await send('Browser.close'); ws.close();
 }finally{if(ws)ws.close();if(chrome.exitCode===null)chrome.kill();}
})().catch(error=>{console.error(error);process.exit(1);});
