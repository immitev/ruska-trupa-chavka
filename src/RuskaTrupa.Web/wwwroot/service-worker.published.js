// Keep production pages network-first. Older deployments registered an offline
// worker with asset integrity checks, which can break GitHub Pages after HTML
// rewrites during deployment. This worker cleans those caches and unregisters.
self.addEventListener('install', event => {
    event.waitUntil(self.skipWaiting());
});

self.addEventListener('activate', event => {
    event.waitUntil((async () => {
        const cacheKeys = await caches.keys();
        await Promise.all(cacheKeys
            .filter(key => key.startsWith('offline-cache-'))
            .map(key => caches.delete(key)));

        await self.clients.claim();
        await self.registration.unregister();
    })());
});

self.addEventListener('fetch', () => { });
