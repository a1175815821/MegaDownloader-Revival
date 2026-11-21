#include <megaapi.h>

#include <atomic>
#include <cstring>
#include <memory>

using namespace mega;

#define MD_EXPORT extern "C" __declspec(dllexport)
using Progress = void (__cdecl *)(long long, long long, long long);
using IsCancelled = int (__cdecl *)();

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
        // Queue cancellation on the SDK worker so it wakes even during quota backoff.
        if (error->getErrorCode() == MegaError::API_EOVERQUOTA && !overQuota.exchange(true))
            api->cancelTransfer(transfer);
    }

    std::atomic<bool> overQuota{false};
private:
    Progress progress;
};
}

MD_EXPORT MegaApi* md_open(const char* appKey, const char* cache,
                            const char* proxyUrl, const char* proxyUser, const char* proxyPassword)
{
    auto api = std::make_unique<MegaApi>(appKey, cache, "MegaDownloader-Revival");
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

MD_EXPORT int md_inspect(MegaApi* api, const char* link, char* name, int nameCapacity, long long* size)
{
    std::unique_ptr<MegaNode> node;
    const int result = publicFile(*api, link, node);
    if (result != MegaError::API_OK) return result;
    if (strcpy_s(name, nameCapacity, node->getName()) != 0) return MegaError::API_EARGS;
    *size = node->getSize();
    return MegaError::API_OK;
}

MD_EXPORT int md_download(MegaApi* api, const char* link, const char* destination,
                           Progress progress, IsCancelled isCancelled)
{
    std::unique_ptr<MegaNode> node;
    const int result = publicFile(*api, link, node);
    if (result != MegaError::API_OK) return result;
    if (isCancelled()) return MegaError::API_EINCOMPLETE;
    std::unique_ptr<MegaCancelToken> token(MegaCancelToken::createInstance());
    Transfer transfer(progress);
    api->startDownload(node.get(), destination, nullptr, nullptr, false, token.get(),
                       MegaTransfer::COLLISION_CHECK_ALWAYSERROR,
                       MegaTransfer::COLLISION_RESOLUTION_NEW_WITH_N, false, &transfer);
    while (transfer.trywait(100) != 0)
    {
        if (isCancelled()) token->cancel();
    }
    const int transferResult = transfer.getError()->getErrorCode();
    if (transferResult == MegaError::API_EINCOMPLETE && transfer.overQuota)
        return MegaError::API_EOVERQUOTA;
    return transferResult;
}
