/**
 * Multichannel Test - Tests all messaging channels with weighted distribution.
 *
 * Channel Distribution:
 *   SMS:        60%
 *   WhatsApp:   20%
 *   RCS:        10%
 *   Others:     10% (Messenger, Viber, MMS, Telegram, KakaoTalk, LINE, Instagram)
 *
 * Purpose: Validate routing, cost tracking, and performance across all channels.
 *
 * Usage:
 *   k6 run multichannel-test.js
 *   k6 run --out json=multichannel-results.json multichannel-test.js
 *   BASE_URL=http://my-gateway:5000 k6 run multichannel-test.js
 */

import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { SharedArray } from 'k6/data';
import { Rate, Counter, Trend } from 'k6/metrics';

// --- Custom Metrics ---
const errorRate = new Rate('custom_error_rate');
const messagesSent = new Counter('messages_sent');
const totalCost = new Counter('total_cost_usd');

// Per-channel metrics
const smsLatency = new Trend('sms_latency', true);
const whatsappLatency = new Trend('whatsapp_latency', true);
const rcsLatency = new Trend('rcs_latency', true);
const messengerLatency = new Trend('messenger_latency', true);
const viberLatency = new Trend('viber_latency', true);
const mmsLatency = new Trend('mms_latency', true);
const telegramLatency = new Trend('telegram_latency', true);
const kakaotalkLatency = new Trend('kakaotalk_latency', true);
const lineLatency = new Trend('line_latency', true);
const instagramLatency = new Trend('instagram_latency', true);

const smsSent = new Counter('sms_sent');
const whatsappSent = new Counter('whatsapp_sent');
const rcsSent = new Counter('rcs_sent');
const messengerSent = new Counter('messenger_sent');
const viberSent = new Counter('viber_sent');
const mmsSent = new Counter('mms_sent');
const telegramSent = new Counter('telegram_sent');
const kakaotalkSent = new Counter('kakaotalk_sent');
const lineSent = new Counter('line_sent');
const instagramSent = new Counter('instagram_sent');

const smsCost = new Counter('sms_cost_usd');
const whatsappCost = new Counter('whatsapp_cost_usd');
const rcsCost = new Counter('rcs_cost_usd');
const otherCost = new Counter('other_cost_usd');

// --- Configuration ---
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const PROJECT_ID = __ENV.PROJECT_ID || 'test-project-001';

export const options = {
    stages: [
        { duration: '30s', target: 50 },
        { duration: '1m', target: 100 },
        { duration: '3m', target: 200 },
        { duration: '3m', target: 200 },
        { duration: '1m', target: 0 },
    ],
    thresholds: {
        http_req_duration: ['p(95)<500', 'p(99)<1000'],
        http_req_failed: ['rate<0.02'],
        custom_error_rate: ['rate<0.02'],
        sms_latency: ['p(95)<400'],
        whatsapp_latency: ['p(95)<400'],
        rcs_latency: ['p(95)<400'],
    },
    tags: {
        test_type: 'multichannel',
    },
};

// --- Channel Weight Configuration ---
// Each entry: { channel, weight, appId, messageTemplate }
const channelConfig = [
    // SMS: 60%
    { channel: 'SMS', cumulativeWeight: 0.60, appId: 'app-sms-001', template: 'Your verification code is {code}. Valid for 5 minutes.' },
    // WhatsApp: 20%
    { channel: 'WHATSAPP', cumulativeWeight: 0.80, appId: 'app-whatsapp-001', template: 'Hi {name}! Your order #{order} is on its way.' },
    // RCS: 10%
    { channel: 'RCS', cumulativeWeight: 0.90, appId: 'app-rcs-001', template: 'Welcome to our new RCS experience! Tap below to get started.' },
    // Messenger: 2%
    { channel: 'MESSENGER', cumulativeWeight: 0.92, appId: 'app-messenger-001', template: 'Thanks for reaching out! A support agent will reply shortly.' },
    // Viber: 2%
    { channel: 'VIBER', cumulativeWeight: 0.94, appId: 'app-viber-001', template: 'Flash sale! 30% off for the next 24 hours.' },
    // MMS: 1.5%
    { channel: 'MMS', cumulativeWeight: 0.955, appId: 'app-mms-001', template: 'Check out this image from your recent trip!' },
    // Telegram: 1.5%
    { channel: 'TELEGRAM', cumulativeWeight: 0.97, appId: 'app-telegram-001', template: 'Your Telegram notification: New message in group.' },
    // KakaoTalk: 1%
    { channel: 'KAKAOTALK', cumulativeWeight: 0.98, appId: 'app-kakaotalk-001', template: 'KakaoTalk: Your delivery update.' },
    // LINE: 1%
    { channel: 'LINE', cumulativeWeight: 0.99, appId: 'app-line-001', template: 'LINE: You have a new coupon!' },
    // Instagram: 1%
    { channel: 'INSTAGRAM', cumulativeWeight: 1.00, appId: 'app-instagram-001', template: 'Thanks for your DM! We will get back to you soon.' },
];

// --- Test Data ---
const contacts = new SharedArray('contacts', function () {
    const data = [];
    for (let i = 0; i < 2000; i++) {
        data.push({
            contact_id: `contact-${String(i).padStart(6, '0')}`,
            phone: `+1555${String(Math.floor(Math.random() * 10000000)).padStart(7, '0')}`,
            name: `User ${i}`,
        });
    }
    return data;
});

// --- Helper Functions ---
function getRandomItem(arr) {
    return arr[Math.floor(Math.random() * arr.length)];
}

function selectChannel() {
    const roll = Math.random();
    for (const cfg of channelConfig) {
        if (roll < cfg.cumulativeWeight) {
            return cfg;
        }
    }
    return channelConfig[0]; // fallback to SMS
}

function formatMessage(template) {
    return template
        .replace('{code}', String(Math.floor(100000 + Math.random() * 900000)))
        .replace('{name}', `User${Math.floor(Math.random() * 1000)}`)
        .replace('{order}', `ORD-${Math.floor(10000 + Math.random() * 90000)}`);
}

function getHeaders() {
    return {
        'Content-Type': 'application/json',
        'Authorization': 'Bearer test-api-key-multichannel',
    };
}

function recordChannelLatency(channel, duration) {
    switch (channel) {
        case 'SMS': smsLatency.add(duration); break;
        case 'WHATSAPP': whatsappLatency.add(duration); break;
        case 'RCS': rcsLatency.add(duration); break;
        case 'MESSENGER': messengerLatency.add(duration); break;
        case 'VIBER': viberLatency.add(duration); break;
        case 'MMS': mmsLatency.add(duration); break;
        case 'TELEGRAM': telegramLatency.add(duration); break;
        case 'KAKAOTALK': kakaotalkLatency.add(duration); break;
        case 'LINE': lineLatency.add(duration); break;
        case 'INSTAGRAM': instagramLatency.add(duration); break;
    }
}

function recordChannelSent(channel) {
    switch (channel) {
        case 'SMS': smsSent.add(1); break;
        case 'WHATSAPP': whatsappSent.add(1); break;
        case 'RCS': rcsSent.add(1); break;
        case 'MESSENGER': messengerSent.add(1); break;
        case 'VIBER': viberSent.add(1); break;
        case 'MMS': mmsSent.add(1); break;
        case 'TELEGRAM': telegramSent.add(1); break;
        case 'KAKAOTALK': kakaotalkSent.add(1); break;
        case 'LINE': lineSent.add(1); break;
        case 'INSTAGRAM': instagramSent.add(1); break;
    }
}

function recordChannelCost(channel, cost) {
    switch (channel) {
        case 'SMS': smsCost.add(cost); break;
        case 'WHATSAPP': whatsappCost.add(cost); break;
        case 'RCS': rcsCost.add(cost); break;
        default: otherCost.add(cost); break;
    }
}

// --- Test Scenario ---
export default function () {
    const headers = getHeaders();
    const channelCfg = selectChannel();
    const contact = getRandomItem(contacts);

    const payload = JSON.stringify({
        app_id: channelCfg.appId,
        recipient: { contact_id: contact.contact_id },
        message: {
            text_message: { text: formatMessage(channelCfg.template) },
            channel: channelCfg.channel,
        },
    });

    const res = http.post(
        `${BASE_URL}/v2/projects/${PROJECT_ID}/messages:send`,
        payload,
        { headers, tags: { endpoint: 'send_message', channel: channelCfg.channel } }
    );

    const success = check(res, {
        'status 200': (r) => r.status === 200,
        'has message_id': (r) => {
            try { return JSON.parse(r.body).message_id !== undefined; }
            catch (e) { return false; }
        },
        'has cost_usd': (r) => {
            try { return JSON.parse(r.body).cost_usd !== undefined; }
            catch (e) { return false; }
        },
        'correct channel in response': (r) => {
            try { return JSON.parse(r.body).channel === channelCfg.channel; }
            catch (e) { return false; }
        },
    });

    errorRate.add(!success);
    recordChannelLatency(channelCfg.channel, res.timings.duration);

    if (success) {
        messagesSent.add(1);
        recordChannelSent(channelCfg.channel);
        try {
            const body = JSON.parse(res.body);
            if (body.cost_usd) {
                totalCost.add(body.cost_usd);
                recordChannelCost(channelCfg.channel, body.cost_usd);
            }
        } catch (e) { /* ignore */ }
    }

    sleep(0.1 + Math.random() * 0.2);
}

// --- Setup & Teardown ---
export function setup() {
    const healthRes = http.get(`${BASE_URL}/admin/health`);
    check(healthRes, {
        'mock server is healthy': (r) => r.status === 200,
    });

    http.post(`${BASE_URL}/admin/reset`);

    console.log('=== MULTICHANNEL TEST ===');
    console.log(`Target: ${BASE_URL}`);
    console.log('Channel weights:');
    console.log('  SMS:        60%');
    console.log('  WhatsApp:   20%');
    console.log('  RCS:        10%');
    console.log('  Messenger:   2%');
    console.log('  Viber:       2%');
    console.log('  MMS:       1.5%');
    console.log('  Telegram:  1.5%');
    console.log('  KakaoTalk:   1%');
    console.log('  LINE:        1%');
    console.log('  Instagram:   1%');
    console.log('=========================');

    return { startTime: new Date().toISOString() };
}

export function teardown(data) {
    const statsRes = http.get(`${BASE_URL}/admin/stats`);
    if (statsRes.status === 200) {
        const stats = JSON.parse(statsRes.body);
        console.log('\n=============================================');
        console.log('        MULTICHANNEL TEST RESULTS             ');
        console.log('=============================================');
        console.log(`Total messages:   ${stats.total_requests}`);
        console.log(`Total errors:     ${stats.total_errors}`);
        console.log(`Error rate:       ${(stats.error_rate * 100).toFixed(3)}%`);
        console.log(`Throughput:       ${stats.requests_per_second} req/s`);
        console.log('');

        if (stats.channels && Object.keys(stats.channels).length > 0) {
            console.log('--- Channel Distribution ---');
            const totalMsgs = Object.values(stats.channels).reduce((a, b) => a + b, 0);
            for (const [ch, count] of Object.entries(stats.channels).sort((a, b) => b[1] - a[1])) {
                const pct = ((count / totalMsgs) * 100).toFixed(1);
                const bar = '#'.repeat(Math.max(1, Math.round(pct / 2)));
                console.log(`  ${ch.padEnd(12)} ${String(count).padStart(8)}  ${pct.padStart(5)}%  ${bar}`);
            }
        }

        if (stats.channel_costs && Object.keys(stats.channel_costs).length > 0) {
            console.log('');
            console.log('--- Cost per Channel (USD) ---');
            let totalCostUsd = 0;
            for (const [ch, cost] of Object.entries(stats.channel_costs).sort((a, b) => b[1] - a[1])) {
                const count = stats.channels[ch] || 0;
                const avgCost = count > 0 ? (cost / count) : 0;
                console.log(`  ${ch.padEnd(12)} $${cost.toFixed(4).padStart(10)}  (avg: $${avgCost.toFixed(4)}/msg)`);
                totalCostUsd += cost;
            }
            console.log(`  ${'TOTAL'.padEnd(12)} $${totalCostUsd.toFixed(4).padStart(10)}`);

            if (stats.total_requests > 0) {
                const avgCostPerMsg = totalCostUsd / stats.total_requests;
                console.log('');
                console.log('--- Cost Projections ---');
                console.log(`  Avg cost/message:   $${avgCostPerMsg.toFixed(6)}`);
                console.log(`  Per 1,000 msgs:     $${(avgCostPerMsg * 1000).toFixed(2)}`);
                console.log(`  Per 10,000 msgs:    $${(avgCostPerMsg * 10000).toFixed(2)}`);
                console.log(`  Per 100,000 msgs:   $${(avgCostPerMsg * 100000).toFixed(2)}`);
                console.log(`  Per 1,000,000 msgs: $${(avgCostPerMsg * 1000000).toFixed(2)}`);
            }
        }

        console.log('');
        console.log('--- Latency (Full Test) ---');
        console.log(`  p50:  ${stats.latency.p50_ms}ms`);
        console.log(`  p95:  ${stats.latency.p95_ms}ms`);
        console.log(`  p99:  ${stats.latency.p99_ms}ms`);
        console.log('=============================================\n');
    }
}
