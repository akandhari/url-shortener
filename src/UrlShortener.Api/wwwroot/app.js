// Page logic. Rules: data from the API is always shown with textContent (never innerHTML), so it can't inject HTML.
"use strict";

document.addEventListener("DOMContentLoaded", () => {
  const form = document.getElementById("shorten-form");
  const input = document.getElementById("url");
  const aliasInput = document.getElementById("alias");
  const expiresSelect = document.getElementById("expires");
  const statusText = document.getElementById("status");
  const shortenButton = document.getElementById("shorten");
  const errorBox = document.getElementById("error");
  const result = document.getElementById("result");
  const shortUrl = document.getElementById("short-url");
  const targetUrl = document.getElementById("target-url");
  const clicks = document.getElementById("clicks");
  const copyButton = document.getElementById("copy");
  const refreshButton = document.getElementById("refresh");
  const perDayList = document.getElementById("per-day");
  const referrerList = document.getElementById("referrers");

  let currentCode = null;

  function showError(message) {
    errorBox.textContent = message;
    errorBox.hidden = false;
  }

  function clearError() {
    errorBox.textContent = "";
    errorBox.hidden = true;
  }

  // Builds list items with createElement + textContent only: API data is never parsed as HTML.
  function fillList(list, rows, emptyText) {
    list.replaceChildren();
    if (rows.length === 0) {
      const item = document.createElement("li");
      item.className = "empty";
      item.textContent = emptyText;
      list.append(item);
      return;
    }

    for (const [label, value] of rows) {
      const item = document.createElement("li");
      const name = document.createElement("span");
      const count = document.createElement("span");
      name.textContent = label;
      count.textContent = String(value);
      item.append(name, count);
      list.append(item);
    }
  }

  async function loadStats(code) {
    const response = await fetch(`/api/links/${encodeURIComponent(code)}/stats`);
    if (!response.ok) {
      showError(await readError(response));
      return;
    }

    const stats = await response.json();
    fillList(perDayList, stats.clicksPerDay.slice().reverse().map(d => [d.date, d.clicks]), "No clicks yet");
    fillList(referrerList, stats.topReferrers.map(r => [r.host, r.clicks]), "No clicks yet");
  }

  // Optional fields are only sent when chosen: an empty alias means "random code", "Never" means no expiry.
  function buildRequest() {
    const request = { url: input.value };
    if (aliasInput.value.trim()) {
      request.alias = aliasInput.value;
    }
    if (expiresSelect.value) {
      request.expiresAt = new Date(Date.now() + Number(expiresSelect.value) * 24 * 60 * 60 * 1000).toISOString();
    }
    return request;
  }

  function describeStatus(link) {
    if (link.status === "disabled") {
      return "disabled";
    }
    if (link.status === "expired") {
      return "expired";
    }
    return link.expiresAt ? `active, expires ${new Date(link.expiresAt).toLocaleString()}` : "active, never expires";
  }

  function showLink(link) {
    currentCode = link.code;
    shortUrl.textContent = link.shortUrl;
    shortUrl.href = link.shortUrl;
    targetUrl.textContent = link.targetUrl;
    statusText.textContent = describeStatus(link);
    clicks.textContent = String(link.clickCount);
    result.hidden = false;
  }

  // Errors from the API are ProblemDetails: prefer the specific "detail", then the "title".
  async function readError(response) {
    try {
      const problem = await response.json();
      return problem.detail || problem.title || `Request failed (${response.status}).`;
    } catch {
      return `Request failed (${response.status}).`;
    }
  }

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    clearError();
    shortenButton.disabled = true;   // no double submits while the request is in flight

    try {
      const response = await fetch("/api/links", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(buildRequest()),
      });

      if (!response.ok) {
        result.hidden = true;
        showError(await readError(response));
        return;
      }

      const link = await response.json();
      showLink(link);
      await loadStats(link.code);
    } catch {
      showError("Could not reach the server. Please try again.");
    } finally {
      shortenButton.disabled = false;
    }
  });

  copyButton.addEventListener("click", async () => {
    try {
      await navigator.clipboard.writeText(shortUrl.href);
      copyButton.textContent = "Copied";
      setTimeout(() => { copyButton.textContent = "Copy"; }, 1500);
    } catch {
      showError("Copy failed. Select the link and copy it manually.");
    }
  });

  refreshButton.addEventListener("click", async () => {
    if (!currentCode) {
      return;
    }

    clearError();
    try {
      const response = await fetch(`/api/links/${encodeURIComponent(currentCode)}`);
      if (!response.ok) {
        showError(await readError(response));
        return;
      }

      showLink(await response.json());
      await loadStats(currentCode);
    } catch {
      showError("Could not reach the server. Please try again.");
    }
  });
});
