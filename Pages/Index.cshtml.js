(() => {
    const techNewsList = document.getElementById("technews-list");
    if (!techNewsList) return;

    const loading = document.getElementById("technews-loading");
    const error = document.getElementById("technews-error");
    const updated = document.getElementById("technews-updated");

    fetch("/api/technews", { method: "GET", credentials: "same-origin" })
        .then(response => {
            if (!response.ok) throw new Error("Feed failed");
            return response.json();
        })
        .then(data => {
            loading?.classList.add("d-none");
            if (data.lastUpdatedUtc && updated) {
                updated.textContent = `Updated ${new Date(data.lastUpdatedUtc).toLocaleString()}`;
            }

            const items = (data.items || []).slice(0, 5);
            if (items.length === 0) {
                const empty = document.createElement("p");
                empty.className = "dcx-feed-state";
                empty.textContent = "No tech news available right now.";
                techNewsList.appendChild(empty);
                return;
            }

            items.forEach(item => {
                const link = item.url && item.url.length > 0
                    ? item.url
                    : `https://news.ycombinator.com/item?id=${item.id}`;
                const card = document.createElement("a");
                card.href = link;
                card.target = "_blank";
                card.rel = "noreferrer noopener";
                card.className = "tech-feed-item";

                const title = document.createElement("span");
                title.className = "tech-feed-title";
                title.textContent = item.title || "Untitled";

                const by = document.createElement("small");
                by.className = "tech-feed-meta";
                by.textContent = `by ${item.by || "unknown"}`;

                card.appendChild(title);
                card.appendChild(by);
                techNewsList.appendChild(card);
            });
        })
        .catch(() => {
            loading?.classList.add("d-none");
            error?.classList.remove("d-none");
        });
})();
