// Load test for the hot path (GET /{code}). Run with Docker:
//   docker run --rm -i -e BASE_URL=http://host.docker.internal:8089 grafana/k6 run - < perf/redirect-load.js
// Start the app with a high redirect rate limit first: all requests come from one IP, so the default (300/min) would
// answer most of them with 429. That limit is part of the abuse protection, not of the redirect's cost.
import http from 'k6/http';
import { check } from 'k6';

const BASE = __ENV.BASE_URL || 'http://localhost:8080';

export const options = {
  scenarios: {
    redirects: { executor: 'constant-vus', vus: 50, duration: '30s' },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],          // under 1% errors
    'http_req_duration{kind:redirect}': ['p(95)<50'],   // p95 under 50 ms on a laptop
  },
};

export function setup() {
  const res = http.post(`${BASE}/api/links`, JSON.stringify({ url: 'https://example.com/load-test' }),
    { headers: { 'Content-Type': 'application/json' } });
  check(res, { 'link created': (r) => r.status === 201 });
  return { code: res.json('code') };
}

export default function (data) {
  const res = http.get(`${BASE}/${data.code}`, {
    redirects: 0,
    headers: { Referer: 'https://load.example.com/' },
    tags: { kind: 'redirect' },
  });
  check(res, { 'is 302': (r) => r.status === 302 });
}

export function teardown(data) {
  // Give the background writer a moment, then report how many clicks were stored.
  const details = http.get(`${BASE}/api/links/${data.code}`);
  console.log(`clickCount after the run: ${details.json('clickCount')}`);
}
