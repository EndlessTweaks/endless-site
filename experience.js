/* Endless Website 10.0 — public read-only product experience. No account secrets. */
(() => {
  'use strict';
  const $ = id => document.getElementById(id);
  const reduce = matchMedia('(prefers-reduced-motion: reduce)');
  const API = 'https://api.github.com/repos/EndlessTweaks/endless-site/releases';
  const RELEASES = 'https://github.com/EndlessTweaks/endless-site/releases';
  const ASSET = 'EndlessTweaks-Consumer-win-x64.exe';
  let noticeTimer;
  function notify(message) { $('toast').textContent = message; $('toast').classList.add('visible'); clearTimeout(noticeTimer); noticeTimer = setTimeout(() => $('toast').classList.remove('visible'), 4200); }
  function node(tag, text, className) { const el = document.createElement(tag); if (text !== undefined) el.textContent = text; if (className) el.className = className; return el; }
  const debounce = (fn, ms = 150) => { let timer; return (...args) => { clearTimeout(timer); timer = setTimeout(() => fn(...args), ms); }; };
  async function fetchJson(url) {
    const controller = new AbortController(), timer = setTimeout(() => controller.abort(), 9000);
    try { const response = await fetch(url, {signal: controller.signal, headers: {Accept: 'application/vnd.github+json'}}); if (!response.ok) throw new Error(`Service returned ${response.status}`); return await response.json(); } finally { clearTimeout(timer); }
  }
  function officialUrl(value) { try { const u = new URL(value); return u.protocol === 'https:' && u.hostname === 'github.com' && u.pathname.startsWith('/EndlessTweaks/endless-site/releases/') ? u.href : null; } catch { return null; } }
  function dateText(value) { const d = new Date(value); return Number.isNaN(d.getTime()) ? 'Date unavailable' : d.toLocaleDateString(undefined, {year: 'numeric', month: 'long', day: 'numeric'}); }
  const releaseLines = value => String(value || '').split(/\r?\n/).map(s => s.replace(/^\s*[-*#>]+\s*/, '').trim()).filter(s => s && !/endless[\s-]*zero/i.test(s));
  async function copyText(value, label) { try { await navigator.clipboard.writeText(value); notify(label + ' copied.'); } catch { notify('Clipboard unavailable. Select and copy the visible information manually.'); } }

  // Keyboard-friendly dropdown and feature search.
  const more = document.querySelector('.nav-more');
  more.querySelectorAll('a').forEach(a => a.addEventListener('click', () => { more.open = false; }));
  document.addEventListener('click', e => { if (!more.contains(e.target)) more.open = false; });
  document.addEventListener('keydown', e => { if (e.key === 'Escape' && more.open) { more.open = false; more.querySelector('summary').focus(); } });
  const featureCards = [...document.querySelectorAll('.feature-card')];
  $('featureSearch').addEventListener('input', debounce(() => {
    const q = $('featureSearch').value.trim().toLowerCase(); let count = 0;
    featureCards.forEach(card => { card.hidden = !card.textContent.toLowerCase().includes(q); if (!card.hidden) count++; });
    $('featureCount').textContent = count ? `${count} feature areas` : 'No features match. Try Windows, controller, or BIOS.';
  }));
  // Consistent inline line icons, using our own small SVG paths.
  const icons = {gaming:'M4 17L10 11L14 14L21 5M16 5H21V10',input:'M12 3V9M12 15V21M3 12H9M15 12H21M9 9H15V15H9Z',windows:'M3 3H10V10H3ZM14 3H21V10H14ZM3 14H10V21H3ZM14 14H21V21H14Z',network:'M5 19H19M12 19V12M4 8C8 3 16 3 20 8M7 11C10 8 14 8 17 11',controller:'M6 7H18L22 16Q22 21 18 18L15 15H9L6 18Q2 21 2 16ZM7 9V13M5 11H9M16 10H17M18 12H19',profiles:'M5 4H19V20H5ZM8 8H16M8 12H16M8 16H12',restore:'M4 9A8 8 0 1 1 4 16M4 3V9H10',plans:'M12 3L21 8V16L12 21L3 16V8ZM12 8V16M8 12H16',bios:'M6 6H18V18H6ZM9 1V5M15 1V5M9 19V23M15 19V23M1 9H5M1 15H5M19 9H23M19 15H23'};
  featureCards.forEach(card => { const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg'), path = document.createElementNS(svg.namespaceURI, 'path'); svg.setAttribute('viewBox','0 0 24 24'); svg.setAttribute('aria-hidden','true'); path.setAttribute('d',icons[card.dataset.feature]); svg.append(path); card.querySelector('.feature-icon').replaceChildren(svg); });

  const walkthrough = [
    ['Know what changes.', 'Search your plan, check risk and compatibility, then expand a tweak to read its description and proposed value.'],
    ['Choose deliberately.', 'Select supported changes in the Windows app. Pro settings require a verified Pro license. Read the trade-offs and any restart requirement first.'],
    ['Keep your original values.', 'Use Restore for a supported tweak or open Backup & Restore to review saved originals. Restore availability depends on a captured original value.']
  ];
  const steps = [...document.querySelectorAll('[data-step]')];
  function selectStep(index) { steps.forEach((b,i) => { b.setAttribute('aria-selected',String(i===index)); b.tabIndex = i===index ? 0 : -1; }); const panel = $('walkthroughPanel'); panel.setAttribute('aria-labelledby',steps[index].id); panel.replaceChildren(node('h3',walkthrough[index][0]),node('p',walkthrough[index][1])); }
  steps.forEach((b,i) => { b.addEventListener('click',()=>selectStep(i)); b.addEventListener('keydown',e=>{ let n; if(e.key==='ArrowRight') n=(i+1)%3; if(e.key==='ArrowLeft') n=(i+2)%3; if(e.key==='Home') n=0; if(e.key==='End') n=2; if(n!==undefined){e.preventDefault();selectStep(n);steps[n].focus();} }); });
  $('previewZoom').addEventListener('click',()=> $('screenshotDialog').showModal());
  document.querySelectorAll('[data-tip]').forEach(button => button.addEventListener('click',()=>{ $('tipText').textContent=button.dataset.tip; $('tipDialog').showModal(); }));
  // A count, not a performance measurement. Do not animate for reduced motion.
  const counter = document.querySelector('[data-counter]');
  if (!reduce.matches && 'IntersectionObserver' in window) {
    const watch = new IntersectionObserver(entries => { if (!entries[0].isIntersecting) return; watch.disconnect(); const start=performance.now(),target=Number(counter.dataset.counter); function tick(now){if(reduce.matches){counter.textContent=target;return;}const t=Math.min(1,(now-start)/850);counter.textContent=Math.round(target*(1-Math.pow(1-t,3)));if(t<1)requestAnimationFrame(tick);} requestAnimationFrame(tick); }); watch.observe(counter);
  }

  // Catalog: read-only metadata exported from the existing app's registrations.
  let catalog = [], catalogLoaded = false, catalogLoading = false, visibleLimit = 12;
  function matchesTweak(t, q, plan, category, risk) { return (!q || `${t.name} ${t.description} ${t.id} ${t.subcategory}`.toLowerCase().includes(q)) && (plan==='All'||t.plan===plan) && (category==='All'||t.category===category) && (risk==='All'||t.risk===risk); }
  function renderTweaks() {
    if(!catalogLoaded)return;
    const q=$('tweakSearch').value.trim().toLowerCase();
    const matches=catalog.filter(t=>matchesTweak(t,q,$('planFilter').value,$('categoryFilter').value,$('riskFilter').value));
    const fragment=document.createDocumentFragment();
    matches.slice(0,visibleLimit).forEach(t=>{
      const card=node('details',undefined,'tweak-card'); card.dataset.tweakId=t.id;
      const summary=node('summary'), heading=node('div'), badges=node('div',undefined,'tweak-badges');
      heading.append(node('span',t.category+' / '+t.subcategory,'tweak-category'),node('h3',t.name));
      badges.append(node('span',t.plan,t.plan==='Pro'?'badge pro-badge':'badge'),node('span',t.risk+' risk','badge risk-'+t.risk.toLowerCase()));
      summary.append(heading,badges); card.append(summary);
      const content=node('div',undefined,'tweak-content');content.append(node('p',t.description));
      const dl=node('dl');
      const rows=[['Compatibility','Checked in the Windows app. Support depends on your Windows build and hardware; this website does not scan your PC.'],['Restore','Uses the original value saved before a supported change. Availability is checked in the app.'],['Restart',t.restart?'May require a restart; follow the app instructions.':'No restart requirement is declared for this tweak.'],['Location',t.category==='Controller'?'Controller Optimization → Review USB power tweaks':t.plan+' Tweaks'],['Tweak ID',t.id]];
      rows.forEach(([key,value])=>dl.append(node('dt',key),node('dd',value)));content.append(dl);card.append(content);fragment.append(card);
    });
    if(!matches.length){const empty=node('div',undefined,'empty-state');empty.append(node('h3','No tweaks match these filters.'),node('p',$('categoryFilter').value==='RAM'?'The app provides RAM diagnostics and BIOS guidance, but registers no automatic RAM tweaks. Reset filters to browse other categories.':'Try a broader search or reset your filters.'));fragment.append(empty);}
    $('tweakResults').replaceChildren(fragment);$('tweakResults').setAttribute('aria-busy','false');
    $('tweakCount').textContent=`${matches.length} matching / ${catalog.length} total · ${Math.min(visibleLimit,matches.length)} shown`;
    $('loadTweaks').hidden=matches.length<=visibleLimit;
  }
  async function loadCatalog(){if(catalogLoaded||catalogLoading)return;catalogLoading=true;$('retryCatalog').hidden=true;$('tweakResults').setAttribute('aria-busy','true');try{const data=await fetchJson('tweaks.json?v=9.6.1');if(!Array.isArray(data.tweaks)||data.tweaks.length!==225)throw Error('Invalid catalog');catalog=data.tweaks;catalogLoaded=true;renderTweaks();}catch{ $('tweakCount').textContent='The tweak catalog could not load.';$('tweakResults').replaceChildren(node('p','Check your connection, then retry. The official app download is still available.','empty-state'));$('retryCatalog').hidden=false;}finally{catalogLoading=false;$('tweakResults').setAttribute('aria-busy','false');}}
  const changed=()=>{visibleLimit=12;renderTweaks();};$('tweakSearch').addEventListener('input',debounce(changed));['planFilter','categoryFilter','riskFilter'].forEach(id=>$(id).addEventListener('change',changed));
  $('resetExplorer').addEventListener('click',()=>{$('tweakSearch').value='';['planFilter','categoryFilter','riskFilter'].forEach(id=>$(id).value='All');changed();});$('loadTweaks').addEventListener('click',()=>{visibleLimit+=12;renderTweaks();});$('retryCatalog').addEventListener('click',loadCatalog);
  if('IntersectionObserver'in window){const observer=new IntersectionObserver(entries=>{if(entries.some(e=>e.isIntersecting)){observer.disconnect();loadCatalog();}},{rootMargin:'500px'});observer.observe($('explorer'));}else loadCatalog();

  // One release fetch populates all version, date, digest, and changelog surfaces.
  let currentRelease=null, currentDigest='', refreshing=false, historyLoaded=false;
  function validRelease(r){return r && !r.draft && !r.prerelease && /^v?\d+\.\d+\.\d+(?:\.\d+)?$/.test(r.tag_name||'') && Array.isArray(r.assets) && r.assets.some(a=>a.name===ASSET && officialUrl(a.browser_download_url));}
  function renderRelease(r,cached=false){
    currentRelease=r;const asset=r.assets.find(a=>a.name===ASSET);currentDigest=/^sha256:[0-9a-f]{64}$/i.test(asset.digest||'')?asset.digest.slice(7):'';
    $('releaseVersion').textContent=r.tag_name;$('releaseDate').textContent='Published '+dateText(r.published_at);$('downloadStatus').textContent=`${cached?'Cached':'Latest'} release: ${r.tag_name}. Download the official Windows x64 EXE.`;
    $('releaseFreshness').textContent=cached?'Showing previously fetched release details. Refresh when you reconnect.':'Release details loaded from the official GitHub repository.';
    $('releaseNotes').replaceChildren(...releaseLines(r.body).slice(0,4).map(line=>node('li',line)));$('fullChangelog').textContent=r.body||'No release notes were published for this version.';
    $('assetInfo').textContent=`${asset.name} · ${(Number(asset.size)/1048576).toFixed(1)} MB`;$('downloadHash').textContent=currentDigest||'No SHA-256 digest is available from GitHub for this file.';$('copyHash').disabled=!currentDigest;
  }
  async function refreshRelease(manual=false){if(refreshing)return;refreshing=true;$('refreshRelease').disabled=true;$('refreshRelease').textContent='Checking…';let cached;
    try{cached=JSON.parse(sessionStorage.getItem('endless-release-cache')||'null');if(cached&&validRelease(cached.release)&&Date.now()-cached.at<300000&&!manual){renderRelease(cached.release,true);}}catch{}
    try{const r=await fetchJson(API+'/latest');if(!validRelease(r))throw Error('Invalid official release');renderRelease(r);try{sessionStorage.setItem('endless-release-cache',JSON.stringify({at:Date.now(),release:r}));}catch{}if(manual)notify('Release details refreshed.');}
    catch{if(cached&&validRelease(cached.release)){renderRelease(cached.release,true);}else if(!currentRelease){$('releaseVersion').textContent='Version unavailable';$('releaseDate').textContent='Release feed temporarily unavailable.';$('releaseNotes').replaceChildren(node('li','Use the official latest release link or retry.'));$('downloadStatus').textContent='The release feed could not be loaded. The official download link remains available.';$('fullChangelog').textContent='Unable to load release notes. Try Refresh release or open the official release page.';$('assetInfo').textContent='File details unavailable.';$('downloadHash').textContent='Unavailable until release details load.';}$('releaseFreshness').textContent='Could not refresh GitHub details. The direct latest-download link still works.';if(manual)notify('Release details could not load. Please retry.');}
    finally{refreshing=false;$('refreshRelease').disabled=false;$('refreshRelease').textContent='Refresh release';}
  }
  $('refreshRelease').addEventListener('click',()=>refreshRelease(true));$('copyHash').addEventListener('click',()=>{if(currentDigest)copyText(currentDigest,'SHA-256');});
  $('historyToggle').addEventListener('click',async()=>{
    const open=$('historyToggle').getAttribute('aria-expanded')!=='true';$('historyToggle').setAttribute('aria-expanded',String(open));$('releaseHistory').hidden=!open;$('historyToggle').textContent=open?'Hide update history':'View update history';if(!open||historyLoaded)return;
    $('releaseHistory').setAttribute('aria-busy','true');$('releaseHistory').replaceChildren(node('p','Loading official releases…','skeleton history-loading'));
    try{const data=await fetchJson(API+'?per_page=20');if(!Array.isArray(data))throw Error('Invalid history');const releases=data.filter(validRelease);const fragment=document.createDocumentFragment();releases.forEach(r=>{const details=node('details'),summary=node('summary');summary.append(node('strong',r.tag_name),node('span',dateText(r.published_at)));const pre=node('pre',r.body||'No release notes.');const link=node('a','Open official release ↗','quiet-link');link.href=officialUrl(r.html_url)||RELEASES+'/latest';link.target='_blank';link.rel='noopener';details.append(summary,pre,link);fragment.append(details);});if(!releases.length)fragment.append(node('p','No published Endless Tweaks releases were returned.'));$('releaseHistory').replaceChildren(fragment);historyLoaded=true;}catch{$('releaseHistory').replaceChildren(node('p','Release history could not load. Close and reopen it to retry.'));}finally{$('releaseHistory').setAttribute('aria-busy','false');}
  });
  refreshRelease();
  document.querySelectorAll('.product-download').forEach(a=>a.addEventListener('click',()=>notify('Opening the official download. Your browser will show its progress.')));
  $('copySupport').addEventListener('click',()=>copyText(`Endless Tweaks support\nWebsite: https://endlesstweaks.github.io/endless-site/\nLatest listed release: ${currentRelease?currentRelease.tag_name:'unavailable'}\nMy installed version: [enter from app]\nWindows version: [enter yours]\nWhat happened: [describe]\nDo not include license codes, passwords, or personal data.`,'Support template'));

  const existingTopics=['Plans','Plans','Compatibility','Restore','Support'];
  const faqRoot=document.querySelector('.faq');[...faqRoot.children].forEach((el,i)=>{el.dataset.topic=existingTopics[i];});
  const help=[['Installation','How do I install Endless Tweaks?','Use the official Windows x64 EXE in Downloads. Run the file and review Windows prompts. For an existing installation, prefer Update Center to update in place.'],['Compatibility','Will every tweak work on my PC?','No. Windows version, hardware, drivers and available settings determine compatibility. The app checks supported settings and shows Unsupported where appropriate. The website cannot scan your PC.'],['Updates','How do I get the latest app update?','Open Update Center and choose Check for updates. Supported clients verify the signed manifest and package. A verified minimum-version requirement can require an update. Very old clients can download the current EXE manually.'],['Updates','What if the update check times out?','Check your connection and retry. A temporary service error does not prove there is a new update. Use the official release link if details remain unavailable; never substitute an unknown package.'],['Restore','Does Restore reset everything to factory defaults?','Restore puts back captured original values for supported changes. It is not a factory reset. Keep your backups and follow any restart instructions shown by the app.'],['Plans','Where do I redeem my Pro code?','Use Redeem Code in the Windows app. Follow the username and code instructions supplied with your purchase. Do not paste your private license code into a public message. Contact Discord support for access issues.'],['Support','What should I include in a support ticket?','Use Copy support info below, then add your installed app version, Windows version, the exact error, and what you were doing. Remove license codes and personal information from screenshots.']];
  help.forEach(([topic,q,a])=>{const item=node('div',undefined,'faq-item');item.dataset.topic=topic;const button=node('button',undefined,'faq-q');button.type='button';button.setAttribute('aria-expanded','false');button.append(node('span',q),node('span','+'));const answer=node('div',undefined,'faq-answer'),inner=node('div');inner.append(node('p',a));answer.append(inner);button.addEventListener('click',()=>{const open=button.getAttribute('aria-expanded')!=='true';button.setAttribute('aria-expanded',String(open));item.classList.toggle('open',open);});item.append(button,answer);faqRoot.append(item);});
  const faqItems=[...faqRoot.children];faqItems.forEach((item,i)=>{const button=item.querySelector('.faq-q'),answer=item.querySelector('.faq-answer');answer.id='help-answer-'+i;button.setAttribute('aria-controls',answer.id);answer.setAttribute('aria-hidden',button.getAttribute('aria-expanded')==='true'?'false':'true');button.addEventListener('click',()=>answer.setAttribute('aria-hidden',button.getAttribute('aria-expanded')==='true'?'false':'true'));});
  function filterFaq(){const q=$('faqSearch').value.trim().toLowerCase(),topic=$('faqCategory').value;let count=0;faqItems.forEach(item=>{item.hidden=!(item.textContent.toLowerCase().includes(q)&&(topic==='All'||item.dataset.topic===topic));if(!item.hidden)count++;});$('faqCount').textContent=count?`${count} help articles`:'No help articles match. Try another term or reset the filters.';}
  $('faqSearch').addEventListener('input',debounce(filterFaq));$('faqCategory').addEventListener('change',filterFaq);$('resetFaq').addEventListener('click',()=>{$('faqSearch').value='';$('faqCategory').value='All';filterFaq();});filterFaq();
})();
