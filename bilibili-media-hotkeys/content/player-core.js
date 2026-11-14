"use strict";

(function () {
  const MAIN_VIDEO_SELECTORS = [
    ".bpx-player-video-wrap video",
    ".bpx-player-container video",
    "#bilibili-player video",
    ".bilibili-player-video video",
    ".bilibili-player video"
  ];

  function isVideoElement(value) {
    return !!value && value.tagName === "VIDEO";
  }

  function rectArea(video) {
    try {
      const rect = video.getBoundingClientRect();
      if (!rect) return 0;
      return Math.max(0, Number(rect.width) || 0) * Math.max(0, Number(rect.height) || 0);
    } catch (_) {
      return 0;
    }
  }

  function isVisible(video) {
    try {
      const style = window.getComputedStyle(video);
      if (!style || style.display === "none" || style.visibility === "hidden") {
        return false;
      }
      const area = rectArea(video);
      return area >= 120 * 68;
    } catch (_) {
      return false;
    }
  }

  function scoreVideo(video) {
    let score = rectArea(video);
    if (!video.paused) score += 1e12;
    if (Number.isFinite(video.duration) && video.duration > 60) score += 5e11;
    return score;
  }

  function pickBest(videos) {
    if (!videos.length) return null;
    const visible = videos.filter(isVisible);
    const pool = visible.length ? visible : videos;

    let best = pool[0];
    let bestScore = scoreVideo(best);
    for (let index = 1; index < pool.length; index += 1) {
      const candidate = pool[index];
      const candidateScore = scoreVideo(candidate);
      if (candidateScore > bestScore) {
        best = candidate;
        bestScore = candidateScore;
      }
    }
    return best;
  }

  function findVideo() {
    // Prefer the known Bilibili player containers. This avoids accidentally
    // controlling a small hover-preview video elsewhere on the page.
    for (const selector of MAIN_VIDEO_SELECTORS) {
      let videos = [];
      try {
        videos = Array.from(document.querySelectorAll(selector)).filter(isVideoElement);
      } catch (_) {
        videos = [];
      }
      if (videos.length) return pickBest(videos);
    }

    try {
      return pickBest(Array.from(document.querySelectorAll("video")).filter(isVideoElement));
    } catch (_) {
      return null;
    }
  }

  function getState(video) {
    const duration =
      Number.isFinite(video.duration) && video.duration > 0 ? video.duration : null;
    return {
      hasVideo: true,
      paused: !!video.paused,
      ended: !!video.ended,
      currentTime: Number.isFinite(video.currentTime) ? video.currentTime : 0,
      duration,
      playbackRate: Number.isFinite(video.playbackRate) ? video.playbackRate : 1,
      title: (document.title || "").trim(),
      url: location.href
    };
  }

  async function toggle(video) {
    if (!video) return { ok: false, reason: "no-video" };

    try {
      if (video.paused || video.ended) {
        const playing = video.play();
        if (playing && typeof playing.then === "function") await playing;
      } else {
        video.pause();
      }
      return { ok: true, state: getState(video) };
    } catch (error) {
      return {
        ok: false,
        reason: "play-blocked",
        error: String(error && error.message ? error.message : error)
      };
    }
  }

  async function seekBy(video, seconds) {
    if (!video) return { ok: false, reason: "no-video" };

    const delta = Number(seconds);
    if (!Number.isFinite(delta) || delta === 0) {
      return { ok: false, reason: "bad-step" };
    }

    const duration =
      Number.isFinite(video.duration) && video.duration > 0 ? video.duration : null;
    const current = Number.isFinite(video.currentTime) ? video.currentTime : 0;
    let target = current + delta;

    if (target < 0) target = 0;
    if (duration !== null) {
      target = Math.min(target, Math.max(0, duration - 0.05));
    }

    try {
      video.currentTime = target;
    } catch (error) {
      return {
        ok: false,
        reason: "seek-failed",
        error: String(error && error.message ? error.message : error)
      };
    }

    return {
      ok: true,
      targetTime: target,
      state: getState(video)
    };
  }

  globalThis.BiliMediaCore = {
    version: "1.0.0",
    findVideo,
    getState,
    toggle,
    seekBy
  };
})();
