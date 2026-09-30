\# Lot 10.3 - VPS evidence (api-demo)



Date: 2026-09-30. Image built from feature/lot\_10 at cf7b996 (docs excluded by .dockerignore). Outputs checked by François during the run; raw outputs not archived (they may contain OTP codes).



\## Deployment

\- Backup before deployment: \~/backup-20260930-1616.dump (287K).

\- Last migration before deployment: 20260929171450\_Lot9\_ExerciseCatalog.

\- Startup logs: migration 20260930085059\_Lot10\_Quiz applied; "QuizQuestionSeeder: created 900, existing 0, filled 0, skipped 74, warnings 74."; Application started; no fail:, crit: or Exception line.

\- SQL counts after startup: QUIZ\_QUESTIONS 900, QUIZ\_SESSIONS 0, QUIZ\_DEVICE\_CLAIMS 0.

\- GET /api/v1/health: 200. POST /api/v1/coach/quiz/basic/claim-device without token: 401 (route deployed).



\## Quiz flow in device mode (device l103-a1b2c3d4e5f60718)

\- POST sessions: 201, 12 questions, expiresAt = creation + 2 h.

\- POST answers with the correct answers: 200, correctCount 12/12, total = score.

\- GET me: totalPoints = score, completedCount 1.



\## Rate limit with the real client IP

\- From the workstation: 27 further anonymous calls passed, the next one returned 429 with Retry-After (limit 30 per 10 min, 3 calls already used).

\- Right after, from the VPS itself (another client IP): 200.

\- Conclusion: behind nginx the API partitions by the real client IP (X-Forwarded-For applied, ForwardedHeaders unchanged).



\## Fixture cleanup

\- Dry run ending with ROLLBACK: 1 fixture session, DELETE 1 (12 session questions by cascade), then 0 sessions, 0 session questions, 0 claims, 900 questions.

\- COMMIT file identical except its last line (diff checked before execution), executed with the same counts; afterwards 0 session with a device id starting with l103-.

