using BUTR.CrashReport.Server.Models;

using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

using System;
using System.Security.Authentication;

namespace BUTR.CrashReport.Server;

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
            // TLS was terminated upstream (reverse proxy): ForwardedHeaders sets Request.IsHttps from
            // X-Forwarded-Proto, but the internal connection is plain HTTP and exposes no handshake feature.
            // We can't observe (let alone enforce) a minimum TLS version here, so skip the check.
            return;
        }

        if (tlsHandshakeFeature.Protocol < Protocol)
        {
            filterContext.Result = new ObjectResult(new TLSError { Message = $"TLS minimally supported version: {Protocol}; Got version: {tlsHandshakeFeature.Protocol}" })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }
    }
}