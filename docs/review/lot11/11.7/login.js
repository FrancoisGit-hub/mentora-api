// OTP login helper for the 11.7 runtime proofs. Reads EMAIL and OTP from the environment (set in the shell,
// never written anywhere), saves the access token to the gitignored tokens.json, prints only the status.
// Usage: node login.js request | EMAIL=... OTP=... node login.js verify
const fs = require('fs');
const path = require('path');
const BASE = 'http://localhost:5243/api/v1/auth/otp';

(async () => {
  const step = process.argv[2];
  const email = process.env.EMAIL;
  if (step === 'request') {
    const r = await fetch(`${BASE}/request`, { method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, isCoach: true }) });
    console.log('otp/request status', r.status);
  } else if (step === 'verify') {
    const r = await fetch(`${BASE}/verify`, { method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, code: process.env.OTP, isCoach: true }) });
    const j = await r.json().catch(() => null);
    const token = j && j.data && (j.data.accessToken ?? j.data.AccessToken ?? j.data.token);
    if (r.status === 200 && token) {
      fs.writeFileSync(path.join(__dirname, 'tokens.json'), token);
      console.log('otp/verify status 200, token saved to tokens.json (not printed), data keys:', Object.keys(j.data).join(','));
    } else {
      console.log('otp/verify status', r.status, 'no token saved, error:', j && j.error);
    }
  }
})();
