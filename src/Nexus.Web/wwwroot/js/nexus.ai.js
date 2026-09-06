// nexus.ai.js — triggers the Explanation Agent and renders the returned partial.
document.addEventListener('DOMContentLoaded', () => {
    const btn = document.getElementById('explain-btn');
    if (!btn) return;

    btn.addEventListener('click', async () => {
        btn.disabled = true;
        btn.textContent = 'Investigating…';
        try {
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            const questionInput = document.getElementById('ai-question-input');
            const formData = new FormData();
            if (token) formData.append('__RequestVerificationToken', token);
            if (questionInput && questionInput.value.trim()) formData.append('question', questionInput.value.trim());

            const response = await Nexus.api.post(btn.dataset.explainUrl, formData);
            const html = await response.text();
            document.getElementById('ai-explanation-container').innerHTML = html;
        } catch (err) {
            Nexus.core.toast('AI explanation failed — see console.');
            console.error(err);
        } finally {
            btn.disabled = false;
            btn.textContent = 'Ask the Explanation Agent';
        }
    });
});
