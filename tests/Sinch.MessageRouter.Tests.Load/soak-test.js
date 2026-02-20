/**
 * Soak (Endurance) Test - 200 VUs for 30 minutes.
 *
 * Purpose: Detect memory leaks, connection exhaustion, resource degradation,
 * and other issues that only manifest under sustained load over time.
 *
 * Monitors:
 * - Latency drift over time (increasing latency = possible leak)
 * - Error rate stability
 * - Throughput consistency
 *
 * Usage:
 *   k6 run soak-test.js
 *   k6 run --out json=soak-results.json soak-test.js
 *   BASE_URL=http://my-gateway:5000 k6 run soak-test.js
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { SharedArray } from 'k6/data';
import { Rate, Counter, Trend, Gauge } from 'k6/metrics';

// --- Custom Metrics ---
const errorRate = new Rate('custom_error_rate');
const messagesSent = new Counter('messages_sent');
const sendLatency = new Trend('send_message_latency', true);
const totalCost = new Counter('total_cost_usd');

// Time-bucketed latency tracking for drift detection
const latencyBucket1 = new Trend('latency_0_5min', true);
const latencyBucket2 = new Trend('latency_5_10min', true);
const latencyBucket3 = new Trend('latency_10_15min', true);
const latencyBucket4 = new Trend('latency_15_20min', true);
const latencyBucket5 = new Trend('latency_20_25min', true);
const latencyBucket6 = new Trend('latency_25_30min', true);

// --- Configuration ---
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const PROJECT_ID = __ENV.PROJECT_ID || 'test-project-001';
const SOAK_DURATION = __ENV.SOAK_DURATION || '30m';
const SOAK_VUS = parseInt(__ENV.SOAK_VUS || '200');

export const options = {
    stages: [
        { duration: '2m', target: SOAK_VUS },         // Ramp up
        { duration: SOAK_DURATION, target: SOAK_VUS }, // Sustained load
        { duration: '2m', target: 0 },                 // Ramp down
    ],
    thresholds: {
        http_req_duration: ['p(95)<500', 'p(99)<1000'],
        http_req_failed: ['rate<0.02'],
        custom_error_rate: ['rate<0.02'],
        send_message_latency: ['p(95)<500'],
        // Ensure latency doesn't drift more than 2x between first and last buckets
        // (manual check in teardown since k6 doesn't support cross-metric thresholds)
    },
    tags: {
        test_type: 'soak',
    },
};

// --- Test Data ---
const contacts = new SharedArray('contacts', function () {
    const data = [];
    for (let i = 0; i < 2000; i++) {
        data.push({
            contact_id: `contact-${String(i).padStart(6, '0')}`,
            phone: `+1555${String(Math.floor(Math.random() * 10000000)).padStart(7, '0')}`,
        });
    }
    return data;
});

const appIds = new SharedArray('appIds', function () {
    return ['app-sms-001', 'app-whatsapp-001', 'app-rcs-001', 'app-messenger-001'];
});

const channels = new SharedArray('channels', function () {
    return [
        'SMS', 'SMS', 'SMS', 'SMS', 'SMS', 'SMS',
        'WHATSAPP', 'WHATSAPP',
        'RCS',
        'MESSENGER',
    ];
});

const messageTexts = new SharedArray('messageTexts', function () {
    const texts = [];
    for (let i = 0; i < 100; i++) {
        texts.push(`Soak test message #${i}: Your code is ${Math.floor(100000 + Math.random() * 900000)}. This is an endurance test message to verify system stability.`);
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
        'Authorization': 'Bearer test-api-key-soak',
    };
}

// Track test start time for bucket assignment
let testStartTime = null;

function getTimeBucket(elapsedMs) {
    const minutes = elapsedMs / 60000;
    if (minutes < 5) return 1;
    if (minutes < 10) return 2;
    if (minutes < 15) return 3;
    if (minutes < 20) return 4;
    if (minutes < 25) return 5;
    return 6;
}

function recordToBucket(bucket, value) {
    switch (bucket) {
        case 1: latencyBucket1.add(value); break;
        case 2: latencyBucket2.add(value); break;
        case 3: latencyBucket3.add(value); break;
        case 4: latencyBucket4.add(value); break;
        case 5: latencyBucket5.add(value); break;
        case 6: latencyBucket6.add(value); break;
    }
}

// --- Test Scenarios ---
export default function () {
    if (!testStartTime) {
        testStartTime = Date.now();
    }

    const headers = getHeaders();

    // Mix of operations: 80% send, 10% list, 10% get
    const roll = Math.random();

    if (roll < 0.80) {
        // Send Message
        const contact = getRandomItem(contacts);
        const channel = getRandomItem(channels);

        const payload = JSON.stringify({
            app_id: getRandomItem(appIds),
            recipient: { contact_id: contact.contact_id },
            message: {
                text_message: { text: getRandomItem(messageTexts) },
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
            'send: valid response': (r) => {
                try { return JSON.parse(r.body).message_id !== undefined; }
                catch (e) { return false; }
            },
        });

        errorRate.add(!success);
        sendLatency.add(res.timings.duration);

        // Record to time bucket for drift analysis
        const elapsed = Date.now() - testStartTime;
        const bucket = getTimeBucket(elapsed);
        recordToBucket(bucket, res.timings.duration);

        if (success) {
            messagesSent.add(1);
            try {
                const body = JSON.parse(res.body);
                if (body.cost_usd) totalCost.add(body.cost_usd);
            } catch (e) { /* ignore */ }
        }

    } else if (roll < 0.90) {
        // List Messages
        const res = http.get(
            `${BASE_URL}/v2/projects/${PROJECT_ID}/messages?page_size=10`,
            { headers, tags: { endpoint: 'list_messages' } }
        );

        check(res, {
            'list: status 200': (r) => r.status === 200,
        });

        errorRate.add(res.status !== 200);

    } else {
        // Get Message
        const messageId = `msg-${Math.random().toString(36).substring(2, 15)}`;
        const res = http.get(
            `${BASE_URL}/v2/projects/${PROJECT_ID}/messages/${messageId}`,
            { headers, tags: { endpoint: 'get_message' } }
        );

        check(res, {
            'get: status 200': (r) => r.status === 200,
        });

        errorRate.add(res.status !== 200);
    }

    sleep(0.1 + Math.random() * 0.2); // 100-300ms think time
}

// --- Setup & Teardown ---
export function setup() {
    const healthRes = http.get(`${BASE_URL}/admin/health`);
    const healthy = check(healthRes, {
        'mock server is healthy': (r) => r.status === 200,
    });

    if (!healthy) {
        console.error('Mock server is not reachable!');
        return { abort: true };
    }

    http.post(`${BASE_URL}/admin/reset`);

    console.log('=== SOAK (ENDURANCE) TEST ===');
    console.log(`Target: ${BASE_URL}`);
    console.log(`VUs: ${SOAK_VUS}`);
    console.log(`Duration: ${SOAK_DURATION}`);
    console.log(`Purpose: Detect memory leaks and resource degradation`);
    console.log('=============================');

    return { startTime: new Date().toISOString() };
}

export function teardown(data) {
    const statsRes = http.get(`${BASE_URL}/admin/stats`);
    if (statsRes.status === 200) {
        const stats = JSON.parse(statsRes.body);
        console.log('\n=============================================');
        console.log('          SOAK TEST RESULTS                  ');
        console.log('=============================================');
        console.log(`Duration:         ${(stats.uptime_seconds / 60).toFixed(1)} minutes`);
        console.log(`Total requests:   ${stats.total_requests}`);
        console.log(`Total errors:     ${stats.total_errors}`);
        console.log(`Error rate:       ${(stats.error_rate * 100).toFixed(3)}%`);
        console.log(`Avg throughput:   ${stats.requests_per_second} req/s`);
        console.log('');
        console.log('--- Latency Percentiles (Full Test) ---');
        console.log(`  Min:  ${stats.latency.min_ms}ms`);
        console.log(`  p50:  ${stats.latency.p50_ms}ms`);
        console.log(`  p95:  ${stats.latency.p95_ms}ms`);
        console.log(`  p99:  ${stats.latency.p99_ms}ms`);
        console.log(`  Max:  ${stats.latency.max_ms}ms`);
        console.log('');
        console.log('--- Latency Drift Analysis ---');
        console.log('  Check the latency_*min metrics in the k6 output for drift.');
        console.log('  If latency_25_30min p95 is > 2x latency_0_5min p95, there');
        console.log('  may be a memory leak or resource exhaustion issue.');
        console.log('');
        if (stats.channels && Object.keys(stats.channels).length > 0) {
            console.log('--- Channel Distribution ---');
            for (const [ch, count] of Object.entries(stats.channels).sort((a, b) => b[1] - a[1])) {
                console.log(`  ${ch.padEnd(12)} ${count}`);
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
        }
        console.log('=============================================\n');
    }
}
