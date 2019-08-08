﻿using System;

namespace Umbraco.Core.Composing
{
    /// <summary>
    /// Specifies the lifetime of a registered instance.
    /// </summary>
    public enum Lifetime
    {
        /// <summary>
        /// Always get a new instance.
        /// </summary>
        /// <remarks>Corresponds to Transient in LightInject, Castle Windsor
        /// or MS.DI, PerDependency in Autofac.</remarks>
        Transient,

        /// <summary>
        /// One unique instance per request.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Any instance created with this lifetime will be disposed at the end of a request.
        /// </para>
        /// Corresponds to
        /// <para>
        /// PerScopeLifetime in LightInject.
        /// Although it would seem that this should map to PerRequestLifeTime in LightInject,
        /// that is actually misleading since PerRequestLifeTime in LightInject means transient but disposed at the end of the current web request.
        /// see: https://github.com/seesharper/LightInject/issues/494#issuecomment-518493262
        /// </para>
        /// <para>
        /// Scoped in MS.DI - means one per web request.
        /// see https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-2.2#service-lifetimes</para>
        /// <para>
        /// InstancePerRequest in Autofac - means one per web request.
        /// see https://autofaccn.readthedocs.io/en/latest/lifetime/instance-scope.html#instance-per-request
        /// But "Behind the scenes, though, it’s still just instance per matching lifetime scope."
        /// </para>
        /// <para>
        /// LifestylePerWebRequest in Castle Windsor - means one per web request.
        /// see https://github.com/castleproject/Windsor/blob/master/docs/mvc-tutorial-part-7-lifestyles.md#the-perwebrequest-lifestyle
        /// </para>
        /// </remarks>        
        Request,

        /// <summary>
        /// One unique instance per scope.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Any instance created with this lifetime will be disposed at the end of the current scope.
        /// </para>
        /// Corresponds to
        /// <para>PerScopeLifetime in LightInject (when in a request, means one per web request)</para>
        /// <para>
        /// Scoped in MS.DI (when in a request, means one per web request)
        /// see https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-2.2#service-lifetimes</para>
        /// <para>
        /// InstancePerLifetimeScope in Autofac (when in a request, means one per web request)
        /// see https://autofaccn.readthedocs.io/en/latest/lifetime/instance-scope.html#instance-per-lifetime-scope
        /// Also note that Autofac's InstancePerRequest is the same as this, see https://autofaccn.readthedocs.io/en/latest/lifetime/instance-scope.html#instance-per-request
        /// it says "Behind the scenes, though, it’s still just instance per matching lifetime scope."
        /// </para>
        /// <para>
        /// LifestyleScoped in Castle Windsor
        /// </para>
        /// </remarks>
        Scope,

        /// <summary>
        /// One unique instance per container.
        /// </summary>
        /// <remarks>Corresponds to Singleton in LightInject, Castle Windsor
        /// or MS.DI and to SingleInstance in Autofac.</remarks>
        Singleton
    }
}
