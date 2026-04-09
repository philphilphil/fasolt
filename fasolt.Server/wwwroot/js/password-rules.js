// Live password-rules evaluator used by /oauth/register and
// /oauth/reset-password. Extracted from the inline <script> blocks in
// the old OAuthRegisterPage.cs and OAuthResetPasswordPage.cs so the
// pages can enforce CSP script-src 'self' without needing 'unsafe-inline'.
// Pure DOM, no dependencies.
//
// Keep the rules in sync with the password policy configured in
// Program.cs (IdentityOptions.Password). Drift here means the
// client-side checklist lies to the user.
(function () {
  const pwd = document.getElementById('password');
  const confirm = document.getElementById('confirmPassword');
  const rules = document.getElementById('rules');
  const mismatch = document.getElementById('mismatch');

  if (!pwd || !rules) return; // page doesn't use the password rules

  function evaluate() {
    const v = pwd.value;
    const checks = {
      length: v.length >= 8,
      upper: /[A-Z]/.test(v),
      lower: /[a-z]/.test(v),
      digit: /[0-9]/.test(v),
    };
    for (const li of rules.children) {
      const r = li.dataset.rule;
      li.className = checks[r] ? 'ok' : 'pending';
    }
    if (mismatch && confirm) {
      mismatch.style.display = (confirm.value && confirm.value !== v) ? 'block' : 'none';
    }
  }

  pwd.addEventListener('input', evaluate);
  if (confirm) confirm.addEventListener('input', evaluate);
})();
