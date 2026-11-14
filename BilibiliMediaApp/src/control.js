(function () {
  "use strict";

  var ACTION = "__ACTION__";
  var STEP = __STEP__;
  var MAIN_SELECTORS = [
    ".bpx-player-video-wrap video",
    ".bpx-player-container video",
    "#bilibili-player video",
    ".bilibili-player-video video",
    ".bilibili-player video"
  ];

  function area(video) {
    try {
      var rect = video.getBoundingClientRect();
      if (!rect) return 0;
      return Math.max(0, rect.width) * Math.max(0, rect.height);
    } catch (e) {
      return 0;
    }
  }

  function visible(video) {
    try {
      var style = window.getComputedStyle(video);
      if (!style || style.display === "none" || style.visibility === "hidden") return false;
      return area(video) >= 120 * 68;
    } catch (e) {
      return false;
    }
  }

  function score(video) {
    var value = area(video);
    if (!video.paused) value += 1e12;
    if (isFinite(video.duration) && video.duration > 60) value += 5e11;
    return value;
  }

  function best(videos) {
    if (!videos || !videos.length) return null;
    var pool = [];
    var i;
    for (i = 0; i < videos.length; i++) {
      if (visible(videos[i])) pool.push(videos[i]);
    }
    if (!pool.length) pool = videos;

    var winner = pool[0];
    var winnerScore = score(winner);
    for (i = 1; i < pool.length; i++) {
      var currentScore = score(pool[i]);
      if (currentScore > winnerScore) {
        winner = pool[i];
        winnerScore = currentScore;
      }
    }
    return winner;
  }

  function findVideo() {
    var i;
    var j;
    for (i = 0; i < MAIN_SELECTORS.length; i++) {
      var list = [];
      try {
        var nodes = document.querySelectorAll(MAIN_SELECTORS[i]);
        for (j = 0; j < nodes.length; j++) {
          if (nodes[j] && nodes[j].tagName === "VIDEO") list.push(nodes[j]);
        }
      } catch (e) {
        list = [];
      }
      if (list.length) return best(list);
    }

    var all = [];
    try {
      var videos = document.querySelectorAll("video");
      for (j = 0; j < videos.length; j++) {
        if (videos[j] && videos[j].tagName === "VIDEO") all.push(videos[j]);
      }
    } catch (e) {
      all = [];
    }
    return best(all);
  }

  function state(video) {
    var duration = (isFinite(video.duration) && video.duration > 0) ? video.duration : null;
    return {
      ok: true,
      paused: !!video.paused,
      ended: !!video.ended,
      currentTime: isFinite(video.currentTime) ? video.currentTime : 0,
      duration: duration,
      title: (document.title || "").replace(/\s+/g, " ").trim(),
      url: location.href
    };
  }

  var video = findVideo();
  if (!video) {
    return { ok: false, reason: "no-video", title: document.title || "" };
  }

  try {
    if (ACTION === "toggle") {
      var wasPaused = !!(video.paused || video.ended);
      if (wasPaused) {
        var playing = video.play();
        if (playing && typeof playing.catch === "function") {
          playing.catch(function () {});
        }
      } else {
        video.pause();
      }
      var toggleResult = state(video);
      toggleResult.wasPaused = wasPaused;
      return toggleResult;
    }

    var current = isFinite(video.currentTime) ? video.currentTime : 0;
    var target = current + (ACTION === "back" ? -STEP : STEP);
    if (target < 0) target = 0;
    if (isFinite(video.duration) && video.duration > 0) {
      target = Math.min(target, Math.max(0, video.duration - 0.05));
    }
    video.currentTime = target;
    var result = state(video);
    result.targetTime = target;
    return result;
  } catch (e) {
    return { ok: false, reason: "error", error: String(e) };
  }
})();
