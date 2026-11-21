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

    void onTransferTemporaryError(MegaApi*, MegaTransfer*, MegaError* error) override
    {
        if (error->getErrorCode() == MegaError::API_EOVERQUOTA) overQuota = true;
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
        if (isCancelled() || transfer.overQuota) token->cancel();
    }
    if (transfer.overQuota) return MegaError::API_EOVERQUOTA;
    return transfer.getError()->getErrorCode();
}
