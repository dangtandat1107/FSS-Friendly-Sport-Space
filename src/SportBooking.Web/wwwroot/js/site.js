// FSS: giao diện sáng/tối và thông báo nhanh (toast). JavaScript thuần, không dùng thư viện.
(function () {
  'use strict';

  /* ---------- Dark mode ---------- */
  var KEY = 'fss-theme';
  var root = document.documentElement;
  var btn = document.getElementById('theme-toggle');

  function isDark() { return root.getAttribute('data-bs-theme') === 'dark'; }

  function sync() {
    if (!btn) return;
    var dark = isDark();
    var label = dark ? 'Chuyển sang giao diện sáng' : 'Chuyển sang giao diện tối';
    btn.setAttribute('aria-pressed', dark ? 'true' : 'false');
    btn.setAttribute('aria-label', label);
    btn.title = label;
  }

  function apply(dark) {
    if (dark) root.setAttribute('data-bs-theme', 'dark');
    else root.removeAttribute('data-bs-theme');
    sync();
  }

  if (btn) {
    btn.addEventListener('click', function () {
      apply(!isDark());
      try { localStorage.setItem(KEY, isDark() ? 'dark' : 'light'); } catch (e) { /* trình duyệt chặn lưu trữ: vẫn đổi giao diện cho lần xem này */ }
    });
  }
  // Mở nhiều tab: đổi ở tab này thì tab kia đổi theo
  window.addEventListener('storage', function (e) { if (e.key === KEY) apply(e.newValue === 'dark'); });
  sync();

  /* ---------- Toast ---------- */
  var TOAST_MS = 4000;

  window.showToast = function (message, ms) {
    var host = document.getElementById('toast-root');
    if (!host || !message) return;

    var toast = document.createElement('div');
    toast.className = 'fss-toast';
    toast.setAttribute('role', 'status');
    toast.textContent = message;          // textContent: không chèn HTML, an toàn với nội dung bất kỳ
    toast.title = 'Bấm để đóng';
    host.appendChild(toast);
    requestAnimationFrame(function () { toast.classList.add('show'); });

    var closed = false;
    function close() {
      if (closed) return;
      closed = true;
      toast.classList.remove('show');
      setTimeout(function () { toast.remove(); }, 300);   // chờ hiệu ứng mờ dần
    }
    toast.addEventListener('click', close);
    setTimeout(close, ms || TOAST_MS);
  };

  // Server đặt thông báo qua TempData["Toast"]
  var data = document.getElementById('toast-data');
  if (data) window.showToast(data.getAttribute('data-message'));
})();
