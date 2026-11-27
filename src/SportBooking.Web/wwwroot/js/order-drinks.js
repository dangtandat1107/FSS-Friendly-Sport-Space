// Hộp thoại đặt sân và đồ uống kèm. JavaScript thuần, không dùng thư viện.
// Luồng: khách bấm ô xanh trên lịch lưới -> mở hộp thoại (phần trên: thông tin sân, phần dưới: chọn đồ uống)
// -> số lượng đổi thì tổng tiền cập nhật ngay, đồng thời hỏi server (debounce) để lấy quà tặng, khuyến mãi và tổng chính xác
// -> bấm "Bỏ qua" hoặc "Thêm vào đơn" thì gửi form tới Bookings/Create hoặc Bookings/CreateBookingWithItems.
(function () {
  'use strict';

  var dlg = document.getElementById('book-dialog');
  if (!dlg) return;   // chỉ khách hàng đã đăng nhập mới có hộp thoại này

  var $ = function (id) { return document.getElementById(id); };
  var el = {
    form: $('bk-form'), court: $('bk-court'), hour: $('bk-hour'), hours: $('bk-hours'),
    name: $('bk-name'), sport: $('bk-sport'), start: $('bk-start'), courtPrice: $('bk-court-price'),
    drinks: $('bk-drinks'), banners: $('bk-banners'), products: $('bk-products'), msg: $('bk-drinks-msg'),
    items: $('bk-items'), skip: $('bk-skip'), add: $('bk-add'), close: $('bk-close'),
    sumCourt: $('bk-sum-court'), sumDrinks: $('bk-sum-drinks'), sumTotal: $('bk-sum-total'), gifts: $('bk-gifts')
  };
  var productsUrl = dlg.getAttribute('data-products-url');
  var quoteUrl = dlg.getAttribute('data-quote-url');
  var SKIP_TEXT = 'Bỏ qua, chỉ đặt sân';

  var state = null;       // trạng thái của lần mở hiện tại
  var cache = {};         // mã sân -> dữ liệu sản phẩm đã tải (mở lại cùng sân không phải tải lại)

  function pad(n) { return (n < 10 ? '0' : '') + n; }
  function money(n) { return Number(n).toLocaleString('vi-VN') + ' đ'; }

  // Tạo phần tử bằng DOM API (textContent) nên dữ liệu từ server không thể chèn HTML vào trang
  function h(tag, cls, text) {
    var e = document.createElement(tag);
    if (cls) e.className = cls;
    if (text != null) e.textContent = text;
    return e;
  }

  /* ---------- Mở hộp thoại khi bấm ô xanh ---------- */
  document.querySelectorAll('.cal-grid button.cal-free').forEach(function (btn) {
    btn.addEventListener('click', function () { open(btn); });
  });

  function open(btn) {
    state = {
      courtId: +btn.dataset.court, pricePerHour: +btn.dataset.price, startHour: +btn.dataset.hour,
      qty: {}, catalog: null, quote: null, seq: 0, timer: null, toasted: {}
    };

    // Cho chọn thêm giờ nếu các ô liền sau cũng còn trống (tối đa 4 giờ)
    var max = 1, td = btn.closest('td').nextElementSibling;
    while (max < 4 && td && td.querySelector('button.cal-free')) { max++; td = td.nextElementSibling; }
    el.hours.innerHTML = '';
    for (var n = 1; n <= max; n++) {
      var o = document.createElement('option');
      o.value = n;
      o.textContent = n + ' giờ (' + pad(state.startHour) + ':00 - ' + pad(state.startHour + n) + ':00)';
      el.hours.appendChild(o);
    }

    // Phần trên: thông tin sân
    el.court.value = state.courtId;
    el.hour.value = state.startHour;
    el.name.textContent = btn.dataset.name;
    el.sport.textContent = btn.dataset.sport || '';
    el.start.textContent = pad(state.startHour) + ':00';

    // Phần dưới: xóa dữ liệu lần trước, tải sản phẩm
    el.items.innerHTML = '';
    el.banners.innerHTML = '';
    el.msg.textContent = '';
    el.products.innerHTML = '';
    el.products.appendChild(h('div', 'text-muted small', 'Đang tải danh sách đồ uống...'));
    el.drinks.hidden = false;
    el.add.hidden = false;
    el.skip.textContent = SKIP_TEXT;
    refreshSummary();
    dlg.showModal();
    loadProducts();
  }

  /* ---------- Tải danh sách sản phẩm của sân (AJAX) ---------- */
  function getJson(url) {
    return fetch(url, { credentials: 'same-origin', headers: { 'Accept': 'application/json' } })
      .then(function (r) { if (!r.ok) throw new Error('HTTP ' + r.status); return r.json(); });
  }

  function loadProducts() {
    var id = state.courtId;
    var ready = cache[id] ? Promise.resolve(cache[id]) : getJson(productsUrl + '?courtId=' + id).then(function (d) { cache[id] = d; return d; });
    ready.then(function (data) {
      if (!state || state.courtId !== id) return;   // khách đã đóng hoặc mở sân khác trong lúc chờ
      state.catalog = data;
      renderProducts(data);
      scheduleQuote(0);   // giỏ trống vẫn hỏi server để biết khách có quà theo chuỗi tập luyện không
    }).catch(function () {
      el.products.innerHTML = '';
      el.msg.textContent = 'Không tải được danh sách đồ uống. Bạn vẫn có thể bấm "' + SKIP_TEXT + '".';
      el.add.hidden = true;
    });
  }

  function renderProducts(data) {
    el.products.innerHTML = '';
    // Sân chưa có sản phẩm: ẩn phần đồ uống, nút đổi thành "Xác nhận đặt sân"
    if (!data.products.length) {
      el.drinks.hidden = true;
      el.add.hidden = true;
      el.skip.textContent = 'Xác nhận đặt sân';
      return;
    }
    data.products.forEach(function (p) { el.products.appendChild(card(p)); });
    updateAddButton();
  }

  // Một thẻ sản phẩm: hình, tên, giá, tồn kho và nút - / +
  function card(p) {
    var soldOut = p.available <= 0;
    var c = h('div', 'drink-card' + (soldOut ? ' soldout' : ''));
    c.dataset.id = p.id;

    var img = h('div', 'drink-img');
    if (p.imageUrl) {
      var i = document.createElement('img');
      i.src = p.imageUrl; i.alt = p.name; i.loading = 'lazy';
      img.appendChild(i);
    } else {
      img.appendChild(h('span', 'drink-icon', p.icon));
    }
    c.appendChild(img);

    c.appendChild(h('div', 'drink-name', p.name));
    c.appendChild(h('div', 'drink-price', money(p.price)));
    c.appendChild(h('div', 'drink-stock' + (soldOut ? ' out' : p.available <= 5 ? ' low' : ''),
      soldOut ? 'Hết hàng' : p.available <= 5 ? 'Chỉ còn ' + p.available : 'Còn ' + p.available));

    var q = h('div', 'qty');
    var minus = h('button', 'qty-btn', '−'); minus.type = 'button'; minus.setAttribute('aria-label', 'Giảm số lượng ' + p.name);
    var val = h('span', 'qty-val', '0'); val.setAttribute('aria-live', 'polite');
    var plus = h('button', 'qty-btn', '+'); plus.type = 'button'; plus.setAttribute('aria-label', 'Tăng số lượng ' + p.name);
    minus.disabled = true;
    plus.disabled = soldOut;   // hết hàng thì khóa nút
    minus.addEventListener('click', function () { change(p, -1, minus, plus, val); });
    plus.addEventListener('click', function () { change(p, +1, minus, plus, val); });
    q.appendChild(minus); q.appendChild(val); q.appendChild(plus);
    c.appendChild(q);
    return c;
  }

  function change(p, delta, minus, plus, val) {
    var cur = state.qty[p.id] || 0;
    var next = Math.max(0, Math.min(p.maxQty, cur + delta));   // không vượt tồn kho hay giới hạn mỗi đơn
    if (next === cur) return;
    state.qty[p.id] = next;
    val.textContent = next;
    minus.disabled = next === 0;
    plus.disabled = next >= p.maxQty;
    state.quote = null;   // số liệu cũ không còn đúng, hiện tạm tính cho tới khi server trả lời
    refreshSummary();
    updateAddButton();
    scheduleQuote(250);
  }

  /* ---------- Tính tiền ---------- */
  function localDrinks() {
    var sum = 0;
    if (state.catalog) state.catalog.products.forEach(function (p) { sum += (state.qty[p.id] || 0) * p.price; });
    return sum;
  }
  function totalQty() { var n = 0; for (var k in state.qty) n += state.qty[k]; return n; }
  function itemsParam() {
    var parts = [];
    for (var k in state.qty) if (state.qty[k] > 0) parts.push(k + ':' + state.qty[k]);
    return parts.join(',');
  }

  // Tiền nước và quà tặng: ưu tiên số server vừa tính, chưa có thì dùng tạm tính ngay trên trình duyệt
  function refreshSummary() {
    var court = state.pricePerHour * (+el.hours.value || 1);
    var drinks = state.quote ? state.quote.drinksTotal : localDrinks();
    el.courtPrice.textContent = money(court);
    el.sumCourt.textContent = money(court);
    el.sumDrinks.textContent = money(drinks);
    el.sumTotal.textContent = money(court + drinks);

    el.gifts.innerHTML = '';
    if (state.quote) state.quote.lines.filter(function (l) { return l.isFree; }).forEach(function (l) {
      el.gifts.appendChild(h('div', 'gift-line', '🎁 Tặng ' + l.quantity + ' x ' + l.name + ' (0 đ)'));
    });
  }

  function hasGift() { return !!(state.quote && state.quote.lines.some(function (l) { return l.isFree; })); }

  // Nút "Thêm vào đơn" bật khi có món được chọn hoặc có quà tặng; tắt khi server báo lỗi (hết hàng, vượt giới hạn)
  function updateAddButton() {
    var errs = state.quote && !state.quote.ok;
    el.add.disabled = !!errs || (totalQty() === 0 && !hasGift());
    el.add.title = el.add.disabled && !errs ? 'Chọn ít nhất 1 món để thêm vào đơn' : '';
  }

  /* ---------- Hỏi server: khuyến mãi, quà tặng, tổng tiền chính xác ---------- */
  function scheduleQuote(delay) {
    clearTimeout(state.timer);
    var mySeq = ++state.seq;
    state.timer = setTimeout(function () {
      getJson(quoteUrl + '?courtId=' + state.courtId + '&items=' + encodeURIComponent(itemsParam()))
        .then(function (q) {
          if (!state || mySeq !== state.seq) return;   // đã có thay đổi mới hơn, bỏ kết quả cũ
          state.quote = q;
          showBanners(q);
          el.msg.textContent = q.ok ? '' : q.errors.join(' ');
          refreshSummary();
          updateAddButton();
        })
        .catch(function () { /* mất mạng tạm thời: giữ tạm tính, server vẫn kiểm tra lại lúc đặt thật */ });
    }, delay);
  }

  function showBanners(q) {
    el.banners.innerHTML = '';
    q.banners.forEach(function (b) {
      el.banners.appendChild(h('div', 'promo-banner promo-' + b.kind, b.text));
      // Toast khi khuyến mãi vừa được áp dụng (mỗi khuyến mãi báo một lần trong một lần mở hộp thoại)
      var key = b.kind + '|' + b.text;
      if (!state.toasted[key]) {
        state.toasted[key] = true;
        if (window.showToast) window.showToast(b.text, 5000);
      }
    });
  }

  /* ---------- Hai nút hành động ---------- */
  // "Bỏ qua": không gửi mặt hàng nào, form gửi tới Bookings/Create như cũ (chỉ đặt sân)
  el.skip.addEventListener('click', function () { el.items.innerHTML = ''; });

  // "Thêm vào đơn & thanh toán": đưa các món đã chọn vào form (Items[0].ProductId, Items[0].Quantity ...), gửi tới CreateBookingWithItems.
  // Giá và quà tặng không gửi lên: server tự tính lại.
  el.add.addEventListener('click', function () {
    el.items.innerHTML = '';
    var i = 0;
    Object.keys(state.qty).forEach(function (id) {
      if (state.qty[id] > 0) {
        [['ProductId', id], ['Quantity', state.qty[id]]].forEach(function (f) {
          var inp = document.createElement('input');
          inp.type = 'hidden'; inp.name = 'Items[' + i + '].' + f[0]; inp.value = f[1];
          el.items.appendChild(inp);
        });
        i++;
      }
    });
  });

  // Chống bấm hai lần: khóa nút ngay sau khi trình duyệt đã lấy xong địa chỉ gửi
  el.form.addEventListener('submit', function () {
    setTimeout(function () { el.skip.disabled = true; el.add.disabled = true; }, 0);
  });

  /* ---------- Đóng hộp thoại ---------- */
  el.hours.addEventListener('change', function () { refreshSummary(); });
  el.close.addEventListener('click', function () { dlg.close(); });
  dlg.addEventListener('click', function (e) { if (e.target === dlg) dlg.close(); });   // bấm ra ngoài thì đóng
  dlg.addEventListener('close', function () {
    if (state) clearTimeout(state.timer);
    state = null;
    el.skip.disabled = false; el.add.disabled = false;   // mở lại sau khi đặt lỗi thì nút dùng được
  });
})();
