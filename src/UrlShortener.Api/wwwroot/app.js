// Page logic. Rules: data from the API is always shown with textContent (never innerHTML), so it can't inject HTML.
"use strict";

document.addEventListener("DOMContentLoaded", () => {
  const form = document.getElementById("shorten-form");
  const input = document.getElementById("url");
  const shortenButton = document.getElementById("shorten");
  const errorBox = document.getElementById("error");
  const result = document.getElementById("result");
  const shortUrl = document.getElementById("short-url");
  const targetUrl = document.getElementById("target-url");
  const clicks = document.getElementById("clicks");
  const copyButton = document.getElementById("copy");
  const refreshButton = document.getElementById("refresh");

  let currentCode = null;

  function showError(message) {
    errorBox.textContent = message;
    errorBox.hidden = false;
  }

  function clearError() {
    errorBox.textContent = "";
    errorBox.hidden = true;
  }

  function showLink(link) {
    currentCode = link.code;
    shortUrl.textContent = link.shortUrl;
    shortUrl.href = link.shortUrl;
    targetUrl.textContent = link.targetUrl;
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
        body: JSON.stringify({ url: input.value }),
      });

      if (!response.ok) {
        result.hidden = true;
        showError(await readError(response));
        return;
      }

      showLink(await response.json());
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
    } catch {
      showError("Could not reach the server. Please try again.");
    }
  });
});
