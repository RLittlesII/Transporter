using System;
using Akka.Actor;
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
    }
}
