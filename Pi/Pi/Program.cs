using Akka.Actor;
using Akka.Routing;
using System;
using System.Linq;

namespace Pi
{
   class Program
   {
      static void Main(string[] args)
      {
         var system = ActorSystem.Create("Pi");
         var main = system.ActorOf<Coordinator>();

         main.Tell(new CalculationOptions { Iterations = 10000, Length = 10000, Workers = 4 });
         Console.ReadLine();
      }
   }

   class CalculationOptions
   {
      public short Iterations { get; set; }
      public short Length { get; set; }
      public short Workers { get; set; }
   }

   class Range
   {
      public int Min { get; set; }
      public int Max { get; set; }
   }

   class Coordinator : ReceiveActor
   {
      IActorRef acc;
      IActorRef workers;

      public Coordinator()
      {
         Receive<CalculationOptions>(options =>
         {
            acc = Context.ActorOf(
               Props.Create(() => new Accumulator(options.Iterations)), "accumulator");

            workers = Context.ActorOf(
               Props.Create(() => new Worker(acc)).WithRouter(new RoundRobinPool(options.Workers)), "workers");

            foreach (var x in Enumerable.Range(0, options.Iterations))
               workers.Tell(new Range { Min = x * options.Length, Max = (x + 1) * options.Length });
         });
      }
   }

   class Worker : ReceiveActor
   {
      public Worker(IActorRef accumulator)
      {
         Receive<Range>(range =>
         {
            var result = Enumerable
               .Range(range.Min, range.Max - range.Min)
               .Sum(num => 4 * (Math.Pow(-1, num) / (2 * num + 1)));

            accumulator.Tell(result);
         });
      }
   }

   class Accumulator : ReceiveActor
   {
      int _count;
      int _iterations;
      double _pi;
      DateTime _startTime;

      public Accumulator(int iterations)
      {
         _iterations = iterations;

         Receive<double>(result =>
         {
            _pi += result;
            _count += 1;

            if (_count == _iterations)
               Self.Tell(PoisonPill.Instance);
         });
      }

      protected override void PreStart()
      {
         _startTime = DateTime.Now;
         base.PreStart();
      }

      protected override void PostStop()
      {
         Console.WriteLine("Pi: {0}, in {1}s", _pi, (DateTime.Now - _startTime).TotalSeconds);
         base.PostStop();
      }
   }
}
