#include <megaapi.h>
#include <objbase.h>

#include <atomic>
#include <cstring>
#include <filesystem>
#include <memory>
#include <limits>
#include <mutex>
#include <string>
#include <string_view>
#include <unordered_set>

using namespace mega;

#define MD_EXPORT extern "C" __declspec(dllexport)
using Progress = void (__cdecl *)(long long, long long, long long);
using IsCancelled = int (__cdecl *)();
using DownloadLimit = long long (__cdecl *)();
using StreamData = int (__cdecl *)(const char*, int, long long);
using DriveNode = int (__cdecl *)(MegaHandle, MegaHandle, const char*, int, long long, long long);

namespace
{
int finish(SynchronousRequestListener& request)
{
    request.wait();
    return request.getError()->getErrorCode();
}

constexpr char userAgent[] = "MegaDownloader-Revival";
constexpr std::string_view folderPrefix = "https://mega.nz/folder/";

int applyProxy(MegaApi& api, MegaProxy& proxy)
{
    SynchronousRequestListener request;
    api.setProxySettings(&proxy, &request);
    return finish(request);
}

class AccountClient final : public MegaApi
{
public:
    AccountClient(const char* appKey, const char* cache)
        : MegaApi(appKey, cache, userAgent),
          cacheDirectory(cache ? std::filesystem::u8path(cache) : std::filesystem::current_path()) {}

    int updateProxy(const char* url, const char* username, const char* password)
    {
        auto settings = std::make_unique<MegaProxy>();
        settings->setProxyType(*url ? MegaProxy::PROXY_CUSTOM : MegaProxy::PROXY_NONE);
        if (*url)
        {
            settings->setProxyURL(url);
            settings->setCredentials(username, password);
        }

        const std::lock_guard lock(proxyMutex);
        const int result = applyProxy(*this, *settings);
        if (result == MegaError::API_OK) proxy = std::move(settings);
        return result;
    }

    int copyProxyTo(MegaApi& folder)
    {
        const std::lock_guard lock(proxyMutex);
        return proxy ? applyProxy(folder, *proxy) : MegaError::API_OK;
    }

    const std::filesystem::path cacheDirectory;

private:
    std::mutex proxyMutex;
    std::unique_ptr<MegaProxy> proxy;
};

// The SDK opens SQLite tables named after the share. Each lookup needs its own directory.
class FolderCache
{
public:
    explicit FolderCache(const std::filesystem::path& parent)
    {
        GUID id;
        if (FAILED(CoCreateGuid(&id))) return;
        wchar_t name[39];
        if (!StringFromGUID2(id, name, 39)) return;
        auto directory = parent / name;
        std::error_code error;
        if (std::filesystem::create_directory(directory, error)) path = std::move(directory);
    }

    ~FolderCache()
    {
        std::error_code error;
        if (!path.empty()) std::filesystem::remove_all(path, error);
    }

    std::filesystem::path path;
};

bool isBase64Url(std::string_view value)
{
    return value.find_first_not_of("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_") == std::string_view::npos;
}

int finishPublicRequest(MegaApi& api, SynchronousRequestListener& request,
                        IsCancelled cancelled, long long* quotaWaitSeconds)
{
    if (cancelled)
    {
        while (request.trywait(100) != 0)
        {
            if (!cancelled()) continue;
            api.removeRequestListener(&request);
            return MegaError::API_EINCOMPLETE;
        }
    }
    else request.wait();

    const auto* error = request.getError();
    const int result = error->getErrorCode();
    if (result == MegaError::API_EOVERQUOTA && quotaWaitSeconds)
    {
        const auto seconds = error->getValue();
        *quotaWaitSeconds = seconds > 0 ? seconds : api.getBandwidthOverquotaDelay();
    }
    return result;
}

int publicFolderFile(AccountClient& account, std::string_view link, std::unique_ptr<MegaNode>& node,
                     IsCancelled cancelled, long long* quotaWaitSeconds)
{
    constexpr size_t handleLength = 8;
    constexpr size_t keyLength = 22;
    constexpr std::string_view fileSeparator = "/file/";
    const auto hash = folderPrefix.size() + handleLength;
    const auto file = hash + 1 + keyLength;
    const auto handle = file + fileSeparator.size();
    if (link.size() != handle + handleLength || link[hash] != '#' ||
        link.substr(file, fileSeparator.size()) != fileSeparator ||
        !isBase64Url(link.substr(folderPrefix.size(), handleLength)) ||
        !isBase64Url(link.substr(hash + 1, keyLength)) || !isBase64Url(link.substr(handle)))
        return MegaError::API_EARGS;

    const std::unique_ptr<char[]> auth(account.getAccountAuth());
    if (!auth) return MegaError::API_ESID;
    FolderCache cache(account.cacheDirectory);
    if (cache.path.empty()) return MegaError::API_EWRITE;

    const auto path = cache.path.u8string();
    MegaApi folder(nullptr, reinterpret_cast<const char*>(path.c_str()), userAgent);
    int result = account.copyProxyTo(folder);
    if (result != MegaError::API_OK) return result;
    folder.setAccountAuth(auth.get());

    const std::string folderLink(link.substr(0, file));
    SynchronousRequestListener login;
    folder.loginToFolder(folderLink.c_str(), &login);
    result = finishPublicRequest(folder, login, cancelled, quotaWaitSeconds);
    if (result != MegaError::API_OK) return result;
    if (cancelled && cancelled()) return MegaError::API_EINCOMPLETE;

    SynchronousRequestListener fetch;
    folder.fetchNodes(&fetch);
    result = finishPublicRequest(folder, fetch, cancelled, quotaWaitSeconds);
    if (result != MegaError::API_OK) return result;
    if (!account.isLoggedIn()) return MegaError::API_ESID;
    if (fetch.getRequest()->getFlag()) return MegaError::API_EKEY;

    const std::string fileHandle(link.substr(handle));
    const std::unique_ptr<MegaNode> child(folder.getNodeByHandle(MegaApi::base64ToHandle(fileHandle.c_str())));
    if (!child || !child->isFile()) return MegaError::API_ENOENT;
    if (!child->isNodeKeyDecrypted()) return MegaError::API_EKEY;
    // Keep the folder's read authorization on the node so the account client can transfer it.
    node.reset(folder.authorizeNode(child.get()));
    return node ? MegaError::API_OK : MegaError::API_EACCESS;
}

int publicFile(MegaApi& api, const char* link, std::unique_ptr<MegaNode>& node,
               IsCancelled cancelled = nullptr, long long* quotaWaitSeconds = nullptr)
{
    if (!api.isLoggedIn()) return MegaError::API_ESID;
    if (cancelled && cancelled()) return MegaError::API_EINCOMPLETE;
    if (std::string_view(link).starts_with(folderPrefix))
        return publicFolderFile(static_cast<AccountClient&>(api), link, node, cancelled, quotaWaitSeconds);

    SynchronousRequestListener request;
    api.getPublicNode(link, &request);
    const int result = finishPublicRequest(api, request, cancelled, quotaWaitSeconds);
    if (result != MegaError::API_OK) return result;
    if (!api.isLoggedIn()) return MegaError::API_ESID;
    if (request.getRequest()->getFlag()) return MegaError::API_EKEY;
    node.reset(request.getRequest()->getPublicMegaNode());
    return node ? MegaError::API_OK : MegaError::API_ENOENT;
}

class Transfer final : public SynchronousTransferListener
{
public:
    explicit Transfer(Progress progress) : progress(progress) {}

    void onTransferUpdate(MegaApi*, MegaTransfer* transfer) override
    {
        progress(transfer->getTransferredBytes(), transfer->getTotalBytes(), transfer->getSpeed());
    }

    void onTransferTemporaryError(MegaApi* api, MegaTransfer* transfer, MegaError* error) override
    {
        if (error->getErrorCode() == MegaError::API_EOVERQUOTA && !overQuota)
        {
            quotaWaitSeconds = error->getValue();
            overQuota = true;
            // Queue cancellation on the SDK worker so it wakes even during quota backoff.
            api->cancelTransfer(transfer);
        }
    }

    std::atomic<bool> overQuota{false};
    long long quotaWaitSeconds = 0;
private:
    Progress progress;
};
}

MD_EXPORT int md_set_proxy(MegaApi* api, const char* proxyUrl, const char* proxyUser, const char* proxyPassword)
{
    return static_cast<AccountClient&>(*api).updateProxy(proxyUrl, proxyUser, proxyPassword);
}

MD_EXPORT MegaApi* md_open(const char* appKey, const char* cache,
                            const char* proxyUrl, const char* proxyUser, const char* proxyPassword)
{
    auto api = std::make_unique<AccountClient>(appKey, cache);
    // Private downloads use streaming as a ranged read, not for real-time playback.
    api->setStreamingMinimumRate(0);
    if (*proxyUrl && api->updateProxy(proxyUrl, proxyUser, proxyPassword) != MegaError::API_OK) return nullptr;
    return api.release();
}

MD_EXPORT void md_close(MegaApi* api) { delete api; }
MD_EXPORT void md_free_string(const char* value) { delete[] value; }

MD_EXPORT int md_sign_in(MegaApi* api, const char* email, const char* password,
                         const char* pin, const char* session)
{
    SynchronousRequestListener request;
    if (*session) api->fastLogin(session, &request);
    else if (*pin) api->multiFactorAuthLogin(email, password, pin, &request);
    else api->login(email, password, &request);
    const int result = finish(request);
    if (result != MegaError::API_OK) return result;
    SynchronousRequestListener nodes;
    api->fetchNodes(&nodes);
    return finish(nodes);
}

MD_EXPORT char* md_session(MegaApi* api) { return api->dumpSession(); }

MD_EXPORT int md_sign_out(MegaApi* api)
{
    SynchronousRequestListener request;
    api->logout(&request);
    return finish(request);
}

MD_EXPORT int md_quota(MegaApi* api, long long* used, long long* total)
{
    SynchronousRequestListener request;
    api->getSpecificAccountDetails(false, true, false, -1, &request);
    const int result = finish(request);
    if (result != MegaError::API_OK) return result;
    std::unique_ptr<MegaAccountDetails> details(request.getRequest()->getMegaAccountDetails());
    *used = details->getTransferUsed();
    *total = details->getTransferMax();
    return MegaError::API_OK;
}

MD_EXPORT int md_account_details(MegaApi* api, int* plan, long long* storageUsed,
                                  long long* storageTotal, long long* transferUsed, long long* transferTotal)
{
    *plan = -1;
    *storageUsed = *storageTotal = *transferUsed = *transferTotal = -1;

    SynchronousRequestListener request;
    api->getAccountDetails(&request);
    const int result = finish(request);
    if (result != MegaError::API_OK) return result;
    std::unique_ptr<MegaAccountDetails> details(request.getRequest()->getMegaAccountDetails());
    if (!details) return MegaError::API_EINTERNAL;

    *plan = details->getProLevel();
    *storageUsed = details->getStorageUsed();
    *storageTotal = details->getStorageMax();
    const auto transferMax = details->getTransferMax();
    // Free IP-based quota has no fixed allowance, and the SDK may mark its usage history invalid.
    *transferTotal = transferMax > 0 ? transferMax : -1;
    if (transferMax > 0 || details->isTemporalBandwidthValid())
        *transferUsed = details->getTransferUsed();
    return MegaError::API_OK;
}

namespace
{
// The SDK never assigns INVALID_HANDLE to a real node.
constexpr MegaHandle sharesRoot = INVALID_HANDLE;

class DriveTree final : public MegaTreeProcessor
{
public:
    explicit DriveTree(DriveNode visit, const std::unordered_set<MegaHandle>* sharedRoots = nullptr)
        : visit(visit), sharedRoots(sharedRoots) {}

    bool processMegaNode(MegaNode* node) override
    {
        const auto handle = node->getHandle();
        if (sharedRoots && !visited.insert(handle).second) return true;
        const auto parent = sharedRoots && sharedRoots->contains(handle) ? sharesRoot : node->getParentHandle();
        const bool file = node->isFile();
        const auto time = file ? node->getModificationTime() : node->getCreationTime();
        return visit(handle, parent, node->getName(), file ? 0 : 1, file ? node->getSize() : 0, time) != 0;
    }

private:
    DriveNode visit;
    const std::unordered_set<MegaHandle>* sharedRoots;
    std::unordered_set<MegaHandle> visited;
};

int describeFile(MegaNode& node, char* name, int nameCapacity, long long* size)
{
    if (strcpy_s(name, nameCapacity, node.getName()) != 0) return MegaError::API_EARGS;
    *size = node.getSize();
    return MegaError::API_OK;
}

// Read the allocation on the bridge caller thread, outside SDK callbacks.
class DownloadRate
{
public:
    DownloadRate(MegaApi* api, DownloadLimit limit) : api(api), limit(limit) { update(); }

    void update()
    {
        if (!limit) return;
        if (limit() == applied) return;
        // Concurrent transfers on the same client share one allocation. Sample
        // again under the lock so a stale sample cannot overwrite a newer cap.
        static std::mutex rateUpdates;
        const std::lock_guard lock(rateUpdates);
        applied = limit();
        api->setMaxDownloadSpeed(applied);
    }

private:
    MegaApi* api;
    DownloadLimit limit;
    long long applied = -1;
};

int downloadFile(MegaApi* api, MegaNode* node, const char* destination,
                 Progress progress, IsCancelled isCancelled, long long& quotaWaitSeconds, DownloadLimit limit = nullptr)
{
    if (isCancelled()) return MegaError::API_EINCOMPLETE;
    DownloadRate rate(api, limit);
    std::unique_ptr<MegaCancelToken> token(MegaCancelToken::createInstance());
    Transfer transfer(progress);
    api->startDownload(node, destination, nullptr, nullptr, false, token.get(),
                       MegaTransfer::COLLISION_CHECK_ALWAYSERROR,
                       MegaTransfer::COLLISION_RESOLUTION_NEW_WITH_N, false, &transfer);
    while (transfer.trywait(100) != 0)
    {
        if (isCancelled()) token->cancel();
        rate.update();
    }
    const int result = transfer.getError()->getErrorCode();
    if (result == MegaError::API_EOVERQUOTA || (result == MegaError::API_EINCOMPLETE && transfer.overQuota))
    {
        const auto seconds = transfer.overQuota ? transfer.quotaWaitSeconds : transfer.getError()->getValue();
        quotaWaitSeconds = seconds > 0 ? seconds : api->getBandwidthOverquotaDelay();
        return MegaError::API_EOVERQUOTA;
    }
    return result;
}
}

MD_EXPORT int md_inspect(MegaApi* api, const char* link, char* name, int nameCapacity, long long* size)
{
    std::unique_ptr<MegaNode> node;
    const int result = publicFile(*api, link, node);
    return result == MegaError::API_OK ? describeFile(*node, name, nameCapacity, size) : result;
}

MD_EXPORT int md_download_with_limit(MegaApi* api, const char* link, const char* destination,
                                      Progress progress, IsCancelled isCancelled, long long* quotaWaitSeconds, DownloadLimit limit)
{
    *quotaWaitSeconds = 0;
    std::unique_ptr<MegaNode> node;
    const int result = publicFile(*api, link, node, isCancelled, quotaWaitSeconds);
    return result == MegaError::API_OK ? downloadFile(api, node.get(), destination, progress, isCancelled, *quotaWaitSeconds, limit) : result;
}

MD_EXPORT int md_download_with_quota(MegaApi* api, const char* link, const char* destination,
                                      Progress progress, IsCancelled isCancelled, long long* quotaWaitSeconds)
{
    return md_download_with_limit(api, link, destination, progress, isCancelled, quotaWaitSeconds, nullptr);
}

MD_EXPORT int md_download(MegaApi* api, const char* link, const char* destination,
                           Progress progress, IsCancelled isCancelled)
{
    long long quotaWaitSeconds;
    return md_download_with_quota(api, link, destination, progress, isCancelled, &quotaWaitSeconds);
}

MD_EXPORT int md_list_drive(MegaApi* api, DriveNode visit, MegaHandle* rootHandle)
{
    if (!api->isLoggedIn()) return MegaError::API_ESID;
    std::unique_ptr<MegaNode> root(api->getRootNode());
    if (!root) return MegaError::API_ENOENT;
    *rootHandle = root->getHandle();
    DriveTree tree(visit);
    return api->processMegaTree(root.get(), &tree) ? MegaError::API_OK : MegaError::API_EINCOMPLETE;
}

MD_EXPORT int md_list_shares(MegaApi* api, DriveNode visit, MegaHandle* rootHandle)
{
    if (!api->isLoggedIn()) return MegaError::API_ESID;
    *rootHandle = sharesRoot;
    if (!visit(sharesRoot, INVALID_HANDLE, "Shared with me", 1, 0, 0))
        return MegaError::API_EINCOMPLETE;

    const std::unique_ptr<MegaNodeList> shares(api->getInShares(MegaApi::ORDER_NONE));
    std::unordered_set<MegaHandle> sharedRoots;
    for (int i = 0; i < shares->size(); ++i)
        sharedRoots.insert(shares->get(i)->getHandle());

    DriveTree tree(visit, &sharedRoots);
    for (int i = 0; i < shares->size(); ++i)
    {
        if (!api->processMegaTree(shares->get(i), &tree)) return MegaError::API_EINCOMPLETE;
    }
    return MegaError::API_OK;
}

MD_EXPORT int md_inspect_node(MegaApi* api, MegaHandle handle, char* name, int capacity, long long* size)
{
    if (!api->isLoggedIn()) return MegaError::API_ESID;
    std::unique_ptr<MegaNode> node(api->getNodeByHandle(handle));
    return node && node->isFile() ? describeFile(*node, name, capacity, size) : MegaError::API_ENOENT;
}

MD_EXPORT int md_download_node(MegaApi* api, MegaHandle handle, const char* destination,
                                Progress progress, IsCancelled isCancelled)
{
    if (!api->isLoggedIn()) return MegaError::API_ESID;
    std::unique_ptr<MegaNode> node(api->getNodeByHandle(handle));
    long long quotaWaitSeconds;
    return node && node->isFile() ? downloadFile(api, node.get(), destination, progress, isCancelled, quotaWaitSeconds) : MegaError::API_ENOENT;
}

namespace
{
std::unique_ptr<char[]> fileKey(MegaNode& node)
{
    const auto* raw = node.getNodeKey();
    if (!node.isNodeKeyDecrypted() || !raw || raw->size() != 32) return {};
    return std::unique_ptr<char[]>(node.getBase64Key());
}

class RangeTransfer final : public SynchronousTransferListener
{
public:
    RangeTransfer(StreamData write, IsCancelled cancelled) : write(write), cancelled(cancelled) {}

    void onTransferStart(MegaApi*, MegaTransfer* transfer) override { tag = transfer->getTag(); }

    void onTransferTemporaryError(MegaApi*, MegaTransfer*, MegaError* error) override
    {
        if (error->getErrorCode() == MegaError::API_EOVERQUOTA)
        {
            quotaWaitSeconds = error->getValue();
            overQuota = true;
        }
    }

    bool onTransferData(MegaApi*, MegaTransfer* transfer, char* bytes, size_t count) override
    {
        if (cancelled() || overQuota) return false;
        if (count <= static_cast<size_t>((std::numeric_limits<int>::max)()) &&
            write(bytes, static_cast<int>(count), transfer->getSpeed())) return true;
        writeFailed = true;
        return false;
    }

    std::atomic<int> tag{-1};
    std::atomic<bool> overQuota{false};
    long long quotaWaitSeconds = 0;
    bool writeFailed = false;

private:
    StreamData write;
    IsCancelled cancelled;
};
}

MD_EXPORT int md_inspect_node_for_resume(MegaApi* api, MegaHandle handle, char* name, int nameCapacity,
                                         long long* size, char* key, int keyCapacity)
{
    if (!api->isLoggedIn()) return MegaError::API_ESID;
    std::unique_ptr<MegaNode> node(api->getNodeByHandle(handle));
    if (!node || !node->isFile()) return MegaError::API_ENOENT;
    const auto fullKey = fileKey(*node);
    if (!fullKey) return MegaError::API_EKEY;
    if (keyCapacity < 44 || strcpy_s(key, keyCapacity, fullKey.get()) != 0) return MegaError::API_EARGS;
    return describeFile(*node, name, nameCapacity, size);
}

MD_EXPORT int md_stream_node_with_limit(MegaApi* api, MegaHandle handle, const char* expectedKey, long long expectedSize,
                                         long long offset, StreamData write, IsCancelled cancelled, long long* quotaWaitSeconds,
                                         DownloadLimit limit)
{
    *quotaWaitSeconds = 0;
    if (cancelled()) return MegaError::API_EINCOMPLETE;
    if (!api->isLoggedIn()) return MegaError::API_ESID;
    std::unique_ptr<MegaNode> node(api->getNodeByHandle(handle));
    if (!node || !node->isFile()) return MegaError::API_ENOENT;
    const auto fullKey = fileKey(*node);
    if (!fullKey || std::strcmp(fullKey.get(), expectedKey) != 0 || node->getSize() != expectedSize)
        return MegaError::API_EKEY;
    if (offset < 0 || offset > expectedSize) return MegaError::API_EARGS;
    if (offset == expectedSize) return MegaError::API_OK; // already complete or empty; managed code verifies it

    DownloadRate rate(api, limit);
    RangeTransfer transfer(write, cancelled);
    api->startStreaming(node.get(), offset, expectedSize - offset, &transfer);
    bool cancelSent = false;
    while (transfer.trywait(100) != 0)
    {
        const int tag = transfer.tag;
        if (!cancelSent && tag >= 0 && (cancelled() || transfer.overQuota))
        {
            cancelSent = true;
            api->cancelTransferByTag(tag);
        }
        rate.update();
    }

    const int result = transfer.getError()->getErrorCode();
    if (transfer.overQuota || result == MegaError::API_EOVERQUOTA)
    {
        const auto seconds = transfer.overQuota ? transfer.quotaWaitSeconds : transfer.getError()->getValue();
        *quotaWaitSeconds = seconds > 0 ? seconds : api->getBandwidthOverquotaDelay();
        return MegaError::API_EOVERQUOTA;
    }
    if (cancelled()) return MegaError::API_EINCOMPLETE;
    if (transfer.writeFailed) return MegaError::API_EWRITE;
    return result;
}

MD_EXPORT int md_stream_node(MegaApi* api, MegaHandle handle, const char* expectedKey, long long expectedSize,
                              long long offset, StreamData write, IsCancelled cancelled, long long* quotaWaitSeconds)
{
    return md_stream_node_with_limit(api, handle, expectedKey, expectedSize, offset, write, cancelled, quotaWaitSeconds, nullptr);
}
