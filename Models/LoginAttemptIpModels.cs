using System;

namespace skpd_multi_tenant_api.Models
{
    public class LoginAttemptIp
    {
        public long Id { get; set; }
        public string? Email { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? FailureReason { get; set; }
        public DateTime AttemptTime { get; set; }
    }

    public class LoginAttemptIpResponse
    {
        public IEnumerable<LoginAttemptIp> Items { get; set; } = [];
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
