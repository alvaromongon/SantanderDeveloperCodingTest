// Load test validating the service SLO (see README.md > Service Level Objectives).
// The thresholds below ARE the SLO: if any is breached, k6 exits with a non-zero code.
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter } from 'k6/metrics';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.1.0/index.js';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';
const ENDPOINT = __ENV.ENDPOINT || '/api/stories/best';
const STUB_ADMIN_URL = __ENV.STUB_ADMIN_URL || 'http://localhost:8081/__admin';
const RATE = Number(__ENV.RATE || 1000);
const DURATION = __ENV.DURATION || '3m';
const REFRESH_INTERVAL_SECONDS = Number(__ENV.REFRESH_INTERVAL_SECONDS || 60);

// SLO targets, calibrated on the reference runner (saturation at ~2000-2700 req/s, see README.md).
const SLO = {
  p95Ms: 50,
  p99Ms: 100,
  maxErrorRate: 0.001,
  rps: RATE,
};

// Upstream protection: at most one full refresh (1 list + 200 items) per refresh interval,
// plus one extra refresh of margin, regardless of the incoming load.
const REQUESTS_PER_REFRESH = 201;
const durationSeconds = parseDuration(DURATION);
const maxUpstreamRequests = (Math.ceil(durationSeconds / REFRESH_INTERVAL_SECONDS) + 1) * REQUESTS_PER_REFRESH;

const upstreamRequests = new Counter('hackernews_upstream_requests');

export const options = {
  scenarios: {
    slo: {
      executor: 'constant-arrival-rate',
      rate: RATE,
      timeUnit: '1s',
      duration: DURATION,
      preAllocatedVUs: 100,
      maxVUs: 1000,
    },
  },
  thresholds: {
    'http_req_duration{scenario:slo}': [`p(95)<${SLO.p95Ms}`, `p(99)<${SLO.p99Ms}`],
    'http_req_failed{scenario:slo}': [`rate<${SLO.maxErrorRate}`],
    'checks{scenario:slo}': [`rate>${1 - SLO.maxErrorRate}`],
    // The generator must sustain the target rate, otherwise the throughput objective is not proven.
    dropped_iterations: [`count<${Math.max(1, Math.floor(RATE * durationSeconds * SLO.maxErrorRate))}`],
    hackernews_upstream_requests: [`count<=${maxUpstreamRequests}`],
  },
  summaryTrendStats: ['avg', 'min', 'med', 'p(90)', 'p(95)', 'p(99)', 'max'],
};

export function setup() {
  // Wait until the API is ready (the ranking is cached), so the test measures steady state.
  const deadline = Date.now() + 60_000;
  let ready = false;
  while (!ready && Date.now() < deadline) {
    ready = http.get(`${BASE_URL}/health/ready`, { tags: { name: 'readiness' } }).status === 200;
    if (!ready) {
      sleep(1);
    }
  }
  if (!ready) {
    throw new Error(`API not ready at ${BASE_URL}/health/ready`);
  }

  // Only count upstream requests made during the measured window.
  http.del(`${STUB_ADMIN_URL}/requests`);
}

export default function () {
  // Mix of page sizes, including the worst case (all stories).
  const count = Math.random() < 0.3 ? 200 : 1 + Math.floor(Math.random() * 200);
  const response = http.get(`${BASE_URL}${ENDPOINT}?count=${count}`, { tags: { name: 'best-stories' } });

  check(response, {
    'status is 200': (r) => r.status === 200,
    'returns at most n stories': (r) => {
      const body = r.json();
      return Array.isArray(body) && body.length <= count;
    },
  });
}

export function teardown() {
  const response = http.post(
    `${STUB_ADMIN_URL}/requests/count`,
    JSON.stringify({ method: 'GET', urlPathPattern: '/v0/.*' }),
    { headers: { 'Content-Type': 'application/json' } },
  );
  upstreamRequests.add(response.json('count'));
}

export function handleSummary(data) {
  return {
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
    '/results/summary.json': JSON.stringify(data, null, 2),
    '/results/summary.md': markdownSummary(data),
  };
}

function markdownSummary(data) {
  const metric = (name) => data.metrics[name] || { values: {}, thresholds: {} };
  const passed = (name) => Object.values(metric(name).thresholds || {}).every((t) => t.ok);
  const icon = (ok) => (ok ? '✅' : '❌');
  const duration = metric('http_req_duration{scenario:slo}').values;
  const failed = metric('http_req_failed{scenario:slo}').values;
  const requests = metric('http_reqs').values;
  const dropped = metric('dropped_iterations').values;
  const upstream = metric('hackernews_upstream_requests').values;
  const format = (value, digits = 1) => (value === undefined ? 'n/a' : Number(value).toFixed(digits));

  const rows = [
    ['p95 latency', `< ${SLO.p95Ms} ms`, `${format(duration['p(95)'])} ms`, passed('http_req_duration{scenario:slo}')],
    ['p99 latency', `< ${SLO.p99Ms} ms`, `${format(duration['p(99)'])} ms`, passed('http_req_duration{scenario:slo}')],
    ['Error rate', `< ${SLO.maxErrorRate * 100}%`, `${format((failed.rate || 0) * 100, 3)}%`, passed('http_req_failed{scenario:slo}')],
    ['Throughput', `${SLO.rps} req/s`, `${format(requests.rate)} req/s (dropped: ${dropped.count || 0})`, passed('dropped_iterations')],
    ['Hacker News requests', `≤ ${maxUpstreamRequests}`, `${upstream.count ?? 'n/a'}`, passed('hackernews_upstream_requests')],
  ];

  const allPassed = rows.every((row) => row[3]) && passed('checks{scenario:slo}');

  return [
    `## ${icon(allPassed)} Load test – SLO ${allPassed ? 'met' : 'NOT met'}`,
    '',
    `Constant arrival rate of **${RATE} req/s** for **${DURATION}** against \`${ENDPOINT}\`.`,
    '',
    '| Objective | Target | Result | Status |',
    '|---|---|---|---|',
    ...rows.map(([name, target, result, ok]) => `| ${name} | ${target} | ${result} | ${icon(ok)} |`),
    '',
    `Latency (ms): avg ${format(duration.avg)} · med ${format(duration.med)} · p90 ${format(duration['p(90)'])} · max ${format(duration.max)}`,
    '',
  ].join('\n');
}

function parseDuration(value) {
  const match = /^(\d+)(s|m|h)$/.exec(value);
  if (!match) {
    throw new Error(`Unsupported DURATION '${value}'. Use e.g. 90s, 3m or 1h.`);
  }
  return Number(match[1]) * { s: 1, m: 60, h: 3600 }[match[2]];
}
