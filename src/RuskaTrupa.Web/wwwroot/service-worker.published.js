// Keep production network-first so GitHub Pages updates are visible quickly,
// while still providing a real installable PWA worker and an offline fallback.
self.importScripts('./service-worker-assets.js');

const cacheNamePrefix = 'ruska-trupa-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineFallback = 'play/index.html';
const cacheableAssetPattern = /\.(?:dll|wasm|html|js|json|css|png|webmanifest|ico|dat|blat)$/;
const nonCacheableAssetPattern = /^(?:service-worker(?:\.published)?\.js|service-worker-assets\.js)$/;

self.addEventListener('install', event => {
    event.waitUntil((async () => {
        const cache = await caches.open(cacheName);
        const assets = self.assetsManifest.assets
            .filter(asset => cacheableAssetPattern.test(asset.url))
            .filter(asset => !nonCacheableAssetPattern.test(asset.url))
            .map(asset => new Request(asset.url, { cache: 'no-cache' }));

        await cache.addAll(assets);
        await self.skipWaiting();
    })());
});

self.addEventListener('activate', event => {
    event.waitUntil((async () => {
        const cacheKeys = await caches.keys();
        await Promise.all(cacheKeys
            .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
            .map(key => caches.delete(key)));

        await self.clients.claim();
    })());
});

self.addEventListener('fetch', event => {
    if (event.request.method !== 'GET') {
        return;
    }

    const requestUrl = new URL(event.request.url);
    if (requestUrl.origin !== self.location.origin) {
        return;
    }

    event.respondWith((async () => {
        const cache = await caches.open(cacheName);

        try {
            const response = await fetch(event.request);
            if (response.ok) {
                await cache.put(event.request, response.clone());
            }

            return response;
        }
        catch {
            const cachedResponse = await cache.match(event.request);
            if (cachedResponse) {
                return cachedResponse;
            }

            if (event.request.mode === 'navigate') {
                return await cache.match(offlineFallback);
            }

            throw new Error(`No cached response for ${event.request.url}`);
        }
    })());
});
