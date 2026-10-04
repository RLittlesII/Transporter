using Akka.Actor;
using Akka.Hosting;
using Microsoft.Maui.Controls;
using ReactiveMarbles.Mvvm;
using Transponder.Features.Demo.Actors;

namespace Transponder.Features.Demo.ViewModels;

public class DemoViewModel : RxObject
{
    public DemoViewModel(IActorRegistry registry) => ClickCommand = new Command(() => ExecuteClick(registry));

    public Command ClickCommand { get; }

    public string Count { get; set => RaiseAndSetIfChanged(ref field, value); } = "Click me";

    private Task<string> ExecuteClick(IActorRegistry registry) =>
        registry.Get<ClickActor>().Ask<string>(Click.Instance, TimeSpan.FromSeconds(5)).ContinueWith(click => Count = click.Result);
}
