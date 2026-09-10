(function () {
    const handlers = new Map();
    let nextId = 1;

    function isEditable(target) {
        if (!target) return false;
        const element = target instanceof Element ? target : target.parentElement;
        if (!element) return false;
        if (element.closest('[contenteditable="true"]')) return true;
        const tag = element.tagName?.toLowerCase();
        return tag === 'input' || tag === 'textarea' || tag === 'select';
    }

    window.helpdeskGlobalSearchHotkeys = {
        register(dotNetRef) {
            const id = nextId++;
            const handler = (event) => {
                if (event.defaultPrevented || event.ctrlKey || event.metaKey || event.altKey) return;
                if (!event.shiftKey || event.key?.toLowerCase() !== 's') return;
                if (isEditable(event.target)) return;

                event.preventDefault();
                dotNetRef.invokeMethodAsync('OpenGlobalSearchFromShortcut');
            };

            window.addEventListener('keydown', handler, true);
            handlers.set(id, handler);
            return id;
        },
        dispose(id) {
            const handler = handlers.get(id);
            if (!handler) return;
            window.removeEventListener('keydown', handler, true);
            handlers.delete(id);
        }
    };
})();
