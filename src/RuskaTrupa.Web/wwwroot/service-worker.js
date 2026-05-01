// In development, always fetch from the network and do not enable offline support.
// Also replace any previously installed published worker, otherwise localhost can
// remain controlled by an old offline cache after switching between builds.
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
    })());
});

self.addEventListener('fetch', () => { });
