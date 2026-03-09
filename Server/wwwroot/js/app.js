/* ── State ──────────────────────────────────────────────────────────────── */
let hub = null;
let authToken = null;
let currentPcId = null;
let fpsCounter = { frames: 0, last: Date.now(), fps: 0 };

const SERVER_URL = window.location.origin;

/* ── DOM refs ───────────────────────────────────────────────────────────── */
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
const viewerWrap = $('viewerWrap');
const viewerPcName = $('viewerPcName');
const badgeFps = $('badgeFps');
const canvas = $('remoteCanvas');
const ctx = canvas.getContext('2d');
const canvasContainer = $('canvasContainer');
const cursorOverlay = $('cursorOverlay');

/* ── Auth ───────────────────────────────────────────────────────────────── */
async function doLogin() {
    const username = inputUsername.value.trim();
    const password = inputPassword.value;
    if (!username || !password) { showLoginError('Enter username and password.'); return; }

    btnLogin.disabled = true;
    btnLogin.querySelector('span').textContent = 'Signing in…';

    try {
        const res = await fetch(`${SERVER_URL}/api/auth/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ username, password })
        });

        if (!res.ok) {
            const data = await res.json();
            showLoginError(data.message || 'Invalid credentials.');
            return;
        }

        const data = await res.json();
        authToken = data.token;
        sessionStorage.setItem('rdToken', authToken);
        sessionStorage.setItem('rdUser', data.username);

        showDashboard(data.username);
    } catch (e) {
        showLoginError('Server unreachable. Check your connection.');
    } finally {
        btnLogin.disabled = false;
        btnLogin.querySelector('span').textContent = 'Sign In';
    }
}

function showLoginError(msg) {
    loginError.textContent = msg;
    loginError.classList.remove('hidden');
}

function doLogout() {
    sessionStorage.clear();
    if (hub) hub.stop();
    hub = null; authToken = null; currentPcId = null;
    loginScreen.classList.add('active');
    dashboardScreen.classList.remove('active');
    inputPassword.value = '';
}

/* ── Dashboard init ─────────────────────────────────────────────────────── */
function showDashboard(username) {
    loginScreen.classList.remove('active');
    dashboardScreen.classList.add('active');
    $('usernameDisplay').textContent = username;
    connectHub();
}

/* ── SignalR Hub ────────────────────────────────────────────────────────── */
async function connectHub() {
    setHubStatus('Connecting…', 'yellow');

    hub = new signalR.HubConnectionBuilder()
        .withUrl(`${SERVER_URL}/remotehub`, {
            accessTokenFactory: () => authToken
        })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    /* ── Incoming messages ────────────────────────────────────────────────── */
    hub.on('ReceiveFrame', (base64) => {
        renderFrame(base64);
    });

    hub.on('PcOnline', (pcId) => {
        addOrUpdatePcItem(pcId, true);
    });

    hub.on('PcOffline', (pcId) => {
        addOrUpdatePcItem(pcId, false);
        if (currentPcId === pcId) {
            showPlaceholder();
        }
    });

    hub.onreconnecting(() => setHubStatus('Reconnecting…', 'yellow'));
    hub.onreconnected(() => { setHubStatus('Connected', 'green'); refreshPcList(); });
    hub.onclose(() => setHubStatus('Disconnected', 'red'));

    try {
        await hub.start();
        setHubStatus('Connected', 'green');
        await refreshPcList();
    } catch (e) {
        setHubStatus('Connection failed', 'red');
        console.error('Hub error:', e);
    }
}

function setHubStatus(label, color) {
    const dot = hubStatus.querySelector('.dot');
    const span = hubStatus.querySelector('span');
    dot.className = `dot ${color}`;
    span.textContent = label;
}

/* ── PC List ─────────────────────────────────────────────────────────────── */
async function refreshPcList() {
    if (!hub || hub.state !== signalR.HubConnectionState.Connected) return;
    try {
        const pcs = await hub.invoke('GetOnlinePcs');
        pcList.innerHTML = '';
        if (!pcs || pcs.length === 0) {
            pcList.innerHTML = '<div class="pc-empty">No PCs online</div>';
            return;
        }
        pcs.forEach(pcId => addOrUpdatePcItem(pcId, true));
    } catch (e) { console.error('GetOnlinePcs error:', e); }
}

function addOrUpdatePcItem(pcId, online) {
    let item = document.querySelector(`.pc-item[data-pc="${pcId}"]`);
    if (!item) {
        item = document.createElement('button');
        item.className = 'pc-item';
        item.dataset.pc = pcId;
        item.innerHTML = `<span class="pc-icon"></span><span class="pc-name">${escHtml(pcId)}</span>`;
        item.addEventListener('click', () => connectToPc(pcId));
        // Remove "no PCs" message if present
        const empty = pcList.querySelector('.pc-empty');
        if (empty) empty.remove();
        pcList.appendChild(item);
    }

    const dot = item.querySelector('.pc-icon');
    dot.style.background = online ? '#44ff88' : '#666';
    dot.style.boxShadow = online ? '0 0 6px #44ff88' : 'none';
    item.style.opacity = online ? '1' : '0.5';
    item.disabled = !online;
}

/* ── Viewer ──────────────────────────────────────────────────────────────── */
async function connectToPc(pcId) {
    if (!hub) return;
    if (currentPcId && currentPcId !== pcId) {
        await hub.invoke('StopWatching', currentPcId);
    }

    currentPcId = pcId;
    document.querySelectorAll('.pc-item').forEach(i => i.classList.remove('active'));
    const item = document.querySelector(`.pc-item[data-pc="${pcId}"]`);
    if (item) item.classList.add('active');

    viewerPcName.textContent = pcId;
    viewerPlaceholder.classList.add('hidden');
    viewerWrap.classList.remove('hidden');

    await hub.invoke('WatchPc', pcId);
}

function disconnectViewer() {
    if (currentPcId && hub) {
        hub.invoke('StopWatching', currentPcId);
        document.querySelectorAll('.pc-item').forEach(i => i.classList.remove('active'));
        currentPcId = null;
    }
    showPlaceholder();
}

function showPlaceholder() {
    viewerPlaceholder.classList.remove('hidden');
    viewerWrap.classList.add('hidden');
    currentPcId = null;
    ctx.clearRect(0, 0, canvas.width, canvas.height);
}

/* ── Frame rendering ─────────────────────────────────────────────────────── */
const img = new Image();
img.onload = () => {
    if (canvas.width !== img.naturalWidth || canvas.height !== img.naturalHeight) {
        canvas.width = img.naturalWidth;
        canvas.height = img.naturalHeight;
    }
    ctx.drawImage(img, 0, 0);
    countFps();
};

function renderFrame(base64) {
    img.src = 'data:image/jpeg;base64,' + base64;
}

function countFps() {
    fpsCounter.frames++;
    const now = Date.now();
    const elapsed = now - fpsCounter.last;
    if (elapsed >= 1000) {
        fpsCounter.fps = Math.round(fpsCounter.frames * 1000 / elapsed);
        fpsCounter.frames = 0;
        fpsCounter.last = now;
        badgeFps.textContent = fpsCounter.fps + ' fps';
    }
}

/* ── Mouse events ────────────────────────────────────────────────────────── */
function getCanvasCoords(e) {
    const rect = canvas.getBoundingClientRect();
    const scaleX = canvas.width / rect.width;
    const scaleY = canvas.height / rect.height;
    return {
        x: Math.round((e.clientX - rect.left) * scaleX),
        y: Math.round((e.clientY - rect.top) * scaleY)
    };
}

function sendMouse(e, type) {
    if (!currentPcId || !hub) return;
    const { x, y } = getCanvasCoords(e);
    hub.invoke('SendMouseEvent', currentPcId, x, y, type);

    // Update custom cursor position
    const rect = canvasContainer.getBoundingClientRect();
    cursorOverlay.style.left = (e.clientX - rect.left) + 'px';
    cursorOverlay.style.top = (e.clientY - rect.top) + 'px';
}

canvas.addEventListener('mousemove', e => sendMouse(e, 'mousemove'));
canvas.addEventListener('mousedown', e => { e.preventDefault(); sendMouse(e, 'mousedown'); });
canvas.addEventListener('mouseup', e => sendMouse(e, 'mouseup'));
canvas.addEventListener('dblclick', e => sendMouse(e, 'dblclick'));
canvas.addEventListener('contextmenu', e => { e.preventDefault(); sendMouse(e, 'contextmenu'); });

canvas.addEventListener('wheel', e => {
    e.preventDefault();
    if (!currentPcId || !hub) return;
    const { x, y } = getCanvasCoords(e);
    hub.invoke('SendMouseEvent', currentPcId, x, y, e.deltaY > 0 ? 'scrolldown' : 'scrollup');
}, { passive: false });

/* ── Keyboard events ─────────────────────────────────────────────────────── */
document.addEventListener('keydown', e => {
    if (!currentPcId || !hub || !viewerWrap.classList.contains('hidden') === false) return;
    e.preventDefault();
    hub.invoke('SendKeyboardEvent', currentPcId, e.key, true);
});

document.addEventListener('keyup', e => {
    if (!currentPcId || !hub) return;
    hub.invoke('SendKeyboardEvent', currentPcId, e.key, false);
});

// Capture keys when canvas is focused
canvasContainer.addEventListener('click', () => canvasContainer.focus());
canvasContainer.setAttribute('tabindex', '0');
canvasContainer.addEventListener('keydown', e => {
    if (!currentPcId || !hub) return;
    e.preventDefault();
    hub.invoke('SendKeyboardEvent', currentPcId, e.key, true);
});
canvasContainer.addEventListener('keyup', e => {
    if (!currentPcId || !hub) return;
    hub.invoke('SendKeyboardEvent', currentPcId, e.key, false);
});

/* ── Fullscreen ──────────────────────────────────────────────────────────── */
function toggleFullscreen() {
    const el = canvasContainer;
    if (!document.fullscreenElement) el.requestFullscreen?.();
    else document.exitFullscreen?.();
}

/* ── Keyboard shortcut: Enter login ─────────────────────────────────────── */
[inputUsername, inputPassword].forEach(el => {
    el.addEventListener('keydown', e => { if (e.key === 'Enter') doLogin(); });
});

/* ── Helpers ─────────────────────────────────────────────────────────────── */
function escHtml(s) {
    return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

/* ── Auto re-login from session ──────────────────────────────────────────── */
window.addEventListener('load', () => {
    const t = sessionStorage.getItem('rdToken');
    const u = sessionStorage.getItem('rdUser');
    if (t && u) {
        authToken = t;
        showDashboard(u);
    }
});