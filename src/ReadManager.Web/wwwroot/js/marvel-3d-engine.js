/* ==========================================================================
   FOLIO — GPU SCROLL-DRIVEN 3D SPATIAL ENGINE & ZERO-GRAVITY FLOATING DECK
   ========================================================================== */
document.addEventListener("DOMContentLoaded", () => {
    const prefersReducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    const clamp = (val, min, max) => Math.min(Math.max(val, min), max);
    const lerp = (start, end, amt) => start + (end - start) * amt;

    // 1. Đồng bộ nút Sáng / Tối (#themeToggle)
    const themeBtn = document.getElementById("themeToggle");
    const rootHtml = document.documentElement;

    function syncThemeIcon() {
        if (!themeBtn) return;
        const isLight = rootHtml.getAttribute("data-theme") === "light";
        themeBtn.textContent = isLight ? "☀" : "☾";
        themeBtn.setAttribute("title", isLight ? "Đang ở chế độ Sáng (Bấm để chuyển Tối)" : "Đang ở chế độ Tối (Bấm để chuyển Sáng)");
    }
    syncThemeIcon();

    if (themeBtn) {
        themeBtn.addEventListener("click", () => {
            setTimeout(() => {
                const current = rootHtml.getAttribute("data-theme");
                if (current !== "light" && current !== "dark") {
                    rootHtml.setAttribute("data-theme", "light");
                    try { localStorage.setItem("theme", "light"); } catch { }
                }
                syncThemeIcon();
            }, 0);
        });
    }

    // 2. Lấy các phần tử sân khấu 3D
    const progressBar = document.getElementById("scrollProgressBar");
    const scrollTrack = document.getElementById("cdScrollTrack");
    const depthGrid = document.getElementById("cdDepthGrid");
    const depthNebula = document.getElementById("cdDepthNebula");
    const spatialCopy = document.getElementById("cdSpatialCopy");
    const bookEl = document.getElementById("mv3dBook");
    const gyroOuter = document.getElementById("cdGyroOuter");
    const gyroInner = document.getElementById("cdGyroInner");
    const orbitCards = Array.from(document.querySelectorAll("#cdOrbitField .cd-orbit-card"));

    // Quỹ đạo Z-depth & thông số bay tự do cho từng thẻ vệ tinh
    const orbitConfigs = [
        { zStart: -140, zEnd: 260, xStart: 12, xEnd: 105, yStart: -10, yEnd: -72, rotY: -20, rotZ: 7, speed: 1.05, phase: 0.0 },
        { zStart: -90, zEnd: 310, xStart: -8, xEnd: -88, yStart: 8, yEnd: 82, rotY: 18, rotZ: -6, speed: 0.92, phase: 1.7 },
        { zStart: -190, zEnd: 220, xStart: -10, xEnd: -95, yStart: -12, yEnd: -84, rotY: 22, rotZ: 9, speed: 1.15, phase: 3.3 },
        { zStart: -160, zEnd: 280, xStart: 10, xEnd: 120, yStart: 10, yEnd: 68, rotY: -19, rotZ: -8, speed: 0.98, phase: 4.9 }
    ];

    // Theo dõi trạng thái rê chuột từng thẻ vệ tinh để hút về phía trước mượt mà
    let hoveredOrbitIdx = -1;
    const orbitHoverLerp = orbitCards.map(() => 0);
    orbitCards.forEach((card, idx) => {
        card.addEventListener("mouseenter", () => { hoveredOrbitIdx = idx; });
        card.addEventListener("mouseleave", () => { if (hoveredOrbitIdx === idx) hoveredOrbitIdx = -1; });
    });

    // Các khối nội dung Z-depth bên dưới
    const zBlocks = Array.from(document.querySelectorAll(".cd-z-block"));

    // Bộ nhớ đệm vị trí bố cục (tránh forced reflow khi cuộn)
    let trackTop = 0;
    let trackSpan = 1;
    let docMaxScroll = 1;
    let winHeight = window.innerHeight || 800;
    let blockMetrics = [];

    function cacheLayoutMetrics() {
        winHeight = window.innerHeight || 800;
        docMaxScroll = Math.max(document.documentElement.scrollHeight - winHeight, 1);

        if (scrollTrack) {
            const rect = scrollTrack.getBoundingClientRect();
            trackTop = rect.top + window.scrollY;
            trackSpan = Math.max(scrollTrack.offsetHeight - winHeight, 1);
        }

        blockMetrics = zBlocks.map((el) => {
            let top = 0;
            let curr = el;
            while (curr) {
                top += curr.offsetTop;
                curr = curr.offsetParent;
            }
            return {
                el,
                top,
                height: el.offsetHeight || 200,
                depthFactor: parseFloat(el.getAttribute("data-z-depth")) || 1
            };
        });
    }

    cacheLayoutMetrics();
    window.addEventListener("resize", cacheLayoutMetrics, { passive: true });
    window.addEventListener("load", cacheLayoutMetrics, { passive: true });

    // Theo dõi con trỏ chuột (Parallax nhẹ kết hợp cùng quỹ đạo bay)
    let targetMouseX = 0, targetMouseY = 0;
    let currentMouseX = 0, currentMouseY = 0;

    window.addEventListener("mousemove", (e) => {
        targetMouseX = (e.clientX / window.innerWidth - 0.5) * 2;
        targetMouseY = (e.clientY / window.innerHeight - 0.5) * 2;
    }, { passive: true });

    // Vòng lặp kết xuất GPU 60-120FPS duy nhất
    let currentScrollY = window.scrollY;
    let currentHeroP = 0;

    function renderSpatialFrame(now) {
        const rawScrollY = window.scrollY;
        const t = now * 0.001;
        currentScrollY = lerp(currentScrollY, rawScrollY, 0.12);
        currentMouseX = lerp(currentMouseX, targetMouseX, 0.08);
        currentMouseY = lerp(currentMouseY, targetMouseY, 0.08);

        if (progressBar) {
            const pct = clamp((currentScrollY / docMaxScroll) * 100, 0, 100);
            progressBar.style.width = `${pct.toFixed(2)}%`;
        }

        // A. SÂN KHẤU HERO 3D + THẺ VỆ TINH BAY LƠ LỬNG TỰ ĐỘNG
        if (scrollTrack && !prefersReducedMotion && window.innerWidth > 900) {
            const targetHeroP = clamp((currentScrollY - trackTop) / trackSpan, 0, 1);
            currentHeroP = lerp(currentHeroP, targetHeroP, 0.14);
            const p = currentHeroP;
            const floatWave = Math.sin(t * 2.1) * 6;

            // 1. Lưới không gian & Nebula lùi/tiến theo Z
            if (depthGrid) {
                const gridZ = -420 + p * 260;
                const gridRotX = 72 - p * 14;
                depthGrid.style.transform = `translate3d(${(currentMouseX * -18).toFixed(1)}px, ${(60 - p * 45).toFixed(1)}px, ${gridZ.toFixed(1)}px) rotateX(${gridRotX.toFixed(2)}deg)`;
            }

            if (depthNebula) {
                const nebZ = -340 + p * 220;
                const nebScale = 1 + p * 0.35;
                depthNebula.style.transform = `translate3d(${(currentMouseX * 28).toFixed(1)}px, ${(currentMouseY * 22).toFixed(1)}px, ${nebZ.toFixed(1)}px) scale3d(${nebScale.toFixed(3)}, ${nebScale.toFixed(3)}, 1)`;
            }

            // 2. Các thẻ truyện vệ tinh tự động bay lơ lửng 3 trục + lao theo Z khi cuộn
            orbitCards.forEach((card, idx) => {
                const cfg = orbitConfigs[idx % orbitConfigs.length];
                const waveT = t * cfg.speed + cfg.phase;

                // Nội suy mượt trạng thái rê chuột
                const targetHover = hoveredOrbitIdx === idx ? 1 : 0;
                orbitHoverLerp[idx] = lerp(orbitHoverLerp[idx], targetHover, 0.12);
                const hBoost = orbitHoverLerp[idx];
                const calm = 1 - hBoost * 0.75;

                // Sóng lơ lửng không trọng lực (X, Y, Z + xoay 3 trục)
                const driftX = (Math.cos(waveT * 0.85) * 14 + Math.sin(waveT * 0.42) * 6) * calm;
                const driftY = (Math.sin(waveT * 1.15) * 18 + Math.cos(waveT * 0.55) * 6) * calm;
                const driftZ = Math.sin(waveT * 0.8) * 34 * calm;
                const driftRotX = Math.sin(waveT * 0.95) * 6.5 * calm;
                const driftRotY = Math.cos(waveT * 0.8) * 8.5 * calm;
                const driftRotZ = Math.sin(waveT * 0.7) * 3.8 * calm;

                const z = lerp(cfg.zStart, cfg.zEnd, p) + driftZ + hBoost * 95;
                const x = lerp(cfg.xStart, cfg.xEnd, p) + driftX + currentMouseX * (16 + idx * 4) * calm;
                const y = lerp(cfg.yStart, cfg.yEnd, p) + driftY + currentMouseY * (12 + idx * 3) * calm - hBoost * 8;
                const rX = driftRotX;
                const rY = (lerp(cfg.rotY, cfg.rotY * 0.25, p) + driftRotY + currentMouseX * 7) * (1 - hBoost);
                const rZ = (lerp(cfg.rotZ, cfg.rotZ * 1.5, p) + driftRotZ) * (1 - hBoost);
                const sc = lerp(0.92, 1.18, p) + hBoost * 0.1;

                const baseOp = p < 0.78 ? clamp(0.76 + p * 0.24, 0, 0.96) : clamp(1 - (p - 0.78) / 0.22, 0, 1);
                const op = lerp(baseOp, 1, hBoost);

                card.style.opacity = op.toFixed(3);
                card.style.transform = `translate3d(${x.toFixed(1)}px, ${y.toFixed(1)}px, ${z.toFixed(1)}px) rotateX(${rX.toFixed(2)}deg) rotateY(${rY.toFixed(2)}deg) rotateZ(${rZ.toFixed(2)}deg) scale3d(${sc.toFixed(3)}, ${sc.toFixed(3)}, 1)`;
            });

            // 3. Khối chữ bên trái tiến nhẹ về phía trước & nghiêng trục Y khi cuộn sâu
            if (spatialCopy) {
                const copyZ = p * 75;
                const copyY = -p * 34;
                const copyRotY = p * 6 + currentMouseX * 2.5;
                const copyOp = p < 0.82 ? 1 : clamp(1 - (p - 0.82) / 0.18, 0, 1);
                spatialCopy.style.opacity = copyOp.toFixed(3);
                spatialCopy.style.transform = `translate3d(0, ${copyY.toFixed(1)}px, ${copyZ.toFixed(1)}px) rotateY(${copyRotY.toFixed(2)}deg)`;
            }

            // 4. Vật thể Sách Bìa Cứng 3D lơ lửng & xoay đa trục
            if (bookEl) {
                const bookZ = 30 + p * 175;
                const bookY = floatWave - p * 26;
                const bookRotY = -14 + Math.sin(t * 1.2) * 3.5 + p * 42 + currentMouseX * 15;
                const bookRotX = 4 + Math.cos(t * 1.0) * 2.2 - p * 12 - currentMouseY * 10;
                const bookScale = 1 + p * 0.18;
                bookEl.style.transform = `translate3d(0, ${bookY.toFixed(1)}px, ${bookZ.toFixed(1)}px) rotateY(${bookRotY.toFixed(2)}deg) rotateX(${bookRotX.toFixed(2)}deg) scale3d(${bookScale.toFixed(3)}, ${bookScale.toFixed(3)}, 1)`;
            }

            // 5. Vòng quỹ đạo con quay hồi chuyển (Gyro Rings)
            if (gyroOuter) {
                gyroOuter.style.transform = `translate3d(0, 0, ${(-60 + p * 90).toFixed(1)}px) rotateX(${(64 - p * 28).toFixed(1)}deg) rotateZ(${(p * 140 + now * 0.015).toFixed(1)}deg)`;
            }
            if (gyroInner) {
                gyroInner.style.transform = `translate3d(0, 0, ${(-20 + p * 120).toFixed(1)}px) rotateY(${(48 + p * 65).toFixed(1)}deg) rotateZ(${(-p * 110 - now * 0.02).toFixed(1)}deg)`;
            }
        }

        // B. HIỆU ỨNG CHIỀU SÂU Z CHO CÁC KHỐI NỘI DUNG BÊN DƯỚI KHI CUỘN QUA
        if (!prefersReducedMotion && window.innerWidth > 900) {
            const viewportCenter = currentScrollY + winHeight * 0.55;
            for (let i = 0; i < blockMetrics.length; i++) {
                const item = blockMetrics[i];
                const blockCenter = item.top + item.height * 0.5;
                const distRatio = clamp((blockCenter - viewportCenter) / (winHeight * 0.85), -1, 1);

                const enterProgress = clamp(1 - Math.max(distRatio, 0), 0, 1);
                const zOffset = -95 * (1 - enterProgress) * item.depthFactor;
                const rotX = 9 * (1 - enterProgress);
                const scaleVal = 0.94 + 0.06 * enterProgress;
                const opacityVal = clamp(0.25 + enterProgress * 0.75, 0, 1);

                item.el.style.opacity = opacityVal.toFixed(3);
                item.el.style.transform = `translate3d(0, 0, ${zOffset.toFixed(1)}px) rotateX(${rotX.toFixed(2)}deg) scale3d(${scaleVal.toFixed(3)}, ${scaleVal.toFixed(3)}, 1)`;
            }
        }

        requestAnimationFrame(renderSpatialFrame);
    }
    requestAnimationFrame(renderSpatialFrame);

    // 3. Chuyển đổi 5 tác phẩm tiêu điểm
    const bgSlides = Array.from(document.querySelectorAll("#mvBgStack .cd-bg-slide"));
    const slideTabs = Array.from(document.querySelectorAll("#mvSlideTabs .cd-slide-tab"));
    const genreEl = document.getElementById("mvBannerGenre");
    const tagEl = document.getElementById("mvBannerTag");
    const titleEl = document.getElementById("mvBannerTitle");
    const blurbEl = document.getElementById("mvBannerBlurb");
    const authorEl = document.getElementById("mvBannerAuthor");
    const chaptersEl = document.getElementById("mvBannerChapters");
    const btnEl = document.getElementById("mvBannerBtn");

    const bookCoverImg = document.getElementById("mvBookCoverImg");
    const bookTypoCover = document.getElementById("mvBookTypoCover");
    const bookTypoGenre = document.getElementById("mvBookTypoGenre");
    const bookTypoTitle = document.getElementById("mvBookTypoTitle");
    const bookTypoAuthor = document.getElementById("mvBookTypoAuthor");

    let currentSlide = 0;
    let slideTimer = null;

    function goToSlide(idx) {
        if (bgSlides.length === 0) return;
        const total = bgSlides.length;
        currentSlide = (idx + total) % total;

        bgSlides.forEach((s, i) => s.classList.toggle("active", i === currentSlide));
        slideTabs.forEach((t, i) => t.classList.toggle("active", i === currentSlide));

        const data = bgSlides[currentSlide].dataset;
        if (genreEl) genreEl.textContent = data.genre || "";
        if (tagEl) tagEl.textContent = data.tag || "";
        if (titleEl) titleEl.textContent = data.title || "";
        if (blurbEl) blurbEl.textContent = data.blurb || "";
        if (authorEl) authorEl.textContent = data.author || "";
        if (chaptersEl) chaptersEl.textContent = data.chapters || "";
        if (btnEl && data.url) btnEl.setAttribute("href", data.url);
        if (bookEl && data.url) bookEl.setAttribute("href", data.url);

        if (data.cover && bookCoverImg && bookTypoCover) {
            bookCoverImg.src = data.cover;
            bookCoverImg.alt = data.title || "";
            bookCoverImg.classList.remove("is-hidden");
            bookTypoCover.classList.add("is-hidden");
        } else if (bookCoverImg && bookTypoCover) {
            bookCoverImg.classList.add("is-hidden");
            bookTypoCover.classList.remove("is-hidden");
            bookTypoCover.className = bookTypoCover.className.replace(/art-cover-\d/g, "").trim() + ` art-cover-${data.artTheme || 1}`;
            if (bookTypoGenre) bookTypoGenre.textContent = data.genre || "Tuyển chọn";
            if (bookTypoTitle) bookTypoTitle.textContent = data.title || "";
            if (bookTypoAuthor) bookTypoAuthor.textContent = data.author || "";
        }
    }

    function restartLoop() {
        if (slideTimer) clearInterval(slideTimer);
        if (bgSlides.length > 1) {
            slideTimer = setInterval(() => goToSlide(currentSlide + 1), 6000);
        }
    }

    if (bgSlides.length > 0) {
        slideTabs.forEach((tab, i) => {
            tab.addEventListener("click", () => {
                goToSlide(i);
                restartLoop();
            });
        });

        orbitCards.forEach((card) => {
            card.addEventListener("click", () => {
                const idx = parseInt(card.getAttribute("data-orbit-idx"), 10);
                if (!isNaN(idx)) {
                    goToSlide(idx);
                    restartLoop();
                }
            });
        });

        restartLoop();
    }

    // 4. Hiệu ứng nghiêng 3D tương tác chuột cho thẻ Thể loại & Thẻ truyện
    function attach3DTilt(el, maxTilt = 8) {
        if (!el || el.dataset.tiltBound === "1") return;
        el.dataset.tiltBound = "1";

        if (!el.querySelector(".tilt-glare")) {
            const glare = document.createElement("div");
            glare.className = "tilt-glare";
            el.appendChild(glare);
        }

        el.addEventListener("mousemove", (e) => {
            const rect = el.getBoundingClientRect();
            const x = e.clientX - rect.left;
            const y = e.clientY - rect.top;
            const rotateX = (((y - rect.height / 2) / (rect.height / 2)) * -maxTilt).toFixed(2);
            const rotateY = (((x - rect.width / 2) / (rect.width / 2)) * maxTilt).toFixed(2);

            el.style.transform = `perspective(1000px) rotateX(${rotateX}deg) rotateY(${rotateY}deg) translate3d(0, -4px, 18px)`;
            el.style.setProperty("--mx", `${(x / rect.width) * 100}%`);
            el.style.setProperty("--my", `${(y / rect.height) * 100}%`);
        });

        el.addEventListener("mouseleave", () => {
            el.style.transform = "perspective(1000px) rotateX(0deg) rotateY(0deg) translate3d(0, 0, 0)";
        });
    }

    document.querySelectorAll("[data-tilt-3d]").forEach((el) => {
        attach3DTilt(el, parseFloat(el.getAttribute("data-tilt-max")) || 6);
    });
    document.querySelectorAll(".marvel-3d-rail > *").forEach((card) => attach3DTilt(card, 8));

    // 5. Lăn chuột ngang mượt mà trên dải truyện nổi bật
    document.querySelectorAll(".marvel-3d-rail").forEach((rail) => {
        rail.addEventListener("wheel", (e) => {
            if (Math.abs(e.deltaY) > Math.abs(e.deltaX)) {
                e.preventDefault();
                rail.scrollBy({ left: e.deltaY * 1.4, behavior: "smooth" });
            }
        }, { passive: false });
    });

    // 6. Đếm số thống kê mượt
    document.querySelectorAll("[data-count-target]").forEach((counter) => {
        const target = parseInt(counter.getAttribute("data-count-target"), 10) || 0;
        if (target <= 0) return;
        const startTime = performance.now();
        const updateCount = (now) => {
            const p = Math.min((now - startTime) / 1000, 1);
            counter.textContent = Math.floor((1 - Math.pow(1 - p, 3)) * target);
            if (p < 1) requestAnimationFrame(updateCount);
            else counter.textContent = target;
        };
        requestAnimationFrame(updateCount);
    });
});