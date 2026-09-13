export async function post(path, body) {
    if (!path.startsWith("/api/v1/local-auth/")) throw new Error("Unsupported account endpoint");
    const response = await fetch(path, {
        method: "POST", credentials: "same-origin", cache: "no-store",
        headers: { "Content-Type": "application/json", "X-Requested-With": "XMLHttpRequest" },
        body: JSON.stringify(body)
    });
    return { ok: response.ok, status: response.status, body: await response.text() };
}
