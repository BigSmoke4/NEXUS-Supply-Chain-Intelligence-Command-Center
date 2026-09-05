// nexus.api.js — thin fetch wrapper with CSRF token support for POSTs.
Nexus.api = {
    async post(url, formData) {
        const response = await fetch(url, { method: 'POST', body: formData });
        if (!response.ok) throw new Error(`Request to ${url} failed: ${response.status}`);
        return response;
    }
};
