const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../../src/ReadManager.Web/wwwroot/js/reading-progress.js'), 'utf8');
const tick = () => new Promise(resolve => setImmediate(resolve));
function setup({ loaded = 'true', percent = '63.5', hash = '', response = 'ok' } = {}) {
    const events = {}, requests = [], timers = new Map();
    const status = { textContent: '' };
    const form = { action: '/Library/SaveProgress', dataset: { loaded, percent } };
    const win = { scrollY: 0, innerHeight: 1000,
        addEventListener: (name, fn) => events[name] = fn,
        scrollTo: ({top}) => win.scrollY = top };
    const doc = { readyState: 'complete', hidden: false,
        getElementById: id => ({ readingProgress: form, readerContent: { getBoundingClientRect: () => ({top: 1000-win.scrollY, height:3000}) }, progressStatus: status })[id],
        addEventListener: (name, fn) => events[name] = fn };
    let timerId=0;
    vm.runInNewContext(source, { window:win, document:doc, location:{hash}, URLSearchParams,
        FormData: class { *[Symbol.iterator]() { yield ['__RequestVerificationToken','csrf']; yield ['storyId','1']; yield ['chapterId','2']; } },
        setTimeout: fn => {timers.set(++timerId,fn);return timerId;}, clearTimeout: id => timers.delete(id),
        fetch: async (url, options) => {requests.push({url, options});return {ok: true, headers:{get:()=>response==='html'?'text/html':'application/json'},json:async()=>({saved:true})};} });
    return {win, doc, events, requests, status, timers};
}
(async () => {
    let c=setup(); await tick();
    assert.equal(c.win.scrollY,2270); assert.equal(c.requests[0].options.body.get('scrollPercent'),'63.50');
    assert.equal(c.requests[0].options.body.get('__RequestVerificationToken'),'csrf');
    c.win.scrollY=1700;c.events.scroll();c.win.scrollY=1800;c.events.scroll();
    assert.equal(c.timers.size,1); [...c.timers.values()][0](); await tick();
    assert.equal(c.requests.at(-1).options.body.get('scrollPercent'),'40.00');
    c=setup({loaded:'false'}); await tick(); c.events.pagehide(); await tick();
    assert.equal(c.requests.length,0); // failed read must not overwrite server progress on exit
    c=setup({hash:'#comments'});await tick();assert.equal(c.win.scrollY,0);
    c.events.pagehide();await tick();assert.equal(c.requests.length,1); // preserve initial position
    c=setup({response:'html'});await tick();assert.match(c.status.textContent,/đăng nhập/);
    console.log('PASS: restore, antiforgery, debounce, scroll mapping, failed-load preservation, anchor preservation, expired-login error');
})().catch(error=>{console.error(error);process.exitCode=1;});
