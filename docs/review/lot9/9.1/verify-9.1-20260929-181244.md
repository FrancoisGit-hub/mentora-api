# Lot 9.1 verification - 20260929-181244

Base URL: http://localhost:5243

| # | Scenario | Method | Path | Expected | Actual | Status | Details |
|---|---|---|---|---|---|---|---|
| V1 | Coach A grants qty 3 on PA1 to M1 | POST | /api/v1/coach/members/38b05ed2-e577-4cf7-81e9-365eaafab626/voucher-grants | 201 | 201 | PASS | voucherCount=3 purchaseId=ee57a8c5-998a-4147-bdf8-dd2f17e6ccce origin=COACH_GRANT |
| V3 | Coach B grants to M1 (not attached to B) | POST | /api/v1/coach/members/38b05ed2-e577-4cf7-81e9-365eaafab626/voucher-grants | 404 | 404 | PASS | Member 38b05ed2-e577-4cf7-81e9-365eaafab626 not found. |
| V4 | Coach A grants on coach B's product | POST | /api/v1/coach/members/38b05ed2-e577-4cf7-81e9-365eaafab626/voucher-grants | 404 | 404 | PASS | Product c8821d7f-d9d5-4f25-a666-fcee2562a0cf not found. |
| V5 | Coach A grants to M2 (not attached to A) | POST | /api/v1/coach/members/ef9296e7-1f7b-47ec-b97e-7dbf7c5d2495/voucher-grants | 404 | 404 | PASS | Member ef9296e7-1f7b-47ec-b97e-7dbf7c5d2495 not found. |
| V6a | quantity=0 -> 422 with errors | POST | /api/v1/coach/members/38b05ed2-e577-4cf7-81e9-365eaafab626/voucher-grants | 422 | 422 | PASS | errors present=True : {"Quantity":["\u0027Quantity\u0027 doit être entre 1 et 50. Vous avez saisi 0."]} |
| V6b | quantity=51 -> 422 with errors | POST | /api/v1/coach/members/38b05ed2-e577-4cf7-81e9-365eaafab626/voucher-grants | 422 | 422 | PASS | errors present=True : {"Quantity":["\u0027Quantity\u0027 doit être entre 1 et 50. Vous avez saisi 51."]} |
| V7 | Ineligible product type (VISIO_GROUPE fixture) | POST | /api/v1/coach/members/38b05ed2-e577-4cf7-81e9-365eaafab626/voucher-grants | 409 | 409 | PASS | details.reason=PRODUCT_NOT_ELIGIBLE |
| V8a | Member token on grant route (CoachOnly policy) | POST | /api/v1/coach/members/38b05ed2-e577-4cf7-81e9-365eaafab626/voucher-grants | 403 | 403 | PASS |  |
| V8b | No Authorization header on grant route | POST | /api/v1/coach/members/38b05ed2-e577-4cf7-81e9-365eaafab626/voucher-grants | 401 | 401 | PASS |  |
| V10a | M1 adds PA3 (qty 2) to cart | POST | /api/v1/member/coaches/e268a512-84d6-4e27-b5c0-1d6c08a47190/cart/items | 200 | 200 | PASS |  |
| V10b | M1 checks out cart | POST | /api/v1/member/coaches/e268a512-84d6-4e27-b5c0-1d6c08a47190/cart/checkout | 200 | 200 | PASS | orderId=b39c64bb-81f7-4303-86bf-e18d8dbc5a28 |
| V10c | Simulate checkout.session.completed for M1's order | POST | /api/v1/internal/stripe/simulate-webhook | 200 | 200 | PASS | status=PROCESSED |
| V11a | GET coach/programs status | GET | /api/v1/coach/programs | 200 | 200 | PASS |  |
| V11b | GET coach/product-programs status | GET | /api/v1/coach/product-programs | 200 | 200 | PASS |  |
| V11c | Same status code, both routes | GET | (compare) | 200 | 200 | PASS |  |
| V11d | Same payload, both routes | GET | (compare) | True | True | PASS |  |
| V11e | POST product-programs status | POST | /api/v1/coach/product-programs | 201 | 201 | PASS | Location header= (endpoint sets none - StatusCode(201,...) with no CreatedAtAction, confirmed in inventory 2.2) |
| V11f | Swagger: /api/v1/coach/programs GET deprecated=true | GET | /swagger/coach/swagger.json | True | True | PASS |  |
| V11g | Swagger: /api/v1/coach/product-programs GET deprecated absent/false | GET | /swagger/coach/swagger.json | True | True | PASS |  |

