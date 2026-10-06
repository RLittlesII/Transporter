using System;
using Akka.Actor;
using Akka.DependencyInjection;
using Akka.Hosting;
using Akka.Hosting.Maui;
using Microsoft.Maui.Hosting;

namespace Gui.Container;

public class AkkaHostBuilder
{
    public AkkaHostBuilder(MauiAppBuilder mauiAppBuilder) => _mauiAppBuilder = mauiAppBuilder;

    public AkkaHostBuilder AddAkka(string actorSystem, Action<ActorSystem, IActorRegistry> actorStarter)
    {
        _mauiAppBuilder.Services.AddAkkaMaui(actorSystem, akkaConfigurationBuilder =>
        {
            akkaConfigurationBuilder.WithActors(actorStarter);
        });
        return this;
    }

    // The overload an actor with a container-resolved collaborator needs: the resolver is how a
    // dependency the service collection owns reaches a constructor Akka calls (spike 0040).
    public AkkaHostBuilder AddAkka(string actorSystem, Action<ActorSystem, IActorRegistry, IDependencyResolver> actorStarter)
    {
        _mauiAppBuilder.Services.AddAkkaMaui(actorSystem, akkaConfigurationBuilder =>
        {
            akkaConfigurationBuilder.WithActors(actorStarter);
        });
        return this;
    }

    private readonly MauiAppBuilder _mauiAppBuilder;
}

public static class AkkaHostBuilderExtensions
{
    extension(MauiAppBuilder mauiAppBuilder)
    {
        public MauiAppBuilder AddAkkaHost(string actorSystem, Action<ActorSystem, IActorRegistry> actorStarter)
        {
            new AkkaHostBuilder(mauiAppBuilder).AddAkka(actorSystem, actorStarter);
            return mauiAppBuilder;
        }

        public MauiAppBuilder AddAkkaHost(
            string actorSystem,
            Action<ActorSystem, IActorRegistry, IDependencyResolver> actorStarter)
        {
            new AkkaHostBuilder(mauiAppBuilder).AddAkka(actorSystem, actorStarter);
            return mauiAppBuilder;
        }
    }
}
