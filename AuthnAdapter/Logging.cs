using Microsoft.Diagnostics.Tracing;
using System.Text;


namespace MobileId.Adfs
{
    [EventSource(Name = "Swisscom-MobileID-Adfs12")]
    public sealed class Logging : EventSource
    {
        static readonly public Logging Log = new Logging();

        [Event(1, Keywords = Keywords.Transport, Level = EventLevel.Verbose, Channel = EventChannel.Analytic,
            Message="instanceId={0}")]
        public void WebClientCreated(int InstanceId) {WriteEvent(1, InstanceId); }

        [Event(2, Keywords = Keywords.Transport, Level = EventLevel.Verbose, Channel = EventChannel.Analytic,
            Message="instanceId={0}")]
        public void WebClientDestroyed(int InstanceId) {WriteEvent(2, InstanceId); }

        [Event(3, Keywords = Keywords.AttrStore, Level = EventLevel.Verbose, Channel = EventChannel.Debug,
            Message="upn={0}, ldapFilter='{1}', {2}={3}, {4}={5}")]
        public void AdSearch(string Upn, string LdapFilter, string AttributeMobile, string Mobile, string AttributeUserSerialNumber, string UserSerialNumber) {
            if (IsEnabled()) WriteEvent(3, Upn, LdapFilter, AttributeMobile, _s(Mobile), AttributeUserSerialNumber, _s(UserSerialNumber)); }

        [Event(4, Keywords = Keywords.AttrStore, Level = EventLevel.Error, Channel = EventChannel.Admin,
            Message = "attribute store - AD search: error={0}")]
        public void AdSearchError(string ExceptionMessage) {WriteEvent(4, ExceptionMessage); }

        [Event(5, Keywords = Keywords.AttrStore, Level = EventLevel.Warning, Channel = EventChannel.Admin,
            Message = "attribute store - user not found: upn={0}, ldapFilter='{1}'")]
        public void AttrUserNotFound(string Upn, string LdapFilter) {WriteEvent(5, Upn, LdapFilter); }

        [Event(6, Keywords = Keywords.AttrStore, Level = EventLevel.Warning, Channel = EventChannel.Admin,
            Message = "attribute store - phonenumber not found: upn={0}")]
        public void AttrMobileNotFound(string Upn) {WriteEvent(6, Upn);}

        [Event(7, Keywords = Keywords.AttrStore, Level = EventLevel.Informational, Channel = EventChannel.Admin,
            Message = "attribute store - user serial number not found: upn={0}")]
        public void AttrUserSerialNumberNotFound(string Upn) {WriteEvent(7, Upn);}

        [Event(8, Keywords = Keywords.AttrStore, Level = EventLevel.Warning, Channel = EventChannel.Admin,
        Message = "attribute store - phonenumber malformed (e.g. illegal length): upn={0}, phoneNumber={1}")]
        public void AttrMobileMalformed(string Upn, string PhoneNumber) { WriteEvent(8, Upn, PhoneNumber); }

        [Event(10, Keywords = Keywords.AttrStore, Level = EventLevel.Verbose, Channel = EventChannel.Debug, Task = Tasks.IsAvailableForUser, Opcode = EventOpcode.Start,
            Message = "upn={0}, context='{1}'")]
        public void IsAvailableForUserStart(string Claim, string Context) { WriteEvent(10, Claim, Context); }

        [Event(11, Keywords = Keywords.AttrStore, Level = EventLevel.Verbose, Channel = EventChannel.Analytic, Task = Tasks.IsAvailableForUser, Opcode = EventOpcode.Stop,
            Message = "upn={1}, result={0}")]
        public void IsAvailableForUserStop(bool Result, string Upn) { WriteEvent(11, Result, Upn); }

        [Event(12, Keywords = Keywords.Config, Level = EventLevel.Verbose, Channel = EventChannel.Analytic, Task = Tasks.LoadAuthProvider, Opcode = EventOpcode.Start,
            Message = "instanceId={0}, version={1}")]
        public void LoadAuthProviderStart(int InstanceId, string Version) { WriteEvent(12, InstanceId, Version); }

        [Event(13, Keywords = Keywords.Config, Level = EventLevel.Verbose, Channel = EventChannel.Analytic, Task = Tasks.LoadAuthProvider, Opcode = EventOpcode.Stop,
            Message = "instanceId={0}")]
        public void LoadAuthProviderStop(int InstanceId) { WriteEvent(13, InstanceId); }

        [Event(14, Keywords = Keywords.Config, Level = EventLevel.Informational, Channel = EventChannel.Admin, Task = Tasks.LoadAuthProvider, Opcode = EventOpcode.Info,
            Message = "load config: codeVersion={0}, cfg='{1}'")]
        public void ConfigInfo(string CodeVersion, string Content) { WriteEvent(14, CodeVersion, Content); }

        [Event(15, Keywords = Keywords.Config, Level = EventLevel.Error, Channel = EventChannel.Admin,
            Message = "config: error='{0}'")]
        public void ConfigError(string Message) { WriteEvent(15, Message); }

        [Event(20, Keywords = Keywords.Service, Level = EventLevel.Error, Channel = EventChannel.Admin,
            Message = "authentication: error='{0}'")]
        public void AuthenticationGeneralError(string Message) { WriteEvent(20, Message); }

        [Event(21, Keywords = Keywords.Service | Keywords.Audit | EventKeywords.AuditSuccess, Level = EventLevel.Informational, Channel = EventChannel.Admin,
            Message = "authentication success: upn={2}, msspTransId={3}, stateOld={0}, stateNew={1}")]
        public void AuthenticationSuccess(int StateOld, int StateNew, string Upn, string MsspTransId) { WriteEvent(21, StateOld, StateNew, Upn, MsspTransId); }

        [Event(22, Keywords = Keywords.Service | Keywords.Audit | EventKeywords.AuditFailure, Level = EventLevel.Warning, Channel = EventChannel.Admin,
            Message = "authentication failure: upn={2}, reason={4}, msspTransId={3}, stateOld={0}, stateNew={1}")]
        public void AuthenticationFail(int StateOld, int StateNew, string Upn, string MsspTransId, string Reason) { WriteEvent(22, StateOld, StateNew, Upn, _s(MsspTransId), _s(Reason)); }

        [Event(23, Keywords = Keywords.Service | Keywords.Audit | EventKeywords.AuditFailure, Level = EventLevel.Warning, Channel = EventChannel.Admin,
            Message = "authentication failure: upn={3}, reason=Timeout, msspTransId={4}, ageSeconds={2}, stateOld={0}, stateNew={1}")]
        public void AuthenticationTimeout(int StateOld, int StateNew, int AgeSeconds, string Upn, string MsspTransId) { WriteEvent(23, StateOld, StateNew, AgeSeconds, Upn, _s(MsspTransId)); }

        [Event(24, Keywords = Keywords.Service | Keywords.Audit | EventKeywords.AuditFailure, Level = EventLevel.Error, Channel = EventChannel.Admin,
            Message = "authentication error: upn={2}, reason={4}, msspTransId={3}, stateOld={0}, stateNew={1}, Detail='{5}'")]
        public void AuthenticationTechnicalError(int StateOld, int StateNew, string Upn, string MsspTransId, string Reason, string Detail) { WriteEvent(24, StateOld, StateNew, Upn, _s(MsspTransId), _s(Reason), _s(Detail)); }

        [Event(25, Keywords = Keywords.Service, Level = EventLevel.Error, Channel = EventChannel.Admin,
            Message = "authentication session timeout: upn={3}, msspTransId={4}, ageSeconds={2}, stateOld={0}, stateNew={1}")]
        public void SessionTimeout(int StateOld, int StateNew, int AgeSeconds, string Upn, string MsspTransId) { WriteEvent(25, StateOld, StateNew, AgeSeconds, Upn, _s(MsspTransId)); }

        [Event(26, Keywords = Keywords.Service, Level = EventLevel.Error, Channel = EventChannel.Admin,
            Message = "too much authentication retries: upn={3}, msspTransId={4}, retries={2}, stateOld={0}, stateNew={1}")]
        public void SessionTooMuchRetries(int StateOld, int StateNew, int Retries, string Upn, string MsspTransId) { WriteEvent(26, StateOld, StateNew, Retries, Upn, MsspTransId); }

        [Event(27, Keywords = Keywords.Service, Level = EventLevel.Verbose, Channel = EventChannel.Analytic,
            Message = "upn={2}, msspTransId={3}, stateOld={0}, stateNew={1}")]
        public void AuthenticationContinue(int StateOld, int StateNew, string Upn, string MsspTransId) { WriteEvent(27, StateOld, StateNew, Upn, MsspTransId); }

        [Event(28, Keywords = Keywords.Service | Keywords.Audit | EventKeywords.AuditFailure, Level = EventLevel.Warning, Channel = EventChannel.Admin,
            Message = "authentication canceled: upn={2}, msspTransId={3}, stateOld={0}, stateNew={1}")]
        public void AuthenticationCancel(int StateOld, int StateNew, string Upn, string MsspTransId) { WriteEvent(28, StateOld, StateNew, Upn, _s(MsspTransId)); }

        [Event(29, Keywords = Keywords.Service, Level = EventLevel.Verbose, Channel = EventChannel.Analytic,
            Message = "authentication pending: upn={2}, msspTransId={3}, stateOld={0}, stateNew={1}")]
        public void AuthenticationPending(int StateOld, int StateNew, string Upn, string MsspTransId) { WriteEvent(29, StateOld, StateNew, Upn, MsspTransId); }

        [Event(30, Keywords = Keywords.Service | Keywords.Audit | Keywords.Attack | EventKeywords.AuditFailure, Level = EventLevel.Warning, Channel = EventChannel.Admin,
            Message = "authentication request error: upn={2}, reason={4}, msspTransId={3}, stateOld={0}, stateNew={1}")]
        public void AuthenticationBadForm(int StateOld, int StateNew, string Upn, string MsspTransId, string FormAction) { WriteEvent(30, StateOld, StateNew, Upn, _s(MsspTransId), _s(FormAction)); }

        [Event(19, Keywords = Keywords.Service, Level = EventLevel.Verbose, Channel = EventChannel.Debug, Task = Tasks.EndAuthentication, Opcode = EventOpcode.Start,
            Message = "formAction='{0}', context={1}, proofData='{2}', request='{3}'")]
        public void TryEndAuthenticationStart(string FormAction, string Context, string ProofData, string Request) {
            if (IsEnabled()) WriteEvent(19, _s(FormAction), Context, ProofData, Request);}

        [Event(18, Keywords = Keywords.Service, Level = EventLevel.Verbose, Channel = EventChannel.Debug, Task = Tasks.EndAuthentication, Opcode = EventOpcode.Stop,
            Message = "TryEndAuthencation returned")]
        public void TryEndAuthenticationStop() {WriteEvent(18);}

        [Event(17, Keywords = Keywords.Presentation, Level = EventLevel.Warning, Channel = EventChannel.Debug,
            Message = "presenstion warning: reason='{0}, message='{1}'")]
        public void PresentationWarning(string Reason, string Message) {WriteEvent(17, Reason, Message);}

        [NonEvent]
        public string _s(string nullableString) { return nullableString != null ? nullableString : "<null>";}

        public class Keywords
        {
            public const EventKeywords Audit        = (EventKeywords) 0x01L;
            public const EventKeywords Config       = (EventKeywords) 0x02L;
            public const EventKeywords Transport    = (EventKeywords) 0x08L;
            public const EventKeywords Presentation = (EventKeywords) 0x10L;
            public const EventKeywords Service      = (EventKeywords) 0x20L;
            public const EventKeywords AttrStore    = (EventKeywords) 0x40L;
            public const EventKeywords Attack       = (EventKeywords) 0x80L;

            public string AsString(EventKeywords keywords)
            {
                StringBuilder sb = new StringBuilder();
                string[] names = new string[] { "Audit", "Config", "", "Transport", "Presentation", "Service", "AttrStore", "Attack" };
                for (int i = 0, k = 1; k <= 0x80; i++, k *= 2)
                {
                    if (k == 4) continue;
                    if ((k & (long)keywords) != 0)
                    {
                        if (sb.Length > 0) sb.Append(",");
                        sb.Append(names[i]);
                    }
                }
                return sb.ToString();
            }

        }

        public class Tasks
        {
            public const EventTask IsAvailableForUser = (EventTask) 1;
            public const EventTask BeginAuthentication = (EventTask) 2;
            public const EventTask EndAuthentication = (EventTask) 3;
            public const EventTask LoadAuthProvider = (EventTask) 4;
        }

    }


}
