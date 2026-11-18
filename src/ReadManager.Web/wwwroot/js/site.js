(() => {
    "use strict";
    // 1. Dark / Light theme toggle
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

    // 2. Auto-close <details class="dd"> dropdowns when clicking outside or pressing Escape
    document.addEventListener("click", (e) => {
        document.querySelectorAll("details.dd[open]").forEach((dd) => {
            if (!dd.contains(e.target)) dd.removeAttribute("open");
        });
    });
    document.addEventListener("keydown", (e) => {
        if (e.key === "Escape") {
            document.querySelectorAll("details.dd[open]").forEach((dd) => dd.removeAttribute("open"));
        }
    });

    // 3. Password visibility toggle buttons (.pw-eye)
    document.querySelectorAll(".pw-eye").forEach((btn) => {
        btn.addEventListener("click", () => {
            const input = btn.parentElement?.querySelector("input");
            if (!input) return;
            const show = input.type === "password";
            input.type = show ? "text" : "password";
            btn.textContent = show ? "🙈" : "👁";
            btn.setAttribute("aria-label", show ? "Ẩn mật khẩu" : "Hiện mật khẩu");
        });
    });

    // 4. Auto-dismiss floating toast after 5 seconds
    const toast = document.getElementById("globalToast");
    if (toast) {
        toast.querySelector(".toast-close")?.addEventListener("click", () => toast.remove());
        setTimeout(() => { if (toast.parentNode) toast.remove(); }, 5500);
    }
})();

(() => {
    "use strict";
    const content = document.getElementById("readerContent");
    if (!content) return;

    // Reader font size
    let size = 18;
    try {
        const saved = Number(localStorage.getItem("readerFontSize"));
        if (saved >= 14 && saved <= 26) size = saved;
    } catch { }

    const sizeLabel = document.getElementById("fontSizeVal");
    const applySize = (delta) => {
        size = Math.max(14, Math.min(26, size + delta));
        content.style.setProperty("--reader-size", size + "px");
        if (sizeLabel) sizeLabel.textContent = size + "px";
        try { localStorage.setItem("readerFontSize", String(size)); } catch { }
    };
    applySize(0);
    document.getElementById("fontSmaller")?.addEventListener("click", () => applySize(-1));
    document.getElementById("fontBigger")?.addEventListener("click", () => applySize(1));

    // Reader paper background theme (default | sepia | night)
    const paperBtns = document.querySelectorAll("[data-paper-btn]");
    const applyPaper = (mode) => {
        if (mode === "sepia" || mode === "night") content.dataset.paper = mode;
        else delete content.dataset.paper;
        paperBtns.forEach((b) => b.classList.toggle("active", b.dataset.paperBtn === mode));
        try { localStorage.setItem("readerPaperTheme", mode); } catch { }
    };
    try {
        const savedPaper = localStorage.getItem("readerPaperTheme") || "default";
        applyPaper(savedPaper);
    } catch { }
    paperBtns.forEach((b) => b.addEventListener("click", () => applyPaper(b.dataset.paperBtn)));
})();
