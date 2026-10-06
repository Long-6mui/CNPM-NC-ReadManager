// Ảnh đại diện và truyện yêu thích lưu trên trình duyệt (tạm thời, cho đến khi có API).
(function () {
  var m = document.querySelector('meta[name=uid]'), uid = m ? m.content : '';
  function key(p) { return p + ':' + uid; }
  function read(k, d) { try { return JSON.parse(localStorage.getItem(k)) || d; } catch (e) { return d; } }
  function favs() { return read(key('fav'), []); }

  function applyAvatar() {
    var a = uid && localStorage.getItem(key('avatar'));
    if (!a) return;
    document.querySelectorAll('.avatar,.pf-avatar').forEach(function (el) {
      el.style.cssText += ';background-image:url(' + a + ');background-size:cover;background-position:center;color:transparent';
    });
  }
  applyAvatar();

  var file = document.getElementById('avatarFile');
  if (file) file.addEventListener('change', function () {
    var f = file.files[0]; if (!f) return;
    var img = new Image(), url = URL.createObjectURL(f);
    img.onload = function () {
      var s = Math.min(img.width, img.height), c = document.createElement('canvas');
      c.width = c.height = 160;
      c.getContext('2d').drawImage(img, (img.width - s) / 2, (img.height - s) / 2, s, s, 0, 0, 160, 160);
      try { localStorage.setItem(key('avatar'), c.toDataURL('image/jpeg', 0.85)); applyAvatar(); }
      catch (e) { alert('Không lưu được ảnh. Hãy thử ảnh nhỏ hơn.'); }
      URL.revokeObjectURL(url);
    };
    img.src = url;
  });

  document.querySelectorAll('.fav-btn').forEach(function (b) {
    function paint() {
      var on = favs().some(function (f) { return String(f.id) === b.dataset.id; });
      b.classList.toggle('on', on);
      b.querySelector('i').textContent = on ? '♥' : '♡';
      b.querySelector('span').textContent = on ? 'Đã yêu thích' : 'Yêu thích';
    }
    paint();
    b.addEventListener('click', function () {
      if (!uid) { location.href = '/Account/Login?returnUrl=' + encodeURIComponent(location.pathname); return; }
      var list = favs(), i = list.findIndex(function (f) { return String(f.id) === b.dataset.id; });
      if (i >= 0) list.splice(i, 1);
      else list.push({ id: b.dataset.id, title: b.dataset.title, author: b.dataset.author, cover: b.dataset.cover });
      localStorage.setItem(key('fav'), JSON.stringify(list));
      paint();
    });
  });

  var box = document.getElementById('favList');
  function el(tag, cls, text) { var e = document.createElement(tag); if (cls) e.className = cls; if (text) e.textContent = text; return e; }
  function renderFavs() {
    if (!box) return;
    var list = favs(); if (!list.length) return;
    box.textContent = '';
    list.forEach(function (f) {
      var wrap = el('div'), a = el('a', 'fav-card'), cv = el('div', 'story-cover');
      a.href = '/Stories/Details/' + encodeURIComponent(f.id);
      if (f.cover) { var im = el('img'); im.src = f.cover; im.alt = f.title; cv.appendChild(im); }
      else { var g = el('div', 'gen-cover'); g.appendChild(el('span', '', f.title)); cv.appendChild(g); }
      a.appendChild(cv); a.appendChild(el('b', '', f.title)); a.appendChild(el('span', '', f.author));
      var rm = el('button', 'fav-rm', 'Bỏ thích'); rm.type = 'button';
      rm.addEventListener('click', function () {
        localStorage.setItem(key('fav'), JSON.stringify(favs().filter(function (x) { return String(x.id) !== String(f.id); })));
        box.innerHTML = '<p class="pf-empty">Bạn chưa lưu truyện nào.</p>'; renderFavs();
      });
      wrap.appendChild(a); wrap.appendChild(rm); box.appendChild(wrap);
    });
  }
  renderFavs();
})();
