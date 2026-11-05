(() => {
    const status = document.querySelector('#coinReward');
    const form = document.querySelector('#readingProgress');
    const chapter = document.querySelector('#readerContent');
    if (!status || !form || !chapter) return;

    let session = 0;
    let timer;
    let done = false;
    async function post(url, extra = {}) {
        const body = new URLSearchParams(new FormData(form));
        for (const [key, value] of Object.entries(extra)) body.set(key, value);
        const response = await fetch(url, { method: 'POST', body, credentials: 'same-origin' });
        if (!response.ok) throw new Error();
        return response.json();
    }
    function stop() {
        session++;
        clearTimeout(timer);
    }
    async function begin() {
        stop();
        if (document.hidden || done) return;
        const current = session;
        try {
            const { ticket } = await post(status.dataset.start);
            if (current !== session) return;
            async function finish() {
                if (current !== session || document.hidden) return;
                if (chapter.getBoundingClientRect().bottom > window.innerHeight + 2) {
                    timer = setTimeout(finish, 500);
                    return;
                }
                try {
                    const { granted } = await post(status.dataset.finish, { ticket, percent: 100 });
                    done = true;
                    if (granted) status.textContent = '+10 xu';
                } catch { status.textContent = 'Chưa ghi nhận được xu. Hãy tải lại để thử lại.'; }
            }
            timer = setTimeout(finish, 60000);
        } catch { if (current === session) status.textContent = 'Chưa kết nối được dịch vụ xu.'; }
    }
    document.addEventListener('visibilitychange', begin);
    window.addEventListener('pagehide', stop);
    window.addEventListener('pageshow', begin);
})();
