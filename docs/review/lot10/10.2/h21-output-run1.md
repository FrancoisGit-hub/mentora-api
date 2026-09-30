# Lot 10.2 H21 rate limit - executed 2026-09-30T12:09:49Z
API restarted with Quiz__AnonymousPermitLimit=5
- anonymous call 1 (device l102-000ffb203fbb0070) -> **201**
- anonymous call 2 (device l102-535251ab473e18de) -> **201**
- anonymous call 3 (device l102-5d6e4d194011674d) -> **201**
- anonymous call 4 (device l102-a3b04c0d90632e86) -> **201**
- anonymous call 5 (device l102-34ae293c1d3a828f) -> **201**
- anonymous call 6 (device l102-8097a2f757eb4a6c) -> **429** body=`{"success": false, "data": null, "error": "Too many requests.", "statusCode": 429}` Retry-After=`600`
  - check **5 pass then 429**: OK 
- 7th anonymous call -> 429
  - check **still limited**: OK 
- **H21 coach call right after** `POST /api/v1/quiz/basic/sessions` (JWT) -> **201** (expected 201) OK; EF queries: 2
  - body: `{"sessionId": "d47ae164-dcae-4b2d-8c27-36e244606349"}`
  - check **coach call not 429**: OK 
- **H21 anonymous with X-Forwarded-For 203.0.113.9 from loopback (different partition)** `POST /api/v1/quiz/basic/sessions` (no Authorization, X-Device-Id=l102-a3dc6e5b75e231e5) -> **201** (expected 201) OK; EF queries: 2
  - body: `{"sessionId": "ac50551a-b4e1-4d4d-bdd8-95a5160cd942"}`

## Result: ALL CHECKS OK
