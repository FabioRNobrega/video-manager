let fillTabEscapeHandler;

export function measureAndCapture(viewport, video, pointerId) {
    if (video.setPointerCapture) {
        video.setPointerCapture(pointerId);
    }

    const viewportBounds = viewport.getBoundingClientRect();
    return {
        viewportWidth: viewportBounds.width,
        viewportHeight: viewportBounds.height,
        videoWidth: video.videoWidth,
        videoHeight: video.videoHeight
    };
}

export function releasePointer(video, pointerId) {
    if (video.hasPointerCapture?.(pointerId)) {
        video.releasePointerCapture(pointerId);
    }
}

export function setMuted(video, muted = true) {
    video.defaultMuted = muted;
    video.muted = muted;
}

export async function playVideo(video) {
    await video.play();
}

export function pauseVideo(video) {
    video.pause();
}

export function seekVideo(video, time) {
    video.currentTime = time;
}

export function setVolume(video, volume) {
    video.volume = volume;
}

export function setPlaybackRate(video, rate) {
    video.playbackRate = rate;
}

export function setLoop(video, enabled) {
    video.loop = enabled;
}

export function readMediaSnapshot(video) {
    return {
        currentTime: Number.isFinite(video.currentTime) ? video.currentTime : 0,
        duration: Number.isFinite(video.duration) ? video.duration : null,
        volume: video.volume,
        muted: video.muted,
        playbackRate: video.playbackRate,
        loop: video.loop,
        paused: video.paused,
        ended: video.ended
    };
}

export function enterFillTab(dotNetReference) {
    exitFillTab();

    fillTabEscapeHandler = event => {
        if (event.key !== "Escape") {
            return;
        }

        event.preventDefault();
        const activeReference = dotNetReference;
        exitFillTab();
        activeReference.invokeMethodAsync("ExitFillTabFromEscapeAsync").catch(() => { });
    };

    window.addEventListener("keydown", fillTabEscapeHandler);
    document.body.classList.add("fill-tab-active");
}

export function exitFillTab() {
    if (fillTabEscapeHandler) {
        window.removeEventListener("keydown", fillTabEscapeHandler);
        fillTabEscapeHandler = undefined;
    }

    document.body.classList.remove("fill-tab-active");
}
