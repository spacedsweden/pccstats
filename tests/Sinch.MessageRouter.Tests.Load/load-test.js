/**
 * Load Test - Standard load test ramping to 100 VUs over 5 minutes.
 *
 * Tests the send message, list messages, and get message endpoints
 * under sustained moderate load to verify baseline performance.
 *
 * Usage:
 *   k6 run load-test.js
 *   k6 run --out json=load-results.json load-test.js
 *   BASE_URL=http://my-gateway:5000 k6 run load-test.js
 */

import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { SharedArray } from 'k6/data';
import { Rate, Counter, Trend } from 'k6/metrics';

// --- Custom Metrics ---
const errorRate = new Rate('custom_error_rate');
const messagesSent = new Counter('messages_sent');
const messagesListed = new Counter('messages_listed');
const messagesRetrieved = new Counter('messages_retrieved');
const sendLatency = new Trend('send_message_latency', true);
const listLatency = new Trend('list_messages_latency', true);
const getLatency = new Trend('get_message_latency', true);
const totalCost = new Counter('total_cost_usd');

// --- Configuration ---
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const PROJECT_ID = __ENV.PROJECT_ID || 'test-project-001';

export const options = {
    stages: [
        { duration: '30s', target: 20 },   // Warm up to 20 VUs
        { duration: '1m', target: 50 },     // Ramp to 50 VUs
        { duration: '1m', target: 100 },    // Ramp to 100 VUs
        { duration: '2m', target: 100 },    // Hold at 100 VUs
        { duration: '30s', target: 0 },     // Ramp down
    ],
    thresholds: {
        http_req_duration: ['p(95)<500', 'p(99)<1000'],
        http_req_failed: ['rate<0.02'],
        custom_error_rate: ['rate<0.02'],
        send_message_latency: ['p(95)<400'],
        list_messages_latency: ['p(95)<600'],
        get_message_latency: ['p(95)<300'],
    },
    tags: {
        test_type: 'load',
    },
};

// --- Test Data ---
const contacts = new SharedArray('contacts', function () {
    const data = [];
    for (let i = 0; i < 1000; i++) {
        data.push({
            contact_id: `contact-${String(i).padStart(6, '0')}`,
            phone: `+1555${String(Math.floor(Math.random() * 10000000)).padStart(7, '0')}`,
            name: `Test User ${i}`,
        });
    }
    return data;
});

const appIds = new SharedArray('appIds', function () {
    return [
        'app-sms-001',
        'app-sms-002',
        'app-whatsapp-001',
        'app-rcs-001',
        'app-messenger-001',
    ];
});

const channels = new SharedArray('channels', function () {
    return ['SMS', 'WHATSAPP', 'RCS', 'MESSENGER', 'VIBER'];
});

const messageTexts = new SharedArray('messageTexts', function () {
    return [
        'Your verification code is 123456.',
        'Your order #ORD-9876 has been shipped!',
        'Appointment reminder: Dr. Smith at 3:00 PM tomorrow.',
        'Flash sale! 50% off all items. Shop now.',
        'Your payment of $49.99 has been processed.',
        'Welcome to our service! Reply HELP for assistance.',
        'Your delivery is on its way. Track: TRK-55443',
        'Account alert: New login from Chrome on Windows.',
        'Your subscription has been renewed successfully.',
        'Rate your recent purchase and earn 100 points!',
    ];
});

// --- Helper Functions ---
function getRandomItem(arr) {
    return arr[Math.floor(Math.random() * arr.length)];
}

function getHeaders() {
    return {
        'Content-Type': 'application/json',
        'Authorization': 'Bearer test-api-key-load',
    };
}

// --- Test Scenarios ---
export default function () {
    const headers = getHeaders();

    // Weighted scenario selection: 70% send, 20% list, 10% get
    const roll = Math.random();

    if (roll < 0.70) {
        group('Send Message', function () {
            const contact = getRandomItem(contacts);
            const appId = getRandomItem(appIds);
            const channel = getRandomItem(channels);
            const text = getRandomItem(messageTexts);

            const payload = JSON.stringify({
                app_id: appId,
                recipient: {
                    contact_id: contact.contact_id,
                },
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
                    try {
                        return JSON.parse(r.body).message_id !== undefined;
                    } catch (e) { return false; }
                },
            });

            errorRate.add(!success);
            sendLatency.add(res.timings.duration);

            if (success) {
                messagesSent.add(1);
                try {
                    const body = JSON.parse(res.body);
                    if (body.cost_usd) {
                        totalCost.add(body.cost_usd);
                    }
                } catch (e) { /* ignore */ }
            }
        });
    } else if (roll < 0.90) {
        group('List Messages', function () {
            const res = http.get(
                `${BASE_URL}/v2/projects/${PROJECT_ID}/messages?page_size=20`,
                { headers, tags: { endpoint: 'list_messages' } }
            );

            const success = check(res, {
                'list: status 200': (r) => r.status === 200,
                'list: has messages array': (r) => {
                    try {
                        return Array.isArray(JSON.parse(r.body).messages);
                    } catch (e) { return false; }
                },
            });

            errorRate.add(!success);
            listLatency.add(res.timings.duration);
            if (success) messagesListed.add(1);
        });
    } else {
        group('Get Message', function () {
            // Use a random UUID as message ID
            const messageId = `msg-${Math.random().toString(36).substring(2, 15)}`;

            const res = http.get(
                `${BASE_URL}/v2/projects/${PROJECT_ID}/messages/${messageId}`,
                { headers, tags: { endpoint: 'get_message' } }
            );

            const success = check(res, {
                'get: status 200': (r) => r.status === 200,
                'get: has message_id': (r) => {
                    try {
                        return JSON.parse(r.body).message_id !== undefined;
                    } catch (e) { return false; }
                },
            });

            errorRate.add(!success);
            getLatency.add(res.timings.duration);
            if (success) messagesRetrieved.add(1);
        });
    }

    sleep(0.1 + Math.random() * 0.3); // 100-400ms think time
}

// --- Setup & Teardown ---
export function setup() {
    const healthRes = http.get(`${BASE_URL}/admin/health`);
    check(healthRes, {
        'mock server is healthy': (r) => r.status === 200,
    });

    // Reset metrics before test
    http.post(`${BASE_URL}/admin/reset`);

    console.log(`Load test starting against ${BASE_URL}`);
    console.log(`Ramping to 100 VUs over 5 minutes`);

    return { startTime: new Date().toISOString() };
}

export function teardown(data) {
    const statsRes = http.get(`${BASE_URL}/admin/stats`);
    if (statsRes.status === 200) {
        const stats = JSON.parse(statsRes.body);
        console.log('\n========================================');
        console.log('         LOAD TEST SERVER STATS         ');
        console.log('========================================');
        console.log(`Total requests:    ${stats.total_requests}`);
        console.log(`Total errors:      ${stats.total_errors}`);
        console.log(`Error rate:        ${(stats.error_rate * 100).toFixed(2)}%`);
        console.log(`Requests/sec:      ${stats.requests_per_second}`);
        console.log(`Uptime:            ${stats.uptime_seconds.toFixed(1)}s`);
        console.log('--- Latency ---');
        console.log(`  Min:  ${stats.latency.min_ms}ms`);
        console.log(`  p50:  ${stats.latency.p50_ms}ms`);
        console.log(`  p95:  ${stats.latency.p95_ms}ms`);
        console.log(`  p99:  ${stats.latency.p99_ms}ms`);
        console.log(`  Max:  ${stats.latency.max_ms}ms`);
        console.log('--- Request Breakdown ---');
        console.log(`  Send:     ${stats.request_breakdown.send_messages}`);
        console.log(`  Get:      ${stats.request_breakdown.get_requests}`);
        console.log(`  Webhooks: ${stats.request_breakdown.webhook_requests}`);
        console.log(`  Contacts: ${stats.request_breakdown.contact_requests}`);
        if (stats.channels && Object.keys(stats.channels).length > 0) {
            console.log('--- Channel Distribution ---');
            for (const [ch, count] of Object.entries(stats.channels)) {
                console.log(`  ${ch}: ${count}`);
            }
        }
        if (stats.channel_costs && Object.keys(stats.channel_costs).length > 0) {
            console.log('--- Channel Costs (USD) ---');
            let totalCostUsd = 0;
            for (const [ch, cost] of Object.entries(stats.channel_costs)) {
                console.log(`  ${ch}: $${cost.toFixed(4)}`);
                totalCostUsd += cost;
            }
            console.log(`  TOTAL: $${totalCostUsd.toFixed(4)}`);
        }
        console.log('========================================\n');
    }
}
