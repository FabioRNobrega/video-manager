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
