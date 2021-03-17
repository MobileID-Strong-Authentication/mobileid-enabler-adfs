
namespace MobileId
{
    // Interface for MSS_Signature 1.1
    public interface IAuthentication
    {
        int GetClientVersion();
        AuthResponseDto RequestSignature(AuthRequestDto req, bool async);
        AuthResponseDto PollSignature(AuthRequestDto req, string msspTransId);
        
    }

}
