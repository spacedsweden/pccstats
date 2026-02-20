/**
 * Smoke Test - Quick validation that the send message endpoint is functional.
 *
 * Configuration: 10 VUs, 30 seconds duration.
 * Purpose: Fast feedback loop to verify the API is responding correctly
 *          before running heavier load tests.
 *
 * Usage:
 *   k6 run smoke-test.js
 *   k6 run --out json=smoke-results.json smoke-test.js
 *   BASE_URL=http://my-gateway:5000 k6 run smoke-test.js
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { SharedArray } from 'k6/data';
import { Rate, Counter, Trend } from 'k6/metrics';

// --- Custom Metrics ---
const errorRate = new Rate('custom_error_rate');
const messagesSent = new Counter('messages_sent');
const sendLatency = new Trend('send_message_latency', true);

// --- Configuration ---
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5000';
const PROJECT_ID = __ENV.PROJECT_ID || 'test-project-001';

export const options = {
    vus: 10,
    duration: '30s',
    thresholds: {
        http_req_duration: ['p(95)<500', 'p(99)<1000'],
        http_req_failed: ['rate<0.05'],       // Allow 5% failure for smoke test
        custom_error_rate: ['rate<0.05'],
    },
    tags: {
        test_type: 'smoke',
    },
};

// --- Test Data ---
const contacts = new SharedArray('contacts', function () {
    const data = [];
    for (let i = 0; i < 100; i++) {
        data.push({
            contact_id: `contact-${String(i).padStart(6, '0')}`,
            phone: `+1555${String(Math.floor(Math.random() * 10000000)).padStart(7, '0')}`,
        });
    }
    return data;
});

const appIds = new SharedArray('appIds', function () {
    return [
        'app-sms-001',
        'app-whatsapp-001',
        'app-rcs-001',
    ];
});

// --- Helper Functions ---
function getRandomContact() {
    return contacts[Math.floor(Math.random() * contacts.length)];
}

function getRandomAppId() {
    return appIds[Math.floor(Math.random() * appIds.length)];
}

function buildSendMessagePayload(contact, appId) {
    return JSON.stringify({
        app_id: appId,
        recipient: {
            contact_id: contact.contact_id,
        },
        message: {
            text_message: {
                text: `Smoke test message at ${new Date().toISOString()}`,
            },
            channel: 'SMS',
        },
    });
}

// --- Test Scenario ---
export default function () {
    const contact = getRandomContact();
    const appId = getRandomAppId();
    const payload = buildSendMessagePayload(contact, appId);

    const params = {
        headers: {
            'Content-Type': 'application/json',
            'Authorization': 'Bearer test-api-key-smoke',
        },
        tags: { endpoint: 'send_message' },
    };

    const res = http.post(
        `${BASE_URL}/v2/projects/${PROJECT_ID}/messages:send`,
        payload,
        params
    );

    const success = check(res, {
        'status is 200': (r) => r.status === 200,
        'response has message_id': (r) => {
            try {
                const body = JSON.parse(r.body);
                return body.message_id !== undefined && body.message_id.length > 0;
            } catch (e) {
                return false;
            }
        },
        'response has accepted_time': (r) => {
            try {
                const body = JSON.parse(r.body);
                return body.accepted_time !== undefined;
            } catch (e) {
                return false;
            }
        },
        'response has cost_usd': (r) => {
            try {
                const body = JSON.parse(r.body);
                return body.cost_usd !== undefined && body.cost_usd > 0;
            } catch (e) {
                return false;
            }
        },
    });

    errorRate.add(!success);
    if (success) {
        messagesSent.add(1);
    }
    sendLatency.add(res.timings.duration);

    sleep(0.5);
}

// --- Setup & Teardown ---
export function setup() {
    // Verify the mock server is reachable
    const healthRes = http.get(`${BASE_URL}/admin/health`);
    check(healthRes, {
        'mock server is healthy': (r) => r.status === 200,
    });

    // Reset metrics before test
    http.post(`${BASE_URL}/admin/reset`);

    console.log(`Smoke test starting against ${BASE_URL}`);
    console.log(`Project ID: ${PROJECT_ID}`);

    return { startTime: new Date().toISOString() };
}

export function teardown(data) {
    // Fetch and display server-side metrics
    const statsRes = http.get(`${BASE_URL}/admin/stats`);
    if (statsRes.status === 200) {
        const stats = JSON.parse(statsRes.body);
        console.log('\n=== Mock Server Stats ===');
        console.log(`Total requests: ${stats.total_requests}`);
        console.log(`Total errors: ${stats.total_errors}`);
        console.log(`Error rate: ${(stats.error_rate * 100).toFixed(2)}%`);
        console.log(`Requests/sec: ${stats.requests_per_second}`);
        console.log(`Latency p50: ${stats.latency.p50_ms}ms`);
        console.log(`Latency p95: ${stats.latency.p95_ms}ms`);
        console.log(`Latency p99: ${stats.latency.p99_ms}ms`);
        console.log('========================\n');
    }
}
