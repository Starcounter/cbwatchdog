using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace CustomWatchdog
{
    public class UserInfo
    {
        public UserInfo()
        {
            var wi = WindowsIdentity.GetCurrent();
            Sid = wi.User;

            if (wi.IsSystem)
            {
                IsSystemAccount = true;
            }
            else
            {
                var serviceSid = new SecurityIdentifier(WellKnownSidType.LocalServiceSid, null);
                IsServiceAccount = serviceSid.Equals(Sid);
            }
            Name = ((NTAccount)Sid.Translate(typeof(NTAccount))).Value;
        }

        public SecurityIdentifier Sid { get; }

        public bool IsServiceAccount { get; }
        public bool IsSystemAccount { get; }
        public string Name { get; }
    }
}
