(() => {
    "use strict";
    try {
        const saved = localStorage.getItem("readerTheme");
        if (saved === "dark" || saved === "light") document.documentElement.dataset.theme = saved;
    } catch { /* Browser storage may be disabled. */ }
    document.getElementById("themeToggle")?.addEventListener("click", () => {
        const theme = document.documentElement.dataset.theme;
        const dark = theme ? theme === "dark" : window.matchMedia("(prefers-color-scheme: dark)").matches;
        const next = dark ? "light" : "dark";
        document.documentElement.dataset.theme = next;
        try { localStorage.setItem("readerTheme", next); } catch { }
    });
})();
(() => {const content=document.getElementById("readerContent");if(!content)return;
let size=18;try{const saved=Number(localStorage.getItem("readerFontSize"));if(saved>=14&&saved<=26)size=saved;}catch{}
const apply=delta=>{size=Math.max(14,Math.min(26,size+delta));content.style.setProperty("--reader-size",size+"px");try{localStorage.setItem("readerFontSize",String(size));}catch{}};
apply(0);document.getElementById("fontSmaller")?.addEventListener("click",()=>apply(-1));document.getElementById("fontBigger")?.addEventListener("click",()=>apply(1));})();
