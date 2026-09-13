const observers = new Map();
let nextId = 0;

export function start() {
    const id = ++nextId;
    let fingerprint;
    let pending = false;
    let stopped = false;
    async function refresh() {
        if (pending || stopped) return;
        pending = true;
        try {
            const response = await fetch('/session/access', { credentials: 'same-origin', cache: 'no-store' });
            if (response.status === 401 || response.status === 403) {
                location.replace('/login');
                return;
            }
            if (!response.ok) return;
            const profile = await response.json();
            const sorted = values => [...(values ?? [])].sort();
            const next = JSON.stringify({
                authenticated: profile.isAuthenticated,
                admin: profile.isHelpdeskAdmin,
                customer: profile.customerId,
                permissions: sorted(profile.permissions),
                roles: sorted(profile.roleBundles),
                organizations: sorted(profile.allowedOrganizationIds),
                grants: sorted((profile.scopedPermissionGrants ?? []).map(grant => `${grant.permission}|${grant.organizationId}`))
            });
            if (fingerprint !== undefined && fingerprint !== next) location.reload();
            fingerprint = next;
        } catch { /* A transient outage preserves the rendered page until the next check. */ }
        finally { pending = false; }
    }
    const timer = setInterval(refresh, 30_000);
    const focused = () => { if (document.visibilityState === 'visible') void refresh(); };
    document.addEventListener('visibilitychange', focused);
    void refresh();
    observers.set(id, () => { stopped = true; clearInterval(timer); document.removeEventListener('visibilitychange', focused); });
    return id;
}

export function stop(id) {
    observers.get(id)?.();
    observers.delete(id);
}
