# Lot 10.2 H21 rate limit - executed 2026-09-30T13:00:46Z
API restarted with Quiz__AnonymousPermitLimit=5
- anonymous call 1 (device l102-c10b36001b819ccd) -> **201**
- anonymous call 2 (device l102-30d12bfe510a7df8) -> **201**
- anonymous call 3 (device l102-c206e42305c3ba5e) -> **201**
- anonymous call 4 (device l102-c494415eb1e610d5) -> **201**
- anonymous call 5 (device l102-535e617e191d57d5) -> **201**
- anonymous call 6 (device l102-b74118249e678119) -> **429** body=`{"success": false, "data": null, "error": "Too many requests.", "statusCode": 429}` Retry-After=`600`
  - check **5 pass then 429**: OK 
- 7th anonymous call -> 429
  - check **still limited**: OK 
- **H21 coach call right after** `POST /api/v1/quiz/basic/sessions` (JWT) -> **201** (expected 201) OK; EF queries: 2
  - body: `{"sessionId": "bfca4cbf-75e7-4794-a26c-d1fb51223599"}`
  - check **coach call not 429**: OK 
- **H21 anonymous with X-Forwarded-For 203.0.113.9 from loopback (different partition)** `POST /api/v1/quiz/basic/sessions` (no Authorization, X-Device-Id=l102-afe0e7b4c5835297) -> **201** (expected 201) OK; EF queries: 2
  - body: `{"sessionId": "2be3ea0f-c1fa-40d1-b8a1-4c4a337b293c"}`

## Result: ALL CHECKS OK
