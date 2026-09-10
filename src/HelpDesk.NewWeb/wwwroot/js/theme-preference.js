window.helpdeskThemePreference = (() => {
    const storageKey = "helpdesk.theme.preference";

    const apply = (isDarkMode) => {
        document.documentElement.dataset.helpdeskTheme = isDarkMode ? "dark" : "light";
    };

    return {
        get: () => window.localStorage.getItem(storageKey),
        set: (value) => {
            if (value === "system") {
                window.localStorage.removeItem(storageKey);
                return;
            }

            window.localStorage.setItem(storageKey, value);
        },
        apply
    };
})();
