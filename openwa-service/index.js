/**
 * Ministerium WhatsApp Service
 * Powered by whatsapp-web.js (open source, MIT license)
 *
 * Exposes a REST API to send WhatsApp Web messages.
 * First startup requires scanning the QR from the phone.
 */

require('dotenv').config();
const express = require('express');
const { Client, LocalAuth } = require('whatsapp-web.js');
const qrcode = require('qrcode');
const qrTerminal = require('qrcode-terminal');
const fs = require('fs');
const path = require('path');

const app = express();
const PORT = process.env.PORT || 3000;
const API_KEY = process.env.API_KEY || '';

// Clear Chromium lock files after container restarts.
function cleanChromiumLocks() {
    const authDir = '/app/.wwebjs_auth';
    if (!fs.existsSync(authDir)) return;

    const walk = (dir) => {
        if (!fs.existsSync(dir)) return;
        for (const file of fs.readdirSync(dir)) {
            const full = path.join(dir, file);
            if (file === 'SingletonLock' || file === 'SingletonCookie' || file === 'SingletonSocket') {
                try {
                    fs.unlinkSync(full);
                    console.log(`[WA] Lock removed: ${full}`);
                } catch {}
            } else if (fs.statSync(full).isDirectory()) {
                walk(full);
            }
        }
    };

    walk(authDir);
}
cleanChromiumLocks();

app.use(express.json());

let clientReady = false;
let currentQr = null;
let qrBase64 = null;
let sessionInfo = null;

const client = new Client({
    authStrategy: new LocalAuth({ clientId: 'ministerium' }),
    puppeteer: {
        headless: true,
        executablePath: process.env.PUPPETEER_EXECUTABLE_PATH || '/usr/bin/chromium',
        args: [
            '--no-sandbox',
            '--disable-setuid-sandbox',
            '--disable-dev-shm-usage',
            '--disable-accelerated-2d-canvas',
            '--no-first-run',
            '--no-zygote',
            '--single-process',
            '--disable-gpu',
            '--window-size=1280,800'
        ]
    },
    webVersionCache: {
        type: 'remote',
        remotePath: 'https://raw.githubusercontent.com/wppconnect-team/wa-version/main/html/2.3000.1023606481-alpha.html'
    }
});

client.on('qr', async (qr) => {
    currentQr = qr;
    qrTerminal.generate(qr, { small: true });
    try {
        qrBase64 = await qrcode.toDataURL(qr);
    } catch {
        qrBase64 = null;
    }
    console.log('[WA] QR generated. Scan it with your phone.');
    console.log('[WA] QR available at: http://localhost:' + PORT + '/qr');
});

client.on('authenticated', () => {
    console.log('[WA] Authenticated successfully.');
    currentQr = null;
    qrBase64 = null;
});

client.on('auth_failure', (msg) => {
    console.error('[WA] Authentication error:', msg);
    clientReady = false;
});

client.on('ready', async () => {
    clientReady = true;
    const info = client.info;
    sessionInfo = {
        phone: info?.wid?.user || 'unknown',
        name: info?.pushname || 'Ministerium WA'
    };
    console.log(`[WA] Ready. Connected as: ${sessionInfo.name} (${sessionInfo.phone})`);
});

client.on('disconnected', (reason) => {
    clientReady = false;
    console.warn('[WA] Disconnected:', reason);
    setTimeout(() => client.initialize(), 5000);
});

function authMiddleware(req, res, next) {
    if (!API_KEY) return next();
    const key = req.headers['x-api-key'] || req.query.key;
    if (key !== API_KEY) return res.status(401).json({ error: 'Invalid API key.' });
    next();
}

function unique(values) {
    return [...new Set(values.filter(Boolean))];
}

function buildPhoneCandidates(phone) {
    let digits = (phone || '').replace(/\D/g, '');
    if (!digits) return [];

    if (digits.startsWith('00')) digits = digits.slice(2);
    if (digits.startsWith('01') && digits.length === 12) digits = digits.slice(2);

    const candidates = [digits];

    // Mexico: try both modern mobile (521) and country code only (52).
    if (digits.length === 10) {
        candidates.push(`521${digits}`);
        candidates.push(`52${digits}`);
    }

    if (digits.length === 12 && digits.startsWith('52')) {
        candidates.push(`521${digits.slice(2)}`);
    }

    if (digits.length === 13 && digits.startsWith('521')) {
        candidates.push(`52${digits.slice(3)}`);
    }

    return unique(candidates);
}

async function resolveChatCandidates(digits) {
    const candidates = [];
    try {
        const numberId = await client.getNumberId(digits);
        if (numberId && numberId._serialized) candidates.push(numberId._serialized);
    } catch (err) {
        console.warn(`[WA] getNumberId failed for ${digits}: ${err.message}`);
    }

    // Fallback for whatsapp-web.js LID/getNumberId issues.
    candidates.push(`${digits}@c.us`);
    return unique(candidates);
}

async function sendMessageWithFallback(to, message) {
    const phoneCandidates = buildPhoneCandidates(to);
    if (phoneCandidates.length === 0) {
        throw new Error('Empty or invalid WhatsApp number.');
    }

    const attempts = [];

    for (const digits of phoneCandidates) {
        const chatCandidates = await resolveChatCandidates(digits);
        for (const chatId of chatCandidates) {
            try {
                await client.sendMessage(chatId, message);
                return { chatId, digits, phoneCandidates };
            } catch (err) {
                attempts.push(`${chatId}: ${err.message}`);
                console.warn(`[WA] sendMessage failed for ${chatId}: ${err.message}`);
            }
        }
    }

    throw new Error(`Could not send WhatsApp. Attempts: ${attempts.slice(-4).join(' | ')}`);
}

app.get('/status', (req, res) => {
    res.json({
        status: clientReady ? 'ready' : (currentQr ? 'waiting_qr' : 'initializing'),
        ready: clientReady,
        hasQr: !!currentQr,
        session: sessionInfo
    });
});

app.get('/qr', (req, res) => {
    if (clientReady) {
        return res.send(`
            <html><body style="font-family:Arial;text-align:center;padding:40px;background:#f8fafc">
            <h2 style="color:#10B981">WhatsApp Connected</h2>
            <p style="color:#64748B">Number: <strong>${sessionInfo?.phone}</strong></p>
            <p style="color:#64748B">Account: <strong>${sessionInfo?.name}</strong></p>
            </body></html>
        `);
    }

    if (!currentQr) {
        return res.send(`
            <html><head><meta http-equiv="refresh" content="3"></head>
            <body style="font-family:Arial;text-align:center;padding:40px;background:#f8fafc">
            <h2 style="color:#F59E0B">Initializing...</h2>
            <p>Wait a few seconds, the QR will appear here.</p>
            </body></html>
        `);
    }

    res.send(`
        <html><head><meta http-equiv="refresh" content="30"></head>
        <body style="font-family:Arial;text-align:center;padding:40px;background:#f8fafc">
        <h2 style="color:#0c2248">Scan the QR with WhatsApp</h2>
        <p style="color:#64748B;margin-bottom:20px">
            Open WhatsApp > Linked devices > Link a device
        </p>
        <img src="${qrBase64}" style="border-radius:12px;box-shadow:0 4px 24px rgba(0,0,0,.1);max-width:300px" />
        <p style="color:#94A3B8;font-size:.85rem;margin-top:16px">
            This page refreshes automatically every 30s
        </p>
        </body></html>
    `);
});

app.post('/send', authMiddleware, async (req, res) => {
    const { to, message } = req.body;

    if (!to || !message) {
        return res.status(400).json({ error: 'Required fields: to, message' });
    }
    if (!clientReady) {
        return res.status(503).json({ error: 'WhatsApp is not ready. Scan the QR first.' });
    }

    try {
        const result = await sendMessageWithFallback(to, message);
        console.log(`[WA] Message sent to ${to} (${result.chatId})`);
        res.json({ success: true, to: result.chatId, normalized: result.digits, candidates: result.phoneCandidates });
    } catch (err) {
        console.error('[WA] Send error:', err.message);
        res.status(500).json({ error: err.message });
    }
});

app.post('/send-bulk', authMiddleware, async (req, res) => {
    const { recipients, message } = req.body;
    if (!Array.isArray(recipients) || !message) {
        return res.status(400).json({ error: 'recipients (array) and message are required.' });
    }
    if (!clientReady) {
        return res.status(503).json({ error: 'WhatsApp is not ready.' });
    }

    const results = [];
    for (const to of recipients) {
        try {
            const result = await sendMessageWithFallback(to, message);
            results.push({ to, success: true, normalized: result.digits, chatId: result.chatId });
            await new Promise(r => setTimeout(r, 1000));
        } catch (err) {
            results.push({ to, success: false, error: err.message });
        }
    }

    res.json({ results });
});

app.post('/logout', authMiddleware, async (req, res) => {
    try {
        await client.logout();
        clientReady = false;
        res.json({ success: true, message: 'Session closed.' });
    } catch (err) {
        res.status(500).json({ error: err.message });
    }
});

app.listen(PORT, () => {
    console.log(`[WA] Server started on port ${PORT}`);
    console.log(`[WA] QR available at: http://localhost:${PORT}/qr`);
});

client.initialize().catch(err => {
    console.error('[WA] Client initialization error:', err.message);
});
