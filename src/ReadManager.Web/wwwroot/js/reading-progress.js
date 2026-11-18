// PB28: browser -> Web (cookie + antiforgery) -> API (bearer token).
(() => {
    const form = document.getElementById('readingProgress');
    const content = document.getElementById('readerContent');
    const status = document.getElementById('progressStatus');
    if (!form || !content) return;
    let ready = false, interacted = false, timer, sending = false, pending = null;
    const initial = Math.max(0, Math.min(100, Number(form.dataset.percent) || 0));
    const geometry = () => {
        const box = content.getBoundingClientRect();
        return { top: box.top + window.scrollY, range: Math.max(0, box.height - window.innerHeight) };
    };
    const position = () => {
        const { top, range } = geometry();
        return range ? Math.max(0, Math.min(100, (window.scrollY - top) / range * 100)) : 0;
    };
    async function drain() {
        if (sending || pending === null) return;
        sending = true;
        const value = pending;
        pending = null;
        try {
            const body = new URLSearchParams(new FormData(form));
            body.set('scrollPercent', value.toFixed(2));
            const response = await fetch(form.action, { method: 'POST', body, credentials: 'same-origin', keepalive: true });
            // Cookie hết hạn có thể chuyển hướng tới HTML login với HTTP 200.
            const data = response.headers.get('content-type')?.includes('application/json') ? await response.json() : null;
            if (!response.ok || !data?.saved) throw new Error(data?.message || 'Chưa lưu được tiến độ. Vui lòng kiểm tra kết nối hoặc đăng nhập lại.');
            status.textContent = 'Đã đồng bộ tiến độ đọc.';
        } catch (error) {
            status.textContent = error.message || 'Chưa lưu được tiến độ. Vui lòng thử lại.';
        } finally {
            sending = false;
            if (pending !== null) void drain();
        }
    }
    const save = value => { pending = value; void drain(); };
    function start() {
        if (ready) return;
        // Không đẩy người vừa gửi bình luận ra khỏi #comments.
        if (form.dataset.loaded === 'true' && initial > 0 && !location.hash) {
            const { top, range } = geometry();
            window.scrollTo({ top: top + range * initial / 100, behavior: 'instant' });
        }
        ready = true;
        // Không ghi đè tiến độ bằng 0 khi việc tải tiến độ từ API bị lỗi.
        if (form.dataset.loaded === 'true') save(initial);
    }
    window.addEventListener('scroll', () => {
        if (!ready) return;
        interacted = true;
        clearTimeout(timer);
        timer = setTimeout(() => save(position()), 700);
    }, { passive: true });
    document.addEventListener('visibilitychange', () => {
        if (document.hidden && ready && interacted) { clearTimeout(timer); save(position()); }
    });
    window.addEventListener('pagehide', () => {
        if (ready && interacted) { clearTimeout(timer); save(position()); }
    });
    if (document.readyState === 'complete') start();
    else window.addEventListener('load', start, { once: true });
})();
