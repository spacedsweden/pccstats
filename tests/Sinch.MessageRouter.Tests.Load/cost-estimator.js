/**
 * Cost Estimator - Sends messages across all channels and projects cost at scale.
 *
 * Purpose: Estimate the cost of sending messages at scale (100k messages)
 * based on the per-channel pricing returned by the mock API.
 *
 * The test sends a configurable number of messages with the same weighted
 * channel distribution as production, then extrapolates costs.
 *
 * Usage:
 *   k6 run cost-estimator.js
 *   k6 run --out json=cost-results.json cost-estimator.js
 *   SAMPLE_SIZE=5000 k6 run cost-estimator.js
 *   BASE_URL=http://my-gateway:5000 k6 run cost-estimator.js
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { SharedArray } from 'k6/data';
import { Counter, Rate } from 'k6/metrics';

// --- Custom Metrics ---
const errorRate = new Rate('custom_error_rate');
const messagesSent = new Counter('messages_sent');
const totalCostUsd = new Counter('total_cost_usd');

// Per-channel cost counters
const smsCostCounter = new Counter('sms_cost_usd');
const whatsappCostCounter = new Counter('whatsapp_cost_usd');
const rcsCostCounter = new Counter('rcs_cost_usd');
const messengerCostCounter = new Counter('messenger_cost_usd');
const viberCostCounter = new Counter('viber_cost_usd');
const mmsCostCounter = new Counter('mms_cost_usd');
const telegramCostCounter = new Counter('telegram_cost_usd');
const kakaotalkCostCounter = new Counter('kakaotalk_cost_usd');
const lineCostCounter = new Counter('line_cost_usd');
const instagramCostCounter = new Counter('instagram_cost_usd');

// Per-channel message counters
const smsCount = new Counter('sms_count');
const whatsappCount = new Counter('whatsapp_count');
const rcsCount = new Counter('rcs_count');
const messengerCount = new Counter('messenger_count');
const viberCount = new Counter('viber_count');
const mmsCount = new Counter('mms_count');
const telegramCount = new Counter('telegram_count');
const kakaotalkCount = new Counter('kakaotalk_count');
const lineCount = new Counter('line_count');
const instagramCount = new Counter('instagram_count');

// --- Configuration ---
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const PROJECT_ID = __ENV.PROJECT_ID || 'test-project-001';
const SAMPLE_SIZE = parseInt(__ENV.SAMPLE_SIZE || '5000');
const TARGET_SCALE = parseInt(__ENV.TARGET_SCALE || '100000');

// Calculate VUs and duration to achieve SAMPLE_SIZE messages
// Assuming ~5 messages/sec/VU with think time
const VUS = 50;
const estimatedDuration = Math.ceil(SAMPLE_SIZE / (VUS * 5)) + 10; // extra 10s buffer

export const options = {
    stages: [
        { duration: '10s', target: VUS },
        { duration: `${estimatedDuration}s`, target: VUS },
        { duration: '5s', target: 0 },
    ],
    thresholds: {
        http_req_duration: ['p(95)<1000'],
        http_req_failed: ['rate<0.05'],
    },
    tags: {
        test_type: 'cost_estimator',
    },
};

// --- Channel distribution matching production weights ---
const channelWeights = [
    { channel: 'SMS', weight: 0.60, appId: 'app-sms-001' },
    { channel: 'WHATSAPP', weight: 0.20, appId: 'app-whatsapp-001' },
    { channel: 'RCS', weight: 0.10, appId: 'app-rcs-001' },
    { channel: 'MESSENGER', weight: 0.02, appId: 'app-messenger-001' },
    { channel: 'VIBER', weight: 0.02, appId: 'app-viber-001' },
    { channel: 'MMS', weight: 0.015, appId: 'app-mms-001' },
    { channel: 'TELEGRAM', weight: 0.015, appId: 'app-telegram-001' },
    { channel: 'KAKAOTALK', weight: 0.01, appId: 'app-kakaotalk-001' },
    { channel: 'LINE', weight: 0.01, appId: 'app-line-001' },
    { channel: 'INSTAGRAM', weight: 0.01, appId: 'app-instagram-001' },
];

// Build cumulative weights
let cumulative = 0;
const channelSelector = channelWeights.map(cw => {
    cumulative += cw.weight;
    return { ...cw, cumWeight: cumulative };
});

// --- Test Data ---
const contacts = new SharedArray('contacts', function () {
    const data = [];
    for (let i = 0; i < 1000; i++) {
        data.push({
            contact_id: `contact-${String(i).padStart(6, '0')}`,
        });
    }
    return data;
});

// --- Helper Functions ---
function selectChannel() {
    const roll = Math.random();
    for (const cs of channelSelector) {
        if (roll < cs.cumWeight) return cs;
    }
    return channelSelector[0];
}

function getRandomItem(arr) {
    return arr[Math.floor(Math.random() * arr.length)];
}

function getHeaders() {
    return {
        'Content-Type': 'application/json',
        'Authorization': 'Bearer test-api-key-cost',
    };
}

function recordChannelCost(channel, cost) {
    switch (channel) {
        case 'SMS': smsCostCounter.add(cost); break;
        case 'WHATSAPP': whatsappCostCounter.add(cost); break;
        case 'RCS': rcsCostCounter.add(cost); break;
        case 'MESSENGER': messengerCostCounter.add(cost); break;
        case 'VIBER': viberCostCounter.add(cost); break;
        case 'MMS': mmsCostCounter.add(cost); break;
        case 'TELEGRAM': telegramCostCounter.add(cost); break;
        case 'KAKAOTALK': kakaotalkCostCounter.add(cost); break;
        case 'LINE': lineCostCounter.add(cost); break;
        case 'INSTAGRAM': instagramCostCounter.add(cost); break;
    }
}

function recordChannelCount(channel) {
    switch (channel) {
        case 'SMS': smsCount.add(1); break;
        case 'WHATSAPP': whatsappCount.add(1); break;
        case 'RCS': rcsCount.add(1); break;
        case 'MESSENGER': messengerCount.add(1); break;
        case 'VIBER': viberCount.add(1); break;
        case 'MMS': mmsCount.add(1); break;
        case 'TELEGRAM': telegramCount.add(1); break;
        case 'KAKAOTALK': kakaotalkCount.add(1); break;
        case 'LINE': lineCount.add(1); break;
        case 'INSTAGRAM': instagramCount.add(1); break;
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
            text_message: { text: `Cost estimation message via ${channelCfg.channel}` },
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
        'has cost_usd': (r) => {
            try { return JSON.parse(r.body).cost_usd > 0; }
            catch (e) { return false; }
        },
    });

    errorRate.add(!success);

    if (success) {
        messagesSent.add(1);
        recordChannelCount(channelCfg.channel);

        try {
            const body = JSON.parse(res.body);
            if (body.cost_usd) {
                totalCostUsd.add(body.cost_usd);
                recordChannelCost(channelCfg.channel, body.cost_usd);
            }
        } catch (e) { /* ignore */ }
    }

    sleep(0.05 + Math.random() * 0.1);
}

// --- Setup & Teardown ---
export function setup() {
    const healthRes = http.get(`${BASE_URL}/admin/health`);
    check(healthRes, {
        'mock server is healthy': (r) => r.status === 200,
    });

    http.post(`${BASE_URL}/admin/reset`);

    console.log('=== COST ESTIMATOR ===');
    console.log(`Target: ${BASE_URL}`);
    console.log(`Sample size: ~${SAMPLE_SIZE} messages`);
    console.log(`Projection target: ${TARGET_SCALE.toLocaleString()} messages`);
    console.log(`VUs: ${VUS}`);
    console.log('Channel distribution:');
    for (const cw of channelWeights) {
        console.log(`  ${cw.channel.padEnd(12)} ${(cw.weight * 100).toFixed(1)}%`);
    }
    console.log('======================');

    return {
        startTime: new Date().toISOString(),
        targetScale: TARGET_SCALE,
        sampleSize: SAMPLE_SIZE,
    };
}

export function teardown(data) {
    const statsRes = http.get(`${BASE_URL}/admin/stats`);
    if (statsRes.status !== 200) {
        console.error('Failed to fetch stats from mock server.');
        return;
    }

    const stats = JSON.parse(statsRes.body);
    const totalMessages = stats.total_requests;
    const targetScale = data.targetScale || TARGET_SCALE;

    console.log('\n==========================================================');
    console.log('                   COST ESTIMATION REPORT                  ');
    console.log('==========================================================');
    console.log(`Sample size:        ${totalMessages} messages`);
    console.log(`Projection target:  ${targetScale.toLocaleString()} messages`);
    console.log(`Scale factor:       ${(targetScale / totalMessages).toFixed(2)}x`);
    console.log('');

    if (stats.channel_costs && stats.channels) {
        const scaleFactor = targetScale / Math.max(totalMessages, 1);

        console.log('--- Per-Channel Cost Breakdown ---');
        console.log(`${'Channel'.padEnd(14)} ${'Count'.padStart(8)} ${'Sample Cost'.padStart(14)} ${'Avg/Msg'.padStart(12)} ${'Projected'.padStart(14)}`);
        console.log('-'.repeat(66));

        let totalSampleCost = 0;
        let totalProjectedCost = 0;

        const channelEntries = Object.entries(stats.channel_costs).sort((a, b) => b[1] - a[1]);

        for (const [channel, sampleCost] of channelEntries) {
            const count = stats.channels[channel] || 0;
            const avgCost = count > 0 ? sampleCost / count : 0;
            const projectedCost = sampleCost * scaleFactor;

            console.log(
                `${channel.padEnd(14)} ${String(count).padStart(8)} ${'$' + sampleCost.toFixed(4).padStart(13)} ${'$' + avgCost.toFixed(6).padStart(11)} ${'$' + projectedCost.toFixed(2).padStart(13)}`
            );

            totalSampleCost += sampleCost;
            totalProjectedCost += projectedCost;
        }

        console.log('-'.repeat(66));
        console.log(
            `${'TOTAL'.padEnd(14)} ${String(totalMessages).padStart(8)} ${'$' + totalSampleCost.toFixed(4).padStart(13)} ${'$' + (totalSampleCost / Math.max(totalMessages, 1)).toFixed(6).padStart(11)} ${'$' + totalProjectedCost.toFixed(2).padStart(13)}`
        );

        console.log('');
        console.log('--- Cost Projections at Scale ---');
        const avgCostPerMessage = totalSampleCost / Math.max(totalMessages, 1);
        const scales = [1000, 10000, 50000, 100000, 500000, 1000000];

        for (const scale of scales) {
            const cost = avgCostPerMessage * scale;
            const marker = scale === targetScale ? ' <-- TARGET' : '';
            console.log(`  ${String(scale).padStart(10).toLocaleString()} messages: $${cost.toFixed(2)}${marker}`);
        }

        console.log('');
        console.log('--- Monthly Budget Estimates ---');
        const dailyVolumes = [1000, 10000, 100000];
        for (const daily of dailyVolumes) {
            const monthlyCost = avgCostPerMessage * daily * 30;
            console.log(`  ${String(daily).padStart(7)}/day (${(daily * 30).toLocaleString()}/month): $${monthlyCost.toFixed(2)}/month`);
        }
    }

    console.log('');
    console.log('--- Performance Summary ---');
    console.log(`  Throughput:    ${stats.requests_per_second} req/s`);
    console.log(`  Error rate:    ${(stats.error_rate * 100).toFixed(3)}%`);
    console.log(`  Latency p50:   ${stats.latency.p50_ms}ms`);
    console.log(`  Latency p95:   ${stats.latency.p95_ms}ms`);
    console.log(`  Latency p99:   ${stats.latency.p99_ms}ms`);
    console.log('==========================================================\n');
}
