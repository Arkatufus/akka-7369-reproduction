// See https://aka.ms/new-console-template for more information

using Akka.Actor;
using Akka.Hosting;
using Microsoft.Extensions.Hosting;
using StreamTest;

await Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddAkka("test-system", (builder, provider) =>
        {
            builder.WithActors((sys, registry, resolver) =>
            {
                var compute = sys.ActorOf(Props.Create(() => new ComputeActor()));
                registry.Register<ComputeActor>(compute);

                var manager = sys.ActorOf(Props.Create(() => new ManagerActor(compute)));
                registry.Register<ManagerActor>(manager);
            });
        });
    })
    .UseConsoleLifetime()
    .RunConsoleAsync();