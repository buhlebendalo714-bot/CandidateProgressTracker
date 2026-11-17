// Reuses the accepted live-browser driver; only disposable archive records are used.
const fs=require('node:fs'),path=require('node:path');
module.exports=async({tab,check,cfg,output,setStage})=>{
 const admin=await tab(),candidate=await tab(),mentor=await tab(),passed=[];
 const phase=name=>setStage(name),pass=name=>{passed.push(name);console.log('PASS: '+name);};
 const login=async(page,name,destination)=>{await page.go('/Account/Login');await page.fill({'Input.UserName':name,'Input.Password':cfg.password});await page.submit();check(await page.pathname()===destination,'Login destination mismatch');};
 const history='/Candidate/SubmissionHistory/'+cfg.assignmentId,mentorHistory='/Mentor/SubmissionHistory/'+cfg.assignmentId;
 const download='/Evidence/Download/'+cfg.fileId;
 const archive='/Admin/Archives?ScopeId='+cfg.projectId+'&Kind=Project';
 phase('reasoned project archive retains approved outcomes');
 await login(admin,cfg.adminName,'/Admin');await login(candidate,cfg.candidateName,'/Candidate');await login(mentor,cfg.mentorName,'/Mentor');
 await candidate.go(history);await mentor.go(mentorHistory);
 await admin.go('/Admin/Archives');check(await admin.evaluate(`[...document.querySelectorAll('main a')].some(a=>a.href.toLowerCase().includes(${JSON.stringify(cfg.projectId)}))`),'Archive navigation missing');
 await admin.go(archive);await admin.fill({Reason:'Browser archive acceptance'});await admin.submit();
 check(await admin.evaluate(`document.querySelector('main form button[type="submit"]').textContent.trim()==='Reopen'`),'Project did not archive');
 await admin.evaluate(`for(const d of document.querySelectorAll('main details'))d.open=true`);
 check(await admin.evaluate(`[...document.querySelectorAll('main details tbody tr')].some(r=>r.textContent.includes('Approved (Required)'))`),'Saved approved outcome missing');
 check((await admin.text()).includes('Browser archive acceptance'),'Archive reason missing');pass('reasoned project archive retains approved outcomes');
 phase('administrator archived history and exact-byte protected attachment');
 const adminHistory=await admin.evaluate(`[...document.querySelectorAll('main a')].find(a=>a.href.includes(${JSON.stringify(cfg.assignmentId)}))?.getAttribute('href')`);check(adminHistory,'Archived history navigation missing');
 await admin.go(adminHistory);check((await admin.text()).includes(cfg.receiptId),'Original receipt missing');
 check(await admin.evaluate(`[...document.querySelectorAll('main h4')].some(h=>/^Review \\d+: Approved$/.test(h.textContent.trim()))`),'Historical approval missing');
 const file=await admin.evaluate(`(async()=>{const r=await fetch(${JSON.stringify(download)});return {status:r.status,attachment:r.headers.get('content-disposition'),cache:r.headers.get('cache-control'),bytes:[...new Uint8Array(await r.arrayBuffer())]};})()`);
 check(file.status===200&&file.attachment?.includes('attachment')&&file.cache?.includes('no-store'),'Archived attachment headers unsafe');
 check(Buffer.from(file.bytes).equals(fs.readFileSync('TestResults/operational-acceptance/samples/certificate.pdf')),'Archived attachment changed');pass('administrator archived history and exact-byte protected attachment');
 phase('current candidate and mentor denied archived direct routes');
 for(const [page,routes] of [[candidate,[history,download]],[mentor,[mentorHistory,'/Mentor/ReviewSubmission/'+cfg.assignmentId,download]]]){
  for(const route of routes){const result=await page.evaluate(`fetch(${JSON.stringify(route)}).then(async r=>({status:r.status,leaked:(await r.text()).includes(${JSON.stringify(cfg.receiptId)})}))`);check(result.status===404&&!result.leaked,'Archived scope disclosed to learner or mentor');}
 }
 pass('current candidate and mentor denied archived direct routes');
 phase('reasoned scope reopening restores reads without reopening approval');
 await admin.go(archive);await admin.fill({Reason:'Browser scope reopening acceptance'});await admin.submit();
 check(await admin.evaluate(`document.querySelector('main form button[type="submit"]').textContent.trim()==='Archive'`),'Scope reopening failed');
 check((await admin.text()).includes('Browser scope reopening acceptance'),'Reopening reason missing');
 await candidate.go(history);await mentor.go(mentorHistory);
 check(await candidate.evaluate(`[...document.querySelectorAll('main h4')].some(h=>/^Review \\d+: Approved$/.test(h.textContent.trim()))`),'Scope reopening lost approval');
 for(const page of [candidate,mentor])check(await page.evaluate(`fetch(${JSON.stringify(download)}).then(r=>r.status)`)===200,'Scope reopening did not restore authorized download');
 await mentor.go('/Mentor/ReviewSubmission/'+cfg.assignmentId);check(await mentor.evaluate(`!document.querySelector('main form select[name="Input.Decision"]')`),'Scope reopening enabled another review');
 pass('reasoned scope reopening restores reads without reopening approval');
 phase('administrator finds archive audit events with actor action record and date filters');
 await admin.go('/Admin/Audit');const dates=await admin.evaluate(`(()=>{const value=(name,offset)=>{const i=document.getElementsByName(name)[0];const d=new Date(Date.now()+offset*86400000).toISOString();return i.type==='datetime-local'?d.slice(0,16):i.type==='date'?d.slice(0,10):d;};return {From:value('From',-1),To:value('To',1)};})()`);
 await admin.fill({Actor:cfg.adminId,Action:'Added',Record:'ScopeArchiveEvent',...dates});await admin.submit();
 const auditIds=await admin.evaluate(`[...document.querySelectorAll('main tbody tr')].map(r=>({actor:r.cells[1].textContent.trim(),record:r.cells[2].textContent.trim(),action:r.cells[3].textContent.trim()}))`);
 check(auditIds.length===2&&auditIds.every(r=>r.actor===cfg.adminId&&r.record.startsWith('ScopeArchiveEvent ')&&r.action==='Added'),'Filtered archive audit events missing');
 pass('administrator finds archive audit events with actor action record and date filters');
 fs.writeFileSync(path.join(output,'acceptance.json'),JSON.stringify({passed,browserChecksCompleted:true,transport:'Live HTTPS Kestrel/Razor/Identity with real forms, isolated SQL and private evidence',credentialsPersisted:false},null,2));
};
