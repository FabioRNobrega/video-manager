const handlers = new WeakMap();

export function registerSaveShortcut(element, dotNetRef) {
    if (!element) {
        return;
    }

    const handler = (event) => {
        const key = (event.key || '').toLowerCase();
        if ((event.ctrlKey || event.metaKey) && key === 's') {
            event.preventDefault();
            dotNetRef.invokeMethodAsync('OnSaveShortcut');
        }
    };

    handlers.set(element, handler);
    element.addEventListener('keydown', handler);
}

export function unregisterSaveShortcut(element) {
    if (!element) {
        return;
    }

    const handler = handlers.get(element);
    if (handler) {
        element.removeEventListener('keydown', handler);
        handlers.delete(element);
    }
}

export function confirmDiscard(message) {
    return Promise.resolve(window.confirm(message));
}

export async function copyToClipboard(text) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch {
        return false;
    }
}
