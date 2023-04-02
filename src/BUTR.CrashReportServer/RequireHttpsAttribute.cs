using BUTR.CrashReportServer.Models;

using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

using System;
using System.Security.Authentication;

namespace BUTR.CrashReportServer
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class HttpsProtocolAttribute : Attribute, IAuthorizationFilter, IOrderedFilter
    {
        public int Order => int.MinValue + 50;

        public SslProtocols Protocol { get; set; }

        public void OnAuthorization(AuthorizationFilterContext filterContext)
        {
            if (filterContext == null)
                throw new ArgumentNullException(nameof(filterContext));

            if (filterContext.HttpContext.Features.Get<ITlsHandshakeFeature>() is not { } tlsHandshakeFeature)
            {
                if (filterContext.HttpContext.Request.IsHttps)
                {
                    throw new InvalidOperationException("ITlsHandshakeFeature is not found when https is enabled");
                }
                return;
            }

            if (tlsHandshakeFeature.Protocol < Protocol)
            {
                filterContext.Result = new ObjectResult(new TLSError($"TLS minimally supported version: {Protocol}; Got version: {tlsHandshakeFeature.Protocol}"))
                {
                    StatusCode = StatusCodes.Status400BadRequest
                };
            }
        }
    }
}