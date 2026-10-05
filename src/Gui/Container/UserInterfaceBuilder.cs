using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;

namespace Gui.Container;

public class UserInterfaceBuilder
{
    public UserInterfaceBuilder(MauiAppBuilder mauiAppBuilder) => _mauiAppBuilder = mauiAppBuilder;

    public UserInterfaceBuilder AddUserInterface(Action<IServiceCollection> register)
    {
        register.Invoke(_mauiAppBuilder.Services);

        return this;
    }

    private readonly MauiAppBuilder _mauiAppBuilder;
}

public static class UserInterfaceBuilderExtensions
{
    extension(MauiAppBuilder mauiAppBuilder)
    {
        public MauiAppBuilder AddUserInterface(Action<IServiceCollection> register)
        {
            new UserInterfaceBuilder(mauiAppBuilder).AddUserInterface(register);
            return mauiAppBuilder;
        }
    }
}
