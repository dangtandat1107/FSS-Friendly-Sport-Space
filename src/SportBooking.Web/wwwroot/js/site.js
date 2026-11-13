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

  var toastRoot = document.getElementById('toast-root');

  // href (không bắt buộc): bấm vào toast thì mở trang đó thay vì chỉ đóng
  window.showToast = function (message, ms, href) {
    var host = document.getElementById('toast-root');
    if (!host || !message) return;

    var toast = document.createElement('div');
    toast.className = 'fss-toast';
    toast.setAttribute('role', 'status');
    toast.textContent = message;          // textContent: không chèn HTML, an toàn với nội dung bất kỳ
    toast.title = href ? 'Bấm để xem' : 'Bấm để đóng';
    host.appendChild(toast);
    requestAnimationFrame(function () { toast.classList.add('show'); });

    var closed = false;
    function close() {
      if (closed) return;
      closed = true;
      toast.classList.remove('show');
      setTimeout(function () { toast.remove(); }, 300);   // chờ hiệu ứng mờ dần
    }
    toast.addEventListener('click', function () { if (href) window.location.href = href; else close(); });
    setTimeout(close, ms || TOAST_MS);
  };

  // Nhân viên, chủ sân, Admin: cứ 15 giây hỏi server có sự cố mới không và hiện toast (không cần thư viện thời gian thực)
  var pollUrl = toastRoot && toastRoot.getAttribute('data-incident-poll');
  if (pollUrl) {
    var SEEN_KEY = 'fss-incident-seen';
    var seen = [];
    try { seen = JSON.parse(sessionStorage.getItem(SEEN_KEY) || '[]'); } catch (e) { seen = []; }
    var poll = function () {
      fetch(pollUrl, { credentials: 'same-origin', headers: { 'Accept': 'application/json' } })
        .then(function (r) { return r.ok ? r.json() : []; })
        .then(function (items) {
          items.forEach(function (it) {
            if (seen.indexOf(it.id) !== -1) return;     // mỗi sự cố chỉ báo một lần trong một phiên làm việc
            seen.push(it.id);
            window.showToast(it.text, 15000, it.url);   // sự cố khẩn thì giữ lâu hơn 4 giây
          });
          seen = seen.slice(-50);
          try { sessionStorage.setItem(SEEN_KEY, JSON.stringify(seen)); } catch (e) { }
        })
        .catch(function () { /* mất mạng tạm thời: lần sau thử lại */ });
    };
    poll();
    setInterval(poll, 15000);
  }

  /* ---------- Đồng hồ đếm ngược SLA ---------- */
  // Server gửi số giây còn lại (không gửi giờ tuyệt đối) nên không bị lệch múi giờ giữa server và trình duyệt
  var slaEls = document.querySelectorAll('[data-sla]');
  if (slaEls.length) {
    var t0 = Date.now();
    var fmt = function (sec) {
      if (sec <= 0) return 'Quá hạn';
      var h = Math.floor(sec / 3600), m = Math.floor((sec % 3600) / 60), s = sec % 60;
      return (h > 0 ? h + ':' : '') + String(m).padStart(2, '0') + ':' + String(s).padStart(2, '0');
    };
    var tick = function () {
      var elapsed = Math.floor((Date.now() - t0) / 1000);
      slaEls.forEach(function (el) {
        var left = parseInt(el.getAttribute('data-sla'), 10) - elapsed;
        el.textContent = fmt(left);
        el.classList.toggle('text-danger', left <= 300);   // còn dưới 5 phút thì chuyển đỏ
      });
    };
    tick();
    setInterval(tick, 1000);
  }

  // Server đặt thông báo qua TempData["Toast"]
  var data = document.getElementById('toast-data');
  if (data) window.showToast(data.getAttribute('data-message'));
})();
