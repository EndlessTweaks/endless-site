
    (() => {
      "use strict";
      const SITE={api:"https://api.github.com/repos/EndlessTweaks/endless-site/releases/latest",discord:"https://discord.gg/4qNSaAs9qr",announcement:{active:false,title:"Endless Tweaks update",message:"A new Endless Tweaks update is available.",link:"#downloads"}};
      const root=document.documentElement,nav=document.getElementById("siteNav"),menu=document.querySelector(".menu-toggle"),toast=document.getElementById("toast");let toastTimer=0;
      {document.querySelectorAll(".product-download").forEach(link=>{link.href=link.dataset.releaseUrl;link.removeAttribute("download")})}
      if(matchMedia("(pointer:fine)").matches&&!matchMedia("(prefers-reduced-motion:reduce)").matches){
        let frame=0,px=0,py=0;window.addEventListener("pointermove",e=>{px=e.clientX;py=e.clientY;if(!frame)frame=requestAnimationFrame(()=>{root.style.setProperty("--mx",px+"px");root.style.setProperty("--my",py+"px");frame=0})},{passive:true});
        const art=document.getElementById("heroArt"),stage=document.getElementById("packStage");
        art.addEventListener("pointermove",e=>{if(document.getElementById("top").classList.contains("motion-paused"))return;const r=art.getBoundingClientRect(),x=(e.clientX-r.left)/r.width,y=(e.clientY-r.top)/r.height;stage.style.transform="rotateY("+((x-.5)*8)+"deg) rotateX("+((.5-y)*5)+"deg)"},{passive:true});
        art.addEventListener("pointerleave",()=>{stage.style.transform=""});
        document.querySelectorAll(".feature-card").forEach(card=>card.addEventListener("pointermove",e=>{const r=card.getBoundingClientRect();card.style.setProperty("--lx",((e.clientX-r.left)/r.width*100)+"%");card.style.setProperty("--ly",((e.clientY-r.top)/r.height*100)+"%")},{passive:true}));
      }

      const hero=document.getElementById("top"),motionToggle=document.getElementById("motionToggle"),motionIcon=document.getElementById("motionIcon"),reducedMotion=matchMedia("(prefers-reduced-motion:reduce)");
      let motionPaused=reducedMotion.matches;
      try{motionPaused=motionPaused||localStorage.getItem("endless-motion-paused")==="true"}catch(e){}
      function setMotion(paused){motionPaused=paused;hero.classList.toggle("motion-paused",paused);motionToggle.setAttribute("aria-pressed",String(paused));motionToggle.setAttribute("aria-label",paused?"Resume background animation":"Pause background animation");motionIcon.textContent=paused?"▶":"Ⅱ";motionToggle.disabled=reducedMotion.matches;if(reducedMotion.matches)motionToggle.setAttribute("aria-label","Animation disabled by reduced motion preference")}
      setMotion(motionPaused);motionToggle.addEventListener("click",()=>{setMotion(!motionPaused);try{localStorage.setItem("endless-motion-paused",String(motionPaused))}catch(e){}});reducedMotion.addEventListener("change",()=>setMotion(reducedMotion.matches||motionPaused));
      if(menu){menu.addEventListener("click",()=>{const open=menu.getAttribute("aria-expanded")==="true";menu.setAttribute("aria-expanded",String(!open));menu.setAttribute("aria-label",open?"Open navigation menu":"Close navigation menu");nav.classList.toggle("open",!open)});
        nav.querySelectorAll("a").forEach(a=>a.addEventListener("click",()=>{nav.classList.remove("open");menu.setAttribute("aria-expanded","false");menu.setAttribute("aria-label","Open navigation menu")}));
        document.addEventListener("keydown",e=>{if(e.key==="Escape"){nav.classList.remove("open");menu.setAttribute("aria-expanded","false");menu.setAttribute("aria-label","Open navigation menu")}})
      }
      if("IntersectionObserver"in window){
        const navLinks=Array.from(nav.querySelectorAll("a[href^='#']"));

        const sections=document.querySelectorAll("main section[id]"),observer=new IntersectionObserver(entries=>entries.forEach(entry=>{if(entry.isIntersecting){navLinks.forEach(a=>a.getAttribute("href")==="#"+entry.target.id?a.setAttribute("aria-current","location"):a.removeAttribute("aria-current"))}}),{rootMargin:"-25% 0px -65% 0px"});sections.forEach(s=>observer.observe(s));
        if(!matchMedia("(prefers-reduced-motion:reduce)").matches){const reveal=new IntersectionObserver((entries,obs)=>entries.forEach(e=>{if(e.isIntersecting){e.target.classList.add("in");obs.unobserve(e.target)}}),{threshold:.12});document.querySelectorAll("[data-reveal]").forEach(el=>reveal.observe(el))}
        else document.querySelectorAll("[data-reveal]").forEach(el=>el.classList.add("in"))
      }else document.querySelectorAll("[data-reveal]").forEach(el=>el.classList.add("in"));
      function say(text){toast.textContent=text;toast.classList.add("visible");clearTimeout(toastTimer);toastTimer=setTimeout(()=>toast.classList.remove("visible"),2700)}
      document.querySelectorAll("dialog.modal").forEach(dialog=>{dialog.querySelectorAll("[data-close]").forEach(c=>c.addEventListener("click",()=>dialog.close()));dialog.addEventListener("click",e=>{if(e.target===dialog)dialog.close()})});
      const data={
        bios:["BIOS guidance","Manual, hardware-aware BIOS guides explain supported options, trade-offs, and how to undo a change. The website cannot read or modify your firmware. Review the guidance for your hardware inside the Windows app."],
        gaming:["Gaming settings","Review Windows gaming and power preferences, apply supported changes, and restore saved values. Performance depends on your own PC and game; no specific frame-rate increase is promised."],
        input:["Input & latency","Endless Tweaks brings current-user mouse and keyboard preferences together with input diagnostics for supported devices."],
        windows:["Windows tuning","The library covers supported Windows preferences, power settings, and shell options. Availability depends on the Windows build and device."],
        network:["Network checks","Inspect adapter and DNS details or run a basic ping check on demand. It is not an internet speed test and cannot guarantee lower game ping."],
        controller:["Controller tools","Endless Tweaks provides a live XInput view for controller sticks, triggers, and buttons. Related USB power settings appear where Windows supports them."],
        profiles:["Game profiles","Endless Tweaks scans common Steam and Epic installation records read-only. It detects locations and does not edit game files automatically."],
        restore:["Restore Center","Supported changes save previous values for review and restore. Unsupported settings remain clearly identified in the application."],
        plans:["Free & Pro","Endless Tweaks includes a Free settings library. Lifetime Pro unlocks additional settings in the same app. Ask the Discord team for current access and pricing details."]
      };
      const featureDialog=document.getElementById("featureDialog");
      document.querySelectorAll(".feature-card .learn").forEach(button=>button.addEventListener("click",()=>{const d=data[button.closest(".feature-card").dataset.feature];document.getElementById("featureTitle").textContent=d[0];document.getElementById("featureText").textContent=d[1];featureDialog.showModal()}));
      document.querySelectorAll("[data-dialog]").forEach(button=>button.addEventListener("click",()=>{const d=document.getElementById(button.dataset.dialog);if(d)d.showModal()}));
      document.querySelectorAll(".faq-q").forEach(button=>button.addEventListener("click",()=>{const open=button.getAttribute("aria-expanded")==="true";button.setAttribute("aria-expanded",String(!open));button.closest(".faq-item").classList.toggle("open",!open)}));
      document.getElementById("copyInvite").addEventListener("click",async()=>{try{await navigator.clipboard.writeText(SITE.discord);say("Discord invite copied.")}catch(e){say("Copy unavailable. Use the Open Discord link instead.")}});

      const announcement=SITE.announcement,bar=document.getElementById("announcement");
      if(announcement.active&&localStorage.getItem("endless-announcement-dismissed")!==announcement.title){bar.classList.add("show");document.getElementById("announcementText").textContent=announcement.message;document.getElementById("announcementLink").href=announcement.link;document.getElementById("announcementLink").textContent=announcement.title;document.getElementById("announcementDialogText").textContent=announcement.message;document.getElementById("announcementDialogLink").href=announcement.link;setTimeout(()=>{if(!document.querySelector("dialog[open]"))document.getElementById("announcementDialog").showModal()},800);bar.querySelector("button").addEventListener("click",()=>{localStorage.setItem("endless-announcement-dismissed",announcement.title);bar.classList.remove("show")})}
      document.getElementById("welcomeDialog").addEventListener("close",()=>{try{localStorage.setItem("endless-welcome-dismissed","true")}catch(e){}});
      document.querySelectorAll("[data-dismiss]").forEach(a=>a.addEventListener("click",()=>{try{localStorage.setItem("endless-welcome-dismissed","true")}catch(e){};document.getElementById("welcomeDialog").close()}));
    })();
  