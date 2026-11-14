"use strict";

const BILI_URL_PATTERNS = [
  "*://*.bilibili.com/*",
  "*://bilibili.com/*"
];

const COMMAND_ACTIONS = {
  "toggle-play": "toggle",
  "seek-backward": "back",
  "seek-forward": "forward"
};

const DEFAULT_SETTINGS = {
  seekStep: 3,
  showToast: true
};

const CONTENT_FILES = [
  "content/player-core.js",
  "content/content.js"
];

function normalizeStep(value) {
  const number = Number(value);
  if (!Number.isFinite(number)) return DEFAULT_SETTINGS.seekStep;
  return Math.min(30, Math.max(1, Math.round(number)));
}

async function getSettings() {
  const stored = await chrome.storage.local.get(DEFAULT_SETTINGS);
  return {
    seekStep: normalizeStep(stored.seekStep),
    showToast: stored.showToast !== false
  };
}

function rankTab(tab, lastTabId) {
  const lastAccessed = Number(tab.lastAccessed) || 0;
  // The active tab reflects what the user last chose in the browser. The
  // remembered tab is the fallback when that active page has no video.
  if (tab.active) return 3000000000000 + lastAccessed;
  if (tab.id === lastTabId) return 2000000000000 + lastAccessed;
  return lastAccessed;
}

async function getCandidateTabs() {
  const tabs = await chrome.tabs.query({ url: BILI_URL_PATTERNS });
  if (!tabs.length) return [];

  const stored = await chrome.storage.local.get("lastTabId");
  const lastTabId = Number(stored.lastTabId) || null;

  return tabs.slice().sort((a, b) => rankTab(b, lastTabId) - rankTab(a, lastTabId));
}

function isMissingReceiver(error) {
  const message = String(error && error.message ? error.message : error);
  return (
    message.includes("Receiving end does not exist") ||
    message.includes("Could not establish connection") ||
    message.includes("The message port closed")
  );
}

async function sendToTab(tabId, message) {
  try {
    return await chrome.tabs.sendMessage(tabId, message);
  } catch (error) {
    if (!isMissingReceiver(error)) return null;
  }

  // The tab may have been open before the extension was installed, or the
  // content script may have been torn down by a navigation. Inject on demand.
  try {
    for (const file of CONTENT_FILES) {
      await chrome.scripting.executeScript({
        target: { tabId },
        files: [file]
      });
    }
    return await chrome.tabs.sendMessage(tabId, message);
  } catch (_) {
    return null;
  }
}

let badgeTimer = null;

async function flashBadge(text, color) {
  try {
    if (badgeTimer) clearTimeout(badgeTimer);
    await chrome.action.setBadgeBackgroundColor({ color });
    await chrome.action.setBadgeText({ text });
    badgeTimer = setTimeout(() => {
      chrome.action.setBadgeText({ text: "" }).catch(() => {});
      badgeTimer = null;
    }, 1800);
  } catch (_) {
    // Badge feedback is optional; never let it break a command.
  }
}

async function dispatchAction(action) {
  if (!["toggle", "back", "forward"].includes(action)) {
    return { ok: false, reason: "bad-action" };
  }

  let tabs;
  try {
    tabs = await getCandidateTabs();
  } catch (error) {
    return { ok: false, reason: "tabs-error", error: String(error) };
  }

  if (!tabs.length) {
    await flashBadge("无", "#c0392b");
    return { ok: false, reason: "no-tab" };
  }

  const settings = await getSettings();
  let lastFailure = "no-video";

  for (const tab of tabs) {
    const result = await sendToTab(tab.id, {
      type: "BILI_MEDIA_COMMAND",
      action,
      step: settings.seekStep,
      showToast: settings.showToast
    });

    if (result && result.ok) {
      await chrome.storage.local.set({ lastTabId: tab.id });
      return result;
    }

    if (result && result.reason === "play-blocked") {
      lastFailure = "play-blocked";
      break;
    }
  }

  await flashBadge("!", "#c0392b");
  return { ok: false, reason: lastFailure };
}

async function getStatus() {
  let tabs;
  try {
    tabs = await getCandidateTabs();
  } catch (error) {
    return { ok: false, reason: "tabs-error", error: String(error) };
  }

  if (!tabs.length) {
    return { ok: false, reason: "no-tab", tabCount: 0 };
  }

  for (const tab of tabs) {
    const result = await sendToTab(tab.id, { type: "BILI_MEDIA_GET_STATE" });
    if (result && result.ok) {
      return {
        ok: true,
        tabId: tab.id,
        tabTitle: tab.title || "",
        state: result.state
      };
    }
  }

  return { ok: false, reason: "no-video", tabCount: tabs.length };
}

chrome.runtime.onInstalled.addListener(async () => {
  try {
    const stored = await chrome.storage.local.get(DEFAULT_SETTINGS);
    await chrome.storage.local.set({
      seekStep: normalizeStep(stored.seekStep),
      showToast: stored.showToast !== false
    });
  } catch (_) {
    // Settings will fall back to defaults at read time.
  }
});

chrome.commands.onCommand.addListener((command) => {
  const action = COMMAND_ACTIONS[command];
  if (!action) return;
  dispatchAction(action).catch(() => {});
});

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (!message || typeof message !== "object") return;
  if (sender && sender.id && sender.id !== chrome.runtime.id) return;

  if (message.type === "BILI_MEDIA_POPUP_COMMAND") {
    dispatchAction(message.action)
      .then(sendResponse)
      .catch((error) => sendResponse({ ok: false, reason: "error", error: String(error) }));
    return true;
  }

  if (message.type === "BILI_MEDIA_GET_STATUS") {
    getStatus()
      .then(sendResponse)
      .catch((error) => sendResponse({ ok: false, reason: "error", error: String(error) }));
    return true;
  }

  return undefined;
});
