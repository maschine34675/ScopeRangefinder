/* Scope Rangefinder - Style Studio page.
 *
 * A view onto the mod's style model. The mod sends one `state` document
 * (retained, so a reload after a renderer crash gets it again) and streams
 * `thumb` images; the page sends fine-grained mutations on named channels.
 * Everything here is vanilla; the `overlay` object is injected by
 * Anvil-WebOverlay before this script runs. No localStorage is used: the mod
 * is the source of truth, and an inline page has no storage anyway.
 */
(function () {
  'use strict';

  var ov = window.overlay;
  if (!ov) {
    document.body.innerHTML = '<p style="padding:20px;color:#f66">This page must be opened by the Scope Rangefinder mod.</p>';
    return;
  }
  var GROUPS = {
    readout: [
      ['Readout.DistanceUnit', 'Distance unit'],
      ['Readout.ShowUnitSuffix', 'Unit suffix'],
      ['Readout.UseDecimalFormat', 'Decimal format (045.0)'],
      ['Readout.ShowZeroLine', 'Zeroing line'],
      ['Readout.BallisticsLine', 'Ballistics line'],
      ['Readout.BallisticsHoldUnit', 'Hold unit'],
      ['Readout.RangeLinePrefix', 'Range prefix'],
      ['Readout.ZeroLinePrefix', 'Zeroing prefix'],
      ['Readout.HoldLinePrefix', 'Hold prefix'],
      ['Readout.DialLinePrefix', 'Dial prefix'],
      ['Readout.NoDistanceText', 'No-target text']
    ],
    text: [
      ['Scope Text.ScopeWorldTextColor', 'Text color'],
      ['Scope Text.ScopeFontSource', 'Font source'],
      ['Scope Text.ScopeFontName', 'System font'],
      ['Scope Text.CustomFontFile', 'Font file', 'fontlist'],
      ['Scope Text.ScopeTextThickness', 'Thickness'],
      ['Scope Text.ScopeTextSpacing', 'Letter spacing'],
      ['Scope Text.ScopeTextGlow', 'Glow'],
      ['Scope Text.ScopeTextOutline', 'Black outline'],
      ['Scope Text.ScopeTextAberration', 'Chromatic aberration'],
      ['Scope Text.ScopeWorldTextOffsetY', 'Vertical offset']
    ],
    background: [
      ['Scope Background.ScopeWorldBackground', 'Background plate'],
      ['Scope Background.ScopeWorldBackgroundWidth', 'Plate width', 'range', 0, 0.8],
      ['Scope Background.ScopeWorldBackgroundHeight', 'Plate height', 'range', 0, 0.4],
      ['Scope Background.ScopeWorldBackgroundColor', 'Plate color']
    ]
  };

  var $ = function (id) { return document.getElementById(id); };
  var state = null;
  var thumbs = {};
  var fieldNodes = {};
  var dragging = {};
  var statusTimer = 0;
  function status(level, text) {
    var el = $('status');
    el.textContent = text || '';
    el.className = 'status ' + (level === 'ok' ? 'ok' : level === 'error' ? 'err' : '');
    clearTimeout(statusTimer);
    if (text) statusTimer = setTimeout(function () { el.textContent = ''; el.className = 'status'; }, 4000);
  }
  function hexToRgba(hex) {
    if (!hex || hex.length < 6) return { r: 255, g: 255, b: 255, a: 255 };
    return {
      r: parseInt(hex.substr(0, 2), 16), g: parseInt(hex.substr(2, 2), 16),
      b: parseInt(hex.substr(4, 2), 16), a: hex.length >= 8 ? parseInt(hex.substr(6, 2), 16) : 255
    };
  }
  function rgbaToHex(c) {
    var h = function (n) { return ('0' + Math.max(0, Math.min(255, Math.round(n))).toString(16)).slice(-2).toUpperCase(); };
    return h(c.r) + h(c.g) + h(c.b) + h(c.a);
  }
  function fmt(n, digits) { return Number(n).toFixed(digits); }
  var setTimers = {};
  function sendSet(key, value, immediate) {
    clearTimeout(setTimers[key]);
    var go = function () { ov.send('set', JSON.stringify({ key: key, value: String(value) })); };
    if (immediate) go(); else setTimers[key] = setTimeout(go, 80);
    refreshPreview();
  }
  function makeField(label, control, valueEl) {
    var row = document.createElement('div');
    row.className = 'field';
    var l = document.createElement('label'); l.textContent = label; row.appendChild(l);
    row.appendChild(control);
    if (valueEl) row.appendChild(valueEl); else row.appendChild(document.createElement('span'));
    return row;
  }

  function buildBool(key, label) {
    var cb = document.createElement('input'); cb.type = 'checkbox'; cb.className = 'check';
    cb.addEventListener('change', function () { sendSet(key, cb.checked ? 'true' : 'false', true); });
    fieldNodes[key] = { set: function (v) { cb.checked = String(v).toLowerCase() === 'true'; } };
    return makeField(label, cb);
  }

  function buildEnum(key, label, meta) {
    var sel = document.createElement('select');
    (meta.options || []).forEach(function (o) { var op = document.createElement('option'); op.value = o; op.textContent = o; sel.appendChild(op); });
    sel.addEventListener('change', function () {
      sendSet(key, sel.value, true);
      if (key === 'Scope Text.ScopeFontSource') updateFontFieldVisibility(sel.value);
    });
    fieldNodes[key] = { set: function (v) { sel.value = v; if (key === 'Scope Text.ScopeFontSource') updateFontFieldVisibility(v); } };
    return makeField(label, sel);
  }
  function updateFontFieldVisibility(source) {
    var row = fieldNodes['Scope Text.ScopeFontName'] && fieldNodes['Scope Text.ScopeFontName'].row;
    if (row) row.classList.toggle('hidden', source !== 'SystemFont');
  }

  function buildText(key, label) {
    var inp = document.createElement('input'); inp.type = 'text'; inp.spellcheck = false;
    var t = 0;
    inp.addEventListener('input', function () { clearTimeout(t); t = setTimeout(function () { sendSet(key, inp.value, true); }, 500); });
    inp.addEventListener('change', function () { clearTimeout(t); sendSet(key, inp.value, true); });
    var row = makeField(label, inp);
    fieldNodes[key] = { row: row, set: function (v) { if (document.activeElement !== inp) inp.value = v; } };
    return row;
  }

  function buildRange(key, label, meta, fallbackMin, fallbackMax) {
    var min = meta.min != null ? meta.min : fallbackMin, max = meta.max != null ? meta.max : fallbackMax;
    var r = document.createElement('input'); r.type = 'range'; r.min = min; r.max = max; r.step = 'any';
    var val = document.createElement('input'); val.type = 'text'; val.className = 'val'; val.spellcheck = false;
    var digits = (max - min) <= 1 ? 3 : 2;
    function show(v) { val.value = fmt(v, digits); }
    r.addEventListener('input', function () { dragging[key] = true; if (document.activeElement !== val) show(r.value); sendSet(key, r.value); });
    r.addEventListener('change', function () { dragging[key] = false; sendSet(key, r.value, true); });
    val.addEventListener('change', function () {
      var n = parseFloat(val.value.replace(',', '.'));
      if (isNaN(n)) { show(r.value); return; }
      n = Math.min(max, Math.max(min, n));
      r.value = n; show(n);
      sendSet(key, n, true);
    });
    val.addEventListener('keydown', function (e) { if (e.key === 'Enter') { val.blur(); } });
    fieldNodes[key] = { set: function (v) { if (dragging[key]) return; r.value = v; if (document.activeElement !== val) show(v); } };
    return makeField(label, r, val);
  }

  function buildColor(key, label) {
    var wrap = document.createElement('div'); wrap.className = 'color';
    var pick = document.createElement('input'); pick.type = 'color';
    var alpha = document.createElement('input'); alpha.type = 'range'; alpha.min = 0; alpha.max = 255; alpha.step = 1; alpha.title = 'Alpha';
    var hex = document.createElement('input'); hex.type = 'text'; hex.className = 'hex'; hex.maxLength = 8; hex.spellcheck = false; hex.title = 'RRGGBBAA';
    wrap.appendChild(pick); wrap.appendChild(alpha); wrap.appendChild(hex);
    var current = { r: 255, g: 255, b: 255, a: 255 };
    function push(immediate) {
      var h = rgbaToHex(current);
      hex.value = h;
      sendSet(key, h, immediate);
    }
    pick.addEventListener('input', function () {
      var c = hexToRgba(pick.value.slice(1).toUpperCase() + 'FF');
      current.r = c.r; current.g = c.g; current.b = c.b; dragging[key] = true; push(false);
    });
    pick.addEventListener('change', function () { dragging[key] = false; push(true); });
    alpha.addEventListener('input', function () { current.a = Number(alpha.value); dragging[key] = true; push(false); });
    alpha.addEventListener('change', function () { dragging[key] = false; push(true); });
    hex.addEventListener('change', function () {
      var v = hex.value.replace(/[^0-9a-fA-F]/g, '').toUpperCase();
      if (v.length === 6) v += 'FF';
      if (v.length !== 8) { hex.value = rgbaToHex(current); return; }
      current = hexToRgba(v); apply(); push(true);
    });
    function apply() {
      pick.value = '#' + rgbaToHex(current).slice(0, 6).toLowerCase();
      alpha.value = current.a;
      hex.value = rgbaToHex(current);
    }
    fieldNodes[key] = { set: function (v) { if (dragging[key]) return; current = hexToRgba(String(v).toUpperCase()); apply(); } };
    var row = makeField(label, wrap);
    return row;
  }

  function buildFontList(key, label) {
    var holder = document.createElement('div'); holder.className = 'fontlist';
    var row = document.createElement('div'); row.className = 'field span';
    var l = document.createElement('label'); l.textContent = label; row.appendChild(l); row.appendChild(holder);
    var injected = {};
    fieldNodes[key] = {
      set: function (v) { holder.querySelectorAll('.f').forEach(function (n) { n.classList.toggle('selected', n.dataset.file.toLowerCase() === String(v).split(':')[0].trim().toLowerCase()); }); },
      rebuild: function (files, host) {
        holder.innerHTML = '';
        files.forEach(function (file) {
          var ext = file.split('.').pop().toLowerCase();
          var isFont = ext === 'ttf' || ext === 'otf';
          var family = 'srf-' + file.replace(/[^a-z0-9]/gi, '_');
          if (isFont && !injected[file]) {
            var st = document.createElement('style');
            st.textContent = '@font-face{font-family:"' + family + '";src:url("' + host + encodeURIComponent(file) + '");}';
            document.head.appendChild(st);
            injected[file] = true;
          }
          var tile = document.createElement('div'); tile.className = 'f'; tile.dataset.file = file;
          var sample = document.createElement('div'); sample.className = 'sample';
          sample.textContent = '0123m';
          if (isFont) sample.style.fontFamily = '"' + family + '", monospace'; else sample.textContent = 'bundle';
          var fn = document.createElement('div'); fn.className = 'fn'; fn.textContent = file; fn.title = file;
          tile.appendChild(sample); tile.appendChild(fn);
          tile.addEventListener('click', function () { ov.send('font', JSON.stringify({ file: file })); });
          holder.appendChild(tile);
        });
      }
    };
    return row;
  }

  function buildFields() {
    Object.keys(GROUPS).forEach(function (g) {
      var container = document.querySelector('.fields[data-group="' + g + '"]');
      container.innerHTML = '';
      GROUPS[g].forEach(function (def) {
        var key = def[0], label = def[1], kind = def[2], meta = (state.meta && state.meta[key]) || {};
        var type = meta.type || '';
        var row;
        if (kind === 'fontlist') row = buildFontList(key, label);
        else if (type === 'Boolean') row = buildBool(key, label);
        else if (meta.options) row = buildEnum(key, label, meta);
        else if (type === 'Color') row = buildColor(key, label);
        else if (type === 'Single') row = buildRange(key, label, meta, def[3] != null ? def[3] : 0, def[4] != null ? def[4] : 1);
        else row = buildText(key, label);
        if (meta.description) row.title = meta.description;
        container.appendChild(row);
      });
    });
  }

  function applyValues() {
    Object.keys(state.values || {}).forEach(function (k) {
      if (fieldNodes[k]) fieldNodes[k].set(state.values[k]);
    });
    var fl = fieldNodes['Scope Text.CustomFontFile'];
    if (fl && fl.rebuild) { fl.rebuild(state.fonts || [], state.fontsHost || ''); fl.set(state.values['Scope Text.CustomFontFile'] || ''); }
  }
  function renderGallery() {
    var box = $('gallery');
    box.innerHTML = '';
    var applyToScope = $('applyToScope').checked && state.scope && state.scope.aiming;
    var selected = (state.selected || '').toLowerCase();
    var assigned = (state.scope && state.scope.assigned || '').toLowerCase();

    if (applyToScope) {
      box.appendChild(tile({ name: '(global style)', builtin: true, special: true }, assigned === '', false));
    }
    (state.presets || []).forEach(function (p) {
      var isSel = applyToScope ? p.name.toLowerCase() === assigned : p.name.toLowerCase() === selected;
      box.appendChild(tile(p, isSel, !applyToScope && p.name.toLowerCase() === assigned));
    });
  }

  function tile(p, isSelected, isAssigned) {
    var t = document.createElement('div');
    t.className = 'tile' + (p.builtin ? ' builtin' : '') + (isSelected ? ' selected' : '') + (isAssigned ? ' assigned' : '');
    var img = thumbs[p.name] || p.thumb;
    if (img && !p.special) { var im = document.createElement('img'); im.src = img; im.alt = p.name; t.appendChild(im); }
    else { var ph = document.createElement('div'); ph.className = 'ph'; ph.textContent = p.special ? 'use the global style' : (state.thumbsAvailable ? 'rendering ...' : 'preview while not aiming'); t.appendChild(ph); }
    var name = document.createElement('div'); name.className = 'name';
    var sp = document.createElement('span'); sp.textContent = p.name; sp.title = p.name; name.appendChild(sp);
    var acts = document.createElement('div'); acts.className = 'acts';
    if (!p.special) {
      var cp = document.createElement('button'); cp.textContent = 'Copy'; cp.title = 'Copy as JSON';
      cp.addEventListener('click', function (e) { e.stopPropagation(); exportPreset(p.name); });
      var del = document.createElement('button'); del.textContent = '✕'; del.className = 'danger'; del.title = 'Delete (click twice)';
      var armed = false;
      del.addEventListener('click', function (e) {
        e.stopPropagation();
        if (!armed) { armed = true; del.textContent = 'sure?'; setTimeout(function () { armed = false; del.textContent = '✕'; }, 3000); return; }
        ov.send('delete', JSON.stringify({ name: p.name }));
      });
      acts.appendChild(cp); acts.appendChild(del);
    }
    name.appendChild(acts); t.appendChild(name);
    t.addEventListener('click', function () {
      var toScope = $('applyToScope').checked;
      if (p.special) { ov.send('assignScope', JSON.stringify({ preset: null })); return; }
      ov.send('apply', JSON.stringify({ preset: p.name, target: toScope ? 'scope' : 'global' }));
    });
    return t;
  }

  function exportPreset(name) {
    ov.request('export', name || '').then(function (json) {
      if (!json) { status('error', 'Nothing to copy'); return; }
      copyText(json).then(function () { status('ok', 'Copied ' + (name || 'the current look') + ' to the clipboard'); },
                          function () { status('error', 'Clipboard blocked; select and copy from the paste box'); $('pasteText').value = json; $('pasteBox').classList.remove('hidden'); });
    });
  }

  function copyText(text) {
    if (navigator.clipboard && navigator.clipboard.writeText) return navigator.clipboard.writeText(text);
    return new Promise(function (res, rej) {
      var ta = document.createElement('textarea'); ta.value = text; document.body.appendChild(ta); ta.select();
      try { document.execCommand('copy') ? res() : rej(); } catch (e) { rej(e); } finally { document.body.removeChild(ta); }
    });
  }
  function renderHeader() {
    $('globalName').textContent = state.selected || '(none)';
    $('modified').classList.toggle('hidden', !state.modified);
    var sc = state.scope || {};
    $('scopeName').textContent = !sc.aiming ? '(not aiming)' : (sc.assigned || '(global style)');
    $('scopeKey').textContent = sc.aiming && sc.key ? sc.key : '';
    $('clearScope').classList.toggle('hidden', !(sc.aiming && sc.assigned));
    var atc = $('applyToScope');
    atc.disabled = !sc.aiming;
    if (!sc.aiming) atc.checked = false;
    var hint = $('scopeHint');
    if (sc.aiming && sc.assigned) {
      hint.textContent = 'This scope shows preset "' + sc.assigned + '". The options below edit the global style, which is hidden on this scope - clear the assignment to tune what you see.';
      hint.classList.remove('hidden');
    } else hint.classList.add('hidden');
    $('previewHint').classList.toggle('hidden', !!state.thumbsAvailable);
  }
  ov.on('state', applyState);

  ov.on('thumb', function (json) {
    var m; try { m = JSON.parse(json); } catch (e) { return; }
    thumbs[m.name] = m.png;
    var tiles = $('gallery').querySelectorAll('.tile');
    for (var i = 0; i < tiles.length; i++) {
      var nameEl = tiles[i].querySelector('.name span');
      if (nameEl && nameEl.textContent === m.name) {
        var ph = tiles[i].querySelector('.ph');
        if (ph) { var im = document.createElement('img'); im.src = m.png; im.alt = m.name; tiles[i].replaceChild(im, ph); }
        else { var img = tiles[i].querySelector('img'); if (img) img.src = m.png; }
      }
    }
  });

  ov.on('status', function (json) {
    var m; try { m = JSON.parse(json); } catch (e) { return; }
    status(m.level, m.text);
  });
  var previewTimer = 0;
  function refreshPreview() {
    clearTimeout(previewTimer);
    previewTimer = setTimeout(function () {
      ov.request('getState', 'preview').then(function (json) {
        if (!json) return;
        var s; try { s = JSON.parse(json); } catch (e) { return; }
        if (s && s.preview) $('preview').src = s.preview;
      });
    }, 150);
  }
  $('copyGlobal').addEventListener('click', function () { exportPreset(''); });
  $('clearScope').addEventListener('click', function () { ov.send('assignScope', JSON.stringify({ preset: null })); });
  $('applyToScope').addEventListener('change', renderGallery);
  $('saveBtn').addEventListener('click', function () {
    var n = $('saveName').value.trim();
    if (!n) { status('error', 'Enter a preset name first'); return; }
    ov.send('save', JSON.stringify({ name: n }));
    $('saveName').value = '';
  });
  $('saveName').addEventListener('keydown', function (e) { if (e.key === 'Enter') $('saveBtn').click(); });
  $('pasteBtn').addEventListener('click', function () {
    $('pasteBox').classList.toggle('hidden');
    if (!$('pasteBox').classList.contains('hidden')) {
      $('pasteText').value = '';
      $('pastePreview').textContent = 'Waiting for JSON ...';
      $('importBtn').disabled = true;
      if (navigator.clipboard && navigator.clipboard.readText) {
        navigator.clipboard.readText().then(function (t) { if (t && /ScopeRangefinderStyle/.test(t)) { $('pasteText').value = t; validatePaste(); } }, function () {});
      }
      $('pasteText').focus();
    }
  });
  $('cancelPaste').addEventListener('click', function () { $('pasteBox').classList.add('hidden'); });
  $('pasteText').addEventListener('input', validatePaste);
  function validatePaste() {
    var t = $('pasteText').value.trim();
    var ok = false, msg = 'Waiting for JSON ...';
    if (t) {
      try {
        var d = JSON.parse(t);
        if (d && d.ScopeRangefinderStyle === 1 && d.Values && typeof d.Values === 'object') {
          var n = Object.keys(d.Values).length;
          ok = n > 0;
          msg = ok ? ('"' + (d.Name || 'Imported Preset') + '" - ' + n + ' settings') : 'No values in this preset';
        } else msg = 'Not a Scope Rangefinder style';
      } catch (e) { msg = 'Not valid JSON yet'; }
    }
    $('pastePreview').textContent = msg;
    $('importBtn').disabled = !ok;
  }
  $('importBtn').addEventListener('click', function () {
    ov.send('import', JSON.stringify({ json: $('pasteText').value }));
    $('pasteBox').classList.add('hidden');
  });
  document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && !$('pasteBox').classList.contains('hidden')) { $('pasteBox').classList.add('hidden'); e.preventDefault(); }
  });
  function applyState(json) {
    try { state = JSON.parse(json); } catch (e) { return; }
    if (!Object.keys(fieldNodes).length) buildFields();
    applyValues();
    renderHeader();
    renderGallery();
    refreshPreview();
    if (state.thumbsAvailable) ov.send('requestThumbs', '');
  }
  ov.request('getState', '').then(function (json) { if (json) applyState(json); });
})();
