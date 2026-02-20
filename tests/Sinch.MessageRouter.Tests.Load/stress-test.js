/**
 * Stress Test - Targets 100k+ requests with aggressive VU ramping.
 *
 * Stages:
 *   1. Ramp to 100 VUs over 1 min
 *   2. Hold 100 VUs for 2 min
 *   3. Ramp to 500 VUs over 2 min
 *   4. Hold 500 VUs for 5 min
 *   5. Ramp to 1000 VUs over 2 min
 *   6. Hold 1000 VUs for 5 min
 *   7. Ramp down to 0 over 2 min
 *
 * Usage:
 *   k6 run stress-test.js
 *   k6 run --out json=stress-results.json stress-test.js
 *   BASE_URL=http://my-gateway:5000 k6 run stress-test.js
 */

import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { SharedArray } from 'k6/data';
import { Rate, Counter, Trend, Gauge } from 'k6/metrics';

// --- Custom Metrics ---
const errorRate = new Rate('custom_error_rate');
const messagesSent = new Counter('messages_sent');
const totalRequests = new Counter('total_requests');
const sendLatency = new Trend('send_message_latency', true);
const listLatency = new Trend('list_messages_latency', true);
const getLatency = new Trend('get_message_latency', true);
const webhookLatency = new Trend('webhook_latency', true);
const contactLatency = new Trend('contact_latency', true);
const totalCost = new Counter('total_cost_usd');
const activeVUs = new Gauge('active_vus');

// --- Configuration ---
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const PROJECT_ID = __ENV.PROJECT_ID || 'test-project-001';

export const options = {
    stages: [
        { duration: '1m', target: 100 },     // Stage 1: Ramp to 100 VUs
        { duration: '2m', target: 100 },      // Stage 2: Hold 100 VUs
        { duration: '2m', target: 500 },      // Stage 3: Ramp to 500 VUs
        { duration: '5m', target: 500 },      // Stage 4: Hold 500 VUs
        { duration: '2m', target: 1000 },     // Stage 5: Ramp to 1000 VUs
        { duration: '5m', target: 1000 },     // Stage 6: Hold 1000 VUs
        { duration: '2m', target: 0 },        // Stage 7: Ramp down
    ],
    thresholds: {
        http_req_duration: ['p(95)<500', 'p(99)<1000'],
        http_req_failed: ['rate<0.01'],
        custom_error_rate: ['rate<0.01'],
        send_message_latency: ['p(95)<500', 'p(99)<1000'],
    },
    tags: {
        test_type: 'stress',
    },
    // Increase batch and connection limits for high concurrency
    batch: 20,
    batchPerHost: 20,
    dns: {
        ttl: '5m',
        select: 'roundRobin',
    },
};

// --- Test Data ---
const contacts = new SharedArray('contacts', function () {
    const data = [];
    for (let i = 0; i < 5000; i++) {
        data.push({
            contact_id: `contact-${String(i).padStart(6, '0')}`,
            phone: `+1555${String(Math.floor(Math.random() * 10000000)).padStart(7, '0')}`,
            name: `Stress Test User ${i}`,
        });
    }
    return data;
});

const appIds = new SharedArray('appIds', function () {
    return [
        'app-sms-001', 'app-sms-002', 'app-sms-003',
        'app-whatsapp-001', 'app-whatsapp-002',
        'app-rcs-001',
        'app-messenger-001',
        'app-viber-001',
    ];
});

const channels = new SharedArray('channels', function () {
    // Weighted by generating more entries for common channels
    return [
        'SMS', 'SMS', 'SMS', 'SMS', 'SMS', 'SMS',          // 60%
        'WHATSAPP', 'WHATSAPP',                               // 20%
        'RCS',                                                 // 10%
        'MESSENGER',                                           // 10%
    ];
});

const messageTexts = new SharedArray('messageTexts', function () {
    const texts = [];
    for (let i = 0; i < 50; i++) {
        texts.push(`Stress test message #${i}: Lorem ipsum dolor sit amet, consectetur adipiscing elit. Verification code: ${Math.floor(100000 + Math.random() * 900000)}.`);
    }
    return texts;
});

// --- Helper Functions ---
function getRandomItem(arr) {
    return arr[Math.floor(Math.random() * arr.length)];
}

function getHeaders() {
    return {
        'Content-Type': 'application/json',
        'Authorization': 'Bearer test-api-key-stress',
    };
}

// --- Test Scenarios ---
export default function () {
    const headers = getHeaders();
    activeVUs.add(__VU);

    // Weighted distribution: 60% send, 15% list, 10% get, 10% webhook, 5% contact
    const roll = Math.random();

    if (roll < 0.60) {
        // --- Send Message (60%) ---
        const contact = getRandomItem(contacts);
        const appId = getRandomItem(appIds);
        const channel = getRandomItem(channels);
        const text = getRandomItem(messageTexts);

        const payload = JSON.stringify({
            app_id: appId,
            recipient: { contact_id: contact.contact_id },
            message: {
                text_message: { text },
                channel,
            },
        });

        const res = http.post(
            `${BASE_URL}/v2/projects/${PROJECT_ID}/messages:send`,
            payload,
            { headers, tags: { endpoint: 'send_message' } }
        );

        const success = check(res, {
            'send: status 200': (r) => r.status === 200,
            'send: has message_id': (r) => {
                try { return JSON.parse(r.body).message_id !== undefined; }
                catch (e) { return false; }
            },
        });

        errorRate.add(!success);
        totalRequests.add(1);
        sendLatency.add(res.timings.duration);
        if (success) {
            messagesSent.add(1);
            try {
                const body = JSON.parse(res.body);
                if (body.cost_usd) totalCost.add(body.cost_usd);
            } catch (e) { /* ignore */ }
        }

    } else if (roll < 0.75) {
        // --- List Messages (15%) ---
        const pageSize = Math.floor(Math.random() * 50) + 10;
        const res = http.get(
            `${BASE_URL}/v2/projects/${PROJECT_ID}/messages?page_size=${pageSize}`,
            { headers, tags: { endpoint: 'list_messages' } }
        );

        const success = check(res, {
            'list: status 200': (r) => r.status === 200,
            'list: has messages': (r) => {
                try { return Array.isArray(JSON.parse(r.body).messages); }
                catch (e) { return false; }
            },
        });

        errorRate.add(!success);
        totalRequests.add(1);
        listLatency.add(res.timings.duration);

    } else if (roll < 0.85) {
        // --- Get Message (10%) ---
        const messageId = `msg-${Math.random().toString(36).substring(2, 15)}`;
        const res = http.get(
            `${BASE_URL}/v2/projects/${PROJECT_ID}/messages/${messageId}`,
            { headers, tags: { endpoint: 'get_message' } }
        );

        const success = check(res, {
            'get: status 200': (r) => r.status === 200,
        });

        errorRate.add(!success);
        totalRequests.add(1);
        getLatency.add(res.timings.duration);

    } else if (roll < 0.95) {
        // --- Create Webhook (10%) ---
        const payload = JSON.stringify({
            app_id: getRandomItem(appIds),
            target: `https://webhook.example.com/callback/${Math.random().toString(36).substring(2, 10)}`,
            target_type: 'HTTP',
            triggers: ['MESSAGE_DELIVERY', 'MESSAGE_INBOUND'],
            secret: `secret-${Math.random().toString(36).substring(2, 18)}`,
        });

        const res = http.post(
            `${BASE_URL}/v2/projects/${PROJECT_ID}/webhooks`,
            payload,
            { headers, tags: { endpoint: 'create_webhook' } }
        );

        const success = check(res, {
            'webhook: status 200': (r) => r.status === 200,
            'webhook: has webhook_id': (r) => {
                try { return JSON.parse(r.body).webhook_id !== undefined; }
                catch (e) { return false; }
            },
        });

        errorRate.add(!success);
        totalRequests.add(1);
        webhookLatency.add(res.timings.duration);

    } else {
        // --- Create Contact (5%) ---
        const contact = getRandomItem(contacts);
        const payload = JSON.stringify({
            channel_identities: [
                { channel: 'SMS', identity: contact.phone },
            ],
            display_name: contact.name,
            email: `user${Math.floor(Math.random() * 100000)}@test.example.com`,
            language: 'en-US',
        });

        const res = http.post(
            `${BASE_URL}/v2/projects/${PROJECT_ID}/contacts`,
            payload,
            { headers, tags: { endpoint: 'create_contact' } }
        );

        const success = check(res, {
            'contact: status 200': (r) => r.status === 200,
            'contact: has contact_id': (r) => {
                try { return JSON.parse(r.body).contact_id !== undefined; }
                catch (e) { return false; }
            },
        });

        errorRate.add(!success);
        totalRequests.add(1);
        contactLatency.add(res.timings.duration);
    }

    // Minimal think time for maximum throughput at high VU counts
    sleep(0.05 + Math.random() * 0.1); // 50-150ms
}

// --- Setup & Teardown ---
export function setup() {
    const healthRes = http.get(`${BASE_URL}/admin/health`);
    const healthy = check(healthRes, {
        'mock server is healthy': (r) => r.status === 200,
    });

    if (!healthy) {
        console.error('Mock server is not reachable! Aborting test.');
        return { abort: true };
    }

    // Disable latency for maximum throughput test (optional)
    // Uncomment the next line to test raw throughput without simulated latency:
    // http.post(`${BASE_URL}/admin/config`, JSON.stringify({ enabled: false }), { headers: { 'Content-Type': 'application/json' } });

    // Reset metrics
    http.post(`${BASE_URL}/admin/reset`);

    console.log('=== STRESS TEST CONFIGURATION ===');
    console.log(`Target: ${BASE_URL}`);
    console.log(`Project: ${PROJECT_ID}`);
    console.log('Stages:');
    console.log('  1. Ramp to 100 VUs   (1 min)');
    console.log('  2. Hold 100 VUs      (2 min)');
    console.log('  3. Ramp to 500 VUs   (2 min)');
    console.log('  4. Hold 500 VUs      (5 min)');
    console.log('  5. Ramp to 1000 VUs  (2 min)');
    console.log('  6. Hold 1000 VUs     (5 min)');
    console.log('  7. Ramp down to 0    (2 min)');
    console.log('Total duration: ~19 minutes');
    console.log('Target: 100k+ requests');
    console.log('=================================');

    return { startTime: new Date().toISOString() };
}

export function teardown(data) {
    const statsRes = http.get(`${BASE_URL}/admin/stats`);
    if (statsRes.status === 200) {
        const stats = JSON.parse(statsRes.body);
        console.log('\n=============================================');
        console.log('           STRESS TEST RESULTS               ');
        console.log('=============================================');
        console.log(`Duration:           ${stats.uptime_seconds.toFixed(1)}s`);
        console.log(`Total requests:     ${stats.total_requests}`);
        console.log(`Total errors:       ${stats.total_errors}`);
        console.log(`Error rate:         ${(stats.error_rate * 100).toFixed(3)}%`);
        console.log(`Throughput:         ${stats.requests_per_second} req/s`);
        console.log('');
        console.log('--- Latency Percentiles ---');
        console.log(`  Min:  ${stats.latency.min_ms}ms`);
        console.log(`  p50:  ${stats.latency.p50_ms}ms`);
        console.log(`  p95:  ${stats.latency.p95_ms}ms`);
        console.log(`  p99:  ${stats.latency.p99_ms}ms`);
        console.log(`  Max:  ${stats.latency.max_ms}ms`);
        console.log('');
        console.log('--- Request Breakdown ---');
        console.log(`  Send:     ${stats.request_breakdown.send_messages}`);
        console.log(`  Get:      ${stats.request_breakdown.get_requests}`);
        console.log(`  Webhooks: ${stats.request_breakdown.webhook_requests}`);
        console.log(`  Contacts: ${stats.request_breakdown.contact_requests}`);
        console.log('');
        if (stats.channels && Object.keys(stats.channels).length > 0) {
            console.log('--- Channel Distribution ---');
            const totalMsgs = Object.values(stats.channels).reduce((a, b) => a + b, 0);
            for (const [ch, count] of Object.entries(stats.channels).sort((a, b) => b[1] - a[1])) {
                const pct = ((count / totalMsgs) * 100).toFixed(1);
                console.log(`  ${ch.padEnd(12)} ${String(count).padStart(8)}  (${pct}%)`);
            }
        }
        if (stats.channel_costs && Object.keys(stats.channel_costs).length > 0) {
            console.log('');
            console.log('--- Cost Summary (USD) ---');
            let totalCostUsd = 0;
            for (const [ch, cost] of Object.entries(stats.channel_costs).sort((a, b) => b[1] - a[1])) {
                console.log(`  ${ch.padEnd(12)} $${cost.toFixed(4)}`);
                totalCostUsd += cost;
            }
            console.log(`  ${'TOTAL'.padEnd(12)} $${totalCostUsd.toFixed(4)}`);
            console.log(`  Projected 100k: $${((totalCostUsd / stats.total_requests) * 100000).toFixed(2)}`);
        }
        console.log('');
        const target100k = stats.total_requests >= 100000 ? 'ACHIEVED' : 'NOT REACHED';
        console.log(`100k request target: ${target100k} (${stats.total_requests} requests)`);
        console.log('=============================================\n');
    }
}
