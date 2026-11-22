#include <megaapi.h>

#include <atomic>
#include <cstring>
#include <memory>
#include <limits>
#include <string>
#include <unordered_set>

using namespace mega;

#define MD_EXPORT extern "C" __declspec(dllexport)
using Progress = void (__cdecl *)(long long, long long, long long);
using IsCancelled = int (__cdecl *)();
using StreamData = int (__cdecl *)(const char*, int, long long);
using DriveNode = int (__cdecl *)(MegaHandle, MegaHandle, const char*, int, long long, long long);

namespace
{
int finish(SynchronousRequestListener& request)
{
    request.wait();
    return request.getError()->getErrorCode();
}

int publicFile(MegaApi& api, const char* link, std::unique_ptr<MegaNode>& node)
{
    if (!api.isLoggedIn()) return MegaError::API_ESID;
    SynchronousRequestListener request;
    api.getPublicNode(link, &request);
    const int result = finish(request);
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

MD_EXPORT MegaApi* md_open(const char* appKey, const char* cache,
                            const char* proxyUrl, const char* proxyUser, const char* proxyPassword)
{
    auto api = std::make_unique<MegaApi>(appKey, cache, "MegaDownloader-Revival");
    // Private downloads use streaming as a ranged read, not for real-time playback.
    api->setStreamingMinimumRate(0);
    if (*proxyUrl)
    {
        MegaProxy proxy;
        proxy.setProxyType(MegaProxy::PROXY_CUSTOM);
        proxy.setProxyURL(proxyUrl);
        proxy.setCredentials(proxyUser, proxyPassword);
        SynchronousRequestListener request;
        api->setProxySettings(&proxy, &request);
        if (finish(request) != MegaError::API_OK) return nullptr;
    }
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

int downloadFile(MegaApi* api, MegaNode* node, const char* destination,
                 Progress progress, IsCancelled isCancelled, long long& quotaWaitSeconds)
{
    if (isCancelled()) return MegaError::API_EINCOMPLETE;
    std::unique_ptr<MegaCancelToken> token(MegaCancelToken::createInstance());
    Transfer transfer(progress);
    api->startDownload(node, destination, nullptr, nullptr, false, token.get(),
                       MegaTransfer::COLLISION_CHECK_ALWAYSERROR,
                       MegaTransfer::COLLISION_RESOLUTION_NEW_WITH_N, false, &transfer);
    while (transfer.trywait(100) != 0)
    {
        if (isCancelled()) token->cancel();
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

MD_EXPORT int md_download_with_quota(MegaApi* api, const char* link, const char* destination,
                                      Progress progress, IsCancelled isCancelled, long long* quotaWaitSeconds)
{
    *quotaWaitSeconds = 0;
    std::unique_ptr<MegaNode> node;
    const int result = publicFile(*api, link, node);
    if (result == MegaError::API_EOVERQUOTA) *quotaWaitSeconds = api->getBandwidthOverquotaDelay();
    return result == MegaError::API_OK ? downloadFile(api, node.get(), destination, progress, isCancelled, *quotaWaitSeconds) : result;
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

MD_EXPORT int md_stream_node(MegaApi* api, MegaHandle handle, const char* expectedKey, long long expectedSize,
                              long long offset, StreamData write, IsCancelled cancelled, long long* quotaWaitSeconds)
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
