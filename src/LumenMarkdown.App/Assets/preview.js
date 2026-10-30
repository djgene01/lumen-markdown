(function () {
  var content = document.getElementById('content');
  var lastRatio = 0;

  function maxScroll() {
    return Math.max(0, document.documentElement.scrollHeight - window.innerHeight);
  }

  function setHtml(html) {
    var ratio = maxScroll() > 0 ? window.scrollY / maxScroll() : 0;
    content.innerHTML = html;
    requestAnimationFrame(function () {
      window.scrollTo(0, ratio * maxScroll());
    });
  }

  function post(message) {
    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage(message);
    }
  }

  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', function (e) {
      var m = e.data;
      if (!m || !m.type) return;
      if (m.type === 'html') { setHtml(m.html); }
      else if (m.type === 'theme') { document.documentElement.setAttribute('data-theme', m.theme); }
      else if (m.type === 'zoom') { document.body.style.zoom = String(m.zoom); }
      else if (m.type === 'scroll') {
        var target = Math.max(0, Math.min(1, m.ratio)) * maxScroll();
        window.scrollTo(0, target);
      }
    });
  }

  document.addEventListener('click', function (ev) {
    var a = ev.target && ev.target.closest ? ev.target.closest('a') : null;
    if (!a) return;
    var href = a.getAttribute('href') || '';
    ev.preventDefault();
    if (/^https?:/i.test(href)) {
      post({ type: 'open', url: href });
    } else if (/^#/.test(href)) {
      var el = document.getElementById(href.substring(1));
      if (el) el.scrollIntoView({ behavior: 'smooth' });
    }
  });

  var timer = null;
  window.addEventListener('scroll', function () {
    if (timer) return;
    timer = setTimeout(function () {
      timer = null;
      var m = maxScroll();
      lastRatio = m > 0 ? window.scrollY / m : 0;
      post({ type: 'scrolled', ratio: lastRatio });
    }, 80);
  });
})();
