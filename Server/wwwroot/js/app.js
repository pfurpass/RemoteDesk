/* ── State ──────────────────────────────────────────────────────────────── */
let hub = null;
let authToken = null;

// pcId → { canvas, ctx, img, fpsFrames, fpsLast, fps, focusEl }
const sessions = new Map();

// current layout: 'auto' | '1' | '2' | '4'
let currentLayout = 'auto';

const SERVER = window.location.origin;

/* ── DOM ────────────────────────────────────────────────────────────────── */
const $ = id => document.getElementById(id);
const loginScreen = $('loginScreen');
const dashboardScreen = $('dashboardScreen');
const loginError = $('loginError');
const btnLogin = $('btnLogin');
const inputUsername = $('inputUsername');
const inputPassword = $('inputPassword');
const hubStatus = $('hubStatus');
const pcList = $('pcList');
const viewerPlaceholder = $('viewerPlaceholder');
const tileGrid = $('tileGrid');
const layoutSwitcher = $('layoutSwitcher');

/* ── Auth ───────────────────────────────────────────────────────────────── */
async function doLogin() {
    const username = inputUsername.value.trim();
    const password = inputPassword.value;
    if (!username || !password) { showLoginError('Enter username and password.'); return; }
    btnLogin.disabled = true;
    btnLogin.querySelector('span').textContent = 'Signing in…';
    try {
        const res = await fetch(`${SERVER}/api/auth/login`, {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ username, password })
        });
        if (!res.ok) { showLoginError((await res.json()).message || 'Invalid credentials.'); return; }
        const data = await res.json();
        authToken = data.token;
        sessionStorage.setItem('rdToken', authToken);
        sessionStorage.setItem('rdUser', data.username);
        showDashboard(data.username);
    } catch { showLoginError('Server unreachable.'); }
    finally { btnLogin.disabled = false; btnLogin.querySelector('span').textContent = 'Sign In'; }
}

function showLoginError(msg) { loginError.textContent = msg; loginError.classList.remove('hidden'); }

function doLogout() {
    sessionStorage.clear();
    sessions.forEach((_, pcId) => stopSession(pcId));
    if (hub) hub.stop();
    hub = null; authToken = null;
    loginScreen.classList.add('active');
    dashboardScreen.classList.remove('active');
    inputPassword.value = '';
}

/* ── Dashboard ──────────────────────────────────────────────────────────── */
function showDashboard(username) {
    loginScreen.classList.remove('active');
    dashboardScreen.classList.add('active');
    $('usernameDisplay').textContent = username;
    connectHub();
}

/* ── Hub ────────────────────────────────────────────────────────────────── */
async function connectHub() {
    setHubStatus('Connecting…', 'yellow');
    hub = new signalR.HubConnectionBuilder()
        .withUrl(`${SERVER}/remotehub`, { accessTokenFactory: () => authToken })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    hub.on('ReceiveFrame', (pcId, b64) => renderFrame(pcId, b64));
    hub.on('PcOnline', pcId => addOrUpdatePcItem(pcId, true));
    hub.on('PcOffline', pcId => { addOrUpdatePcItem(pcId, false); stopSession(pcId); });
    hub.onreconnecting(() => setHubStatus('Reconnecting…', 'yellow'));
    hub.onreconnected(() => { setHubStatus('Connected', 'green'); refreshPcList(); });
    hub.onclose(() => setHubStatus('Disconnected', 'red'));

    try {
        await hub.start();
        setHubStatus('Connected', 'green');
        await refreshPcList();
    } catch { setHubStatus('Connection failed', 'red'); }
}

// NOTE: Server hub SendFrame only sends to viewers group — we need pcId in ReceiveFrame.
// The server's RemoteHub.SendFrame forwards as ReceiveFrame(frameBase64) without pcId.
// We route by tracking which pcId each connection is watching via a local map.
// Simpler fix: patch server to also send pcId, OR use one connection per PC.
// Best approach here: we use a per-session sub-connection for each watched PC.
// See createSession() below.

function setHubStatus(label, color) {
    hubStatus.querySelector('.dot').className = `dot ${color}`;
    hubStatus.querySelector('span').textContent = label;
}

/* ── PC List ─────────────────────────────────────────────────────────────── */
async function refreshPcList() {
    if (!hub || hub.state !== signalR.HubConnectionState.Connected) return;
    try {
        const pcs = await hub.invoke('GetOnlinePcs');
        pcList.innerHTML = '';
        if (!pcs || pcs.length === 0) { pcList.innerHTML = '<div class="pc-empty">No PCs online</div>'; return; }
        pcs.forEach(pcId => addOrUpdatePcItem(pcId, true));
    } catch (e) { console.error(e); }
}

function addOrUpdatePcItem(pcId, online) {
    let item = document.querySelector(`.pc-item[data-pc="${pcId}"]`);
    if (!item) {
        item = document.createElement('button');
        item.className = 'pc-item';
        item.dataset.pc = pcId;
        item.innerHTML = `<span class="pc-icon"></span><span class="pc-name">${escHtml(pcId)}</span>`;
        item.addEventListener('click', () => toggleSession(pcId));
        const empty = pcList.querySelector('.pc-empty');
        if (empty) empty.remove();
        pcList.appendChild(item);
    }
    const dot = item.querySelector('.pc-icon');
    dot.style.background = online ? '#44ff88' : '#666';
    dot.style.boxShadow = online ? '0 0 6px #44ff88' : 'none';
    item.style.opacity = online ? '1' : '0.5';
    item.disabled = !online;

    // Mark active if session open
    item.classList.toggle('active', sessions.has(pcId));
}

/* ── Sessions (one SignalR connection per PC for clean frame routing) ─────── */
function toggleSession(pcId) {
    if (sessions.has(pcId)) stopSession(pcId);
    else startSession(pcId);
}

async function startSession(pcId) {
    if (sessions.has(pcId)) return;

    // Create a dedicated hub connection for this PC so frames don't get mixed up
    const conn = new signalR.HubConnectionBuilder()
        .withUrl(`${SERVER}/remotehub`, { accessTokenFactory: () => authToken })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    // Build tile DOM
    const tile = document.createElement('div');
    tile.className = 'tile';
    tile.dataset.pc = pcId;
    tile.innerHTML = `
    <div class="tile-toolbar">
      <span class="tile-pcname">${escHtml(pcId)}</span>
      <span class="tile-badge live">● LIVE</span>
      <span class="tile-badge fps" id="fps-${escId(pcId)}">— fps</span>
      <div class="tile-controls">
        <button class="btn-toolbar" onclick="popOut('${escHtml(pcId)}')" title="Pop out to new window">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6"/><polyline points="15 3 21 3 21 9"/><line x1="10" y1="14" x2="21" y2="3"/></svg>
        </button>
        <button class="btn-toolbar" onclick="toggleTileFullscreen('${escHtml(pcId)}')" title="Fullscreen">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><path d="M8 3H5a2 2 0 0 0-2 2v3m18 0V5a2 2 0 0 0-2-2h-3m0 18h3a2 2 0 0 0 2-2v-3M3 16v3a2 2 0 0 0 2 2h3"/></svg>
        </button>
        <button class="btn-toolbar btn-disconnect" onclick="stopSession('${escHtml(pcId)}')" title="Close">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
        </button>
      </div>
    </div>
    <div class="tile-canvas-wrap" id="wrap-${escId(pcId)}" tabindex="0"></div>
  `;

    tileGrid.appendChild(tile);

    const canvas = document.createElement('canvas');
    const wrap = tile.querySelector('.tile-canvas-wrap');
    wrap.appendChild(canvas);

    const ctx = canvas.getContext('2d');
    const img = new Image();

    const session = {
        conn, tile, canvas, ctx, img,
        fpsFrames: 0, fpsLast: Date.now(), fps: 0
    };
    sessions.set(pcId, session);

    // Frame rendering
    img.onload = () => {
        if (canvas.width !== img.naturalWidth || canvas.height !== img.naturalHeight) {
            canvas.width = img.naturalWidth;
            canvas.height = img.naturalHeight;
        }
        ctx.drawImage(img, 0, 0);
        // FPS
        session.fpsFrames++;
        const now = Date.now();
        if (now - session.fpsLast >= 1000) {
            session.fps = Math.round(session.fpsFrames * 1000 / (now - session.fpsLast));
            session.fpsFrames = 0; session.fpsLast = now;
            const el = $(`fps-${escId(pcId)}`);
            if (el) el.textContent = session.fps + ' fps';
        }
    };

    conn.on('ReceiveFrame', b64 => { img.src = 'data:image/jpeg;base64,' + b64; });
    conn.on('PcOffline', id => { if (id === pcId) stopSession(pcId); });

    // Mouse events
    function coords(e) {
        const r = canvas.getBoundingClientRect();
        return {
            x: Math.round((e.clientX - r.left) * (canvas.width / r.width)),
            y: Math.round((e.clientY - r.top) * (canvas.height / r.height))
        };
    }
    function sm(e, t) { const { x, y } = coords(e); conn.invoke('SendMouseEvent', pcId, x, y, t); }

    canvas.addEventListener('mousemove', e => sm(e, 'mousemove'));
    canvas.addEventListener('mousedown', e => { e.preventDefault(); sm(e, 'mousedown'); wrap.focus(); });
    canvas.addEventListener('mouseup', e => sm(e, 'mouseup'));
    canvas.addEventListener('dblclick', e => sm(e, 'dblclick'));
    canvas.addEventListener('contextmenu', e => { e.preventDefault(); sm(e, 'contextmenu'); });
    canvas.addEventListener('wheel', e => {
        e.preventDefault();
        const { x, y } = coords(e);
        conn.invoke('SendMouseEvent', pcId, x, y, e.deltaY > 0 ? 'scrolldown' : 'scrollup');
    }, { passive: false });

    // Keyboard — single listener on wrap div only
    wrap.addEventListener('keydown', e => {
        e.preventDefault(); e.stopPropagation();
        conn.invoke('SendKeyboardEvent', pcId, e.key, true);
    });
    wrap.addEventListener('keyup', e => {
        e.preventDefault(); e.stopPropagation();
        conn.invoke('SendKeyboardEvent', pcId, e.key, false);
    });

    wrap.style.cursor = 'none';

    // Connect and watch
    try {
        await conn.start();
        await conn.invoke('WatchPc', pcId);
    } catch (e) { console.error('Session connect error:', e); }

    updateGrid();
    updatePcItemState(pcId);
    viewerPlaceholder.classList.add('hidden');
    layoutSwitcher.style.display = 'flex';
}

async function stopSession(pcId) {
    const s = sessions.get(pcId);
    if (!s) return;
    try {
        await s.conn.invoke('StopWatching', pcId);
        await s.conn.stop();
    } catch { }
    s.tile.remove();
    sessions.delete(pcId);
    updateGrid();
    updatePcItemState(pcId);
    if (sessions.size === 0) {
        viewerPlaceholder.classList.remove('hidden');
        layoutSwitcher.style.display = 'none';
    }
}

/* ── Pop Out ──────────────────────────────────────────────────────────────── */
function popOut(pcId) {
    const w = screen.width / 2;
    const h = screen.height;
    const url = `/viewer.html?pc=${encodeURIComponent(pcId)}&token=${encodeURIComponent(authToken)}&srv=${encodeURIComponent(SERVER)}`;
    window.open(url, `rd_${pcId}`, `width=${w},height=${h},left=${w},top=0`);
    // Optionally close the in-grid tile
    stopSession(pcId);
}

/* ── Fullscreen per tile ──────────────────────────────────────────────────── */
function toggleTileFullscreen(pcId) {
    const s = sessions.get(pcId);
    if (!s) return;
    if (!document.fullscreenElement) s.tile.requestFullscreen?.();
    else document.exitFullscreen?.();
}

/* ── Grid layout ──────────────────────────────────────────────────────────── */
function setLayout(mode) {
    currentLayout = mode;
    document.querySelectorAll('.layout-btn').forEach(b => b.classList.remove('active'));
    $('layout' + (mode === 'auto' ? 'Auto' : mode))?.classList.add('active');
    updateGrid();
}

function updateGrid() {
    const count = sessions.size;
    if (count === 0) { tileGrid.className = 'tile-grid'; return; }

    let cols;
    if (currentLayout === '1') cols = 1;
    else if (currentLayout === '2') cols = 2;
    else if (currentLayout === '4') cols = 2;
    else {
        // Auto
        if (count === 1) cols = 1;
        else if (count === 2) cols = 2;
        else cols = 2; // 3–4 → 2×2
    }

    tileGrid.style.gridTemplateColumns = `repeat(${cols}, 1fr)`;
    tileGrid.className = 'tile-grid visible';
}

function updatePcItemState(pcId) {
    const item = document.querySelector(`.pc-item[data-pc="${pcId}"]`);
    if (item) item.classList.toggle('active', sessions.has(pcId));
}

/* ── Helpers ─────────────────────────────────────────────────────────────── */
function renderFrame(pcId, b64) {
    // Only used for the main hub — not needed since each session has its own conn
}

function escHtml(s) { return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;'); }
function escId(s) { return s.replace(/[^a-zA-Z0-9_-]/g, '_'); }

/* ── Login shortcut ─────────────────────────────────────────────────────── */
[inputUsername, inputPassword].forEach(el => {
    el.addEventListener('keydown', e => { if (e.key === 'Enter') doLogin(); });
});

/* ── Auto re-login ──────────────────────────────────────────────────────── */
window.addEventListener('load', () => {
    const t = sessionStorage.getItem('rdToken');
    const u = sessionStorage.getItem('rdUser');
    if (t && u) { authToken = t; showDashboard(u); }
});