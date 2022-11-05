﻿using BUTR.CrashReportServer.Models;

using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

using System;
using System.Security.Authentication;

namespace BUTR.CrashReportServer.Controllers
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class HttpsProtocolAttribute : Attribute, IAuthorizationFilter, IOrderedFilter
    {
        /// <inheritdoc />
        /// <value>Default is <c>int.MinValue + 50</c> to run this <see cref="IAuthorizationFilter"/> early.</value>
        public int Order { get; set; } = int.MinValue + 50;

        public SslProtocols Protocol { get; set; }

        public void OnAuthorization(AuthorizationFilterContext filterContext)
        {
            if (filterContext == null)
                throw new ArgumentNullException(nameof(filterContext));

            if (!filterContext.HttpContext.Request.IsHttps ||
                filterContext.HttpContext.Features.Get<ITlsHandshakeFeature>() is not { } tlsHandshakeFeature ||
                tlsHandshakeFeature.Protocol < Protocol)
            {
                filterContext.Result = new ObjectResult(new TLSError($"TLS minimally supported version: {Protocol}"))
                {
                    StatusCode = StatusCodes.Status400BadRequest
                };
            }
        }
    }
}