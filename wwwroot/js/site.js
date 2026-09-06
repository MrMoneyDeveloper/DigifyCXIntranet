// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
(() => {
    const visibleInputIcon = `
        <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M13.875 18.825A10.05 10.05 0 0112 19c-4.477 0-8.268-2.943-9.542-7a9.956 9.956 0 012.293-3.95M6.228 6.228A9.956 9.956 0 0112 5c4.477 0 8.268 2.943 9.542 7a9.956 9.956 0 01-1.772 3.274M6.228 6.228L3 3m3.228 3.228l3.65 3.65M17.772 15.272l3.228 3.228m-3.228-3.228l-3.65-3.65" />
        </svg>`;
    const hiddenInputIcon = `
        <svg xmlns="http://www.w3.org/2000/svg" class="h-5 w-5" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
            <path stroke-linecap="round" stroke-linejoin="round" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.477 0 8.268 2.943 9.542 7-1.274 4.057-5.065 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
        </svg>`;

    document.querySelectorAll("[data-password-toggle]").forEach(button => {
        const input = document.getElementById(button.dataset.passwordToggle);
        if (!input) return;

        button.setAttribute("aria-controls", input.id);
        button.querySelector("svg")?.setAttribute("aria-hidden", "true");
        button.addEventListener("click", () => {
            const isHidden = input.type === "password";
            input.type = isHidden ? "text" : "password";
            button.setAttribute("aria-label", isHidden ? "Hide password" : "Show password");
            button.innerHTML = isHidden ? visibleInputIcon : hiddenInputIcon;
            button.querySelector("svg")?.setAttribute("aria-hidden", "true");
        });
    });

    // Return keyboard and screen-reader users to server-side submission errors.
    const errorSummary = document.querySelector(".validation-summary-errors, [data-validation-summary]");
    if (errorSummary?.textContent.trim()) {
        const details = errorSummary.closest("details");
        if (details) details.open = true;
        errorSummary.setAttribute("tabindex", "-1");
        errorSummary.focus();
    }
})();
